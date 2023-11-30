using System.Net;
using Serilog;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using SIPSorcery;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorcery.Sys;
using SIPSorceryMedia.Abstractions;
using Windows.System;
using Windows.Devices.Radios;
using NAudio.Wave;
using System.IO;

namespace SIPServer
{
    struct SIPRegisterAccount
    {
        public string Username;
        public string Password;
        public string Domain;
        public int Expiry;

        public SIPRegisterAccount(string username, string password, string domain, int expiry)
        {
            Username = username;
            Password = password;
            Domain = domain;
            Expiry = expiry;
        }
    }

    struct SIPAcceptedCallsCall
    {
        public SIPUserAgent UA;
        public SIPServerUserAgent UAS;
        public string User;

        public SIPAcceptedCallsCall(SIPUserAgent ua, SIPServerUserAgent uas, string user)
        {
            UA = ua;
            UAS = uas;
            User = user;
        }
    }

    internal class Server
    {
        private static SIPTransport _sipTransport;
        private static int SIP_LISTEN_PORT = 5060;
        private static Microsoft.Extensions.Logging.ILogger Log = NullLogger.Instance;
        private static Action<string> _appendToLog;
        private static WaveFileWriter _waveFile;
        private static readonly WaveFormat _waveFormat = new WaveFormat(8000, 16, 1);

        private static IWavePlayer waveOutDevice;

        private static ConcurrentDictionary<string, SIPRegisterAccount> Registrations = new ConcurrentDictionary<string, SIPRegisterAccount>();
        private static ConcurrentDictionary<string, SIPAcceptedCallsCall> AcceptedCalls = new ConcurrentDictionary<string, SIPAcceptedCallsCall>();
        private static ConcurrentDictionary<string, SIPUserAgent> ActiveCalls = new ConcurrentDictionary<string, SIPUserAgent>();
        SIPAcceptedCallsCall call;

        public Server(Action<string> appendToLog)
        {
            _appendToLog = appendToLog; // Store the logging action

            _sipTransport = new SIPTransport();
            _sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, SIP_LISTEN_PORT)));

            _sipTransport.EnableTraceLogs();

            _sipTransport.SIPTransportRequestReceived += OnRequest;

            _waveFile = new WaveFileWriter("C:\\Users\\IGFI\\Desktop\\repos\\SIPCoreServer\\SIPServer\\output.mp3", _waveFormat);
            waveOutDevice = new WaveOutEvent();
            
            Log = AddConsoleLogger();
        }

        public async Task AnswerCall(string user)
        {
            if (!AcceptedCalls.TryGetValue(user, out call))
                return;

            var rtpSession = CreateRtpSession(call.UA, call.User);
            await call.UA.Answer(call.UAS, rtpSession);

            if (call.UA.IsCallActive)
            {
                await rtpSession.Start();
                ActiveCalls.TryAdd(call.UA.Dialogue.CallId, call.UA);
            }
            _appendToLog?.Invoke($"Call Answerd: from {call.UA.ContactURI}");

        }
        public async Task EndCall()
        {
            if (ActiveCalls.TryRemove(call.UA.Dialogue.CallId, out var userAgent))
            {
                userAgent.Hangup();
                _appendToLog?.Invoke($"Call ended.");
            }
            else
            {
                _appendToLog?.Invoke($"No active call with user {call.User} found.");
            }
        }

        private static async Task OnRequest(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            try
            {
                SIPRegisterAccount user;

                user.Username = sipRequest.Header.From.FromURI.User;
                user.Password = "xxx";
                user.Expiry = 1;
                user.Domain = "xyz";
              
                if (sipRequest.Method == SIPMethodsEnum.INVITE)
                {
                    _appendToLog?.Invoke($"Incoming call request: {sipRequest.URI}.");

                    SIPUserAgent ua = new SIPUserAgent(_sipTransport, null);
                    ua.OnCallHungup += OnHangup;

                    var uas = ua.AcceptCall(sipRequest);

                    AcceptedCalls.TryAdd($"{user.Username}@{user.Domain}", new SIPAcceptedCallsCall(ua, uas, sipRequest.URI.User));
                }
                else if (sipRequest.Method == SIPMethodsEnum.BYE)
                {
                    SIPResponse byeResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null);
                    await _sipTransport.SendResponseAsync(byeResponse);
                    _appendToLog?.Invoke($"Call Ended");

                }
                else if (sipRequest.Method == SIPMethodsEnum.SUBSCRIBE)
                {
                    SIPResponse notAllowededResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.MethodNotAllowed, null);
                    await _sipTransport.SendResponseAsync(notAllowededResponse);
                }
                else if (sipRequest.Method == SIPMethodsEnum.OPTIONS || sipRequest.Method == SIPMethodsEnum.REGISTER)
                {

                    Registrations.TryAdd($"{user.Username}@{user.Domain}", user);

                    SIPResponse optionsResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                    await _sipTransport.SendResponseAsync(optionsResponse);
                    _appendToLog?.Invoke($"Registration received: {user.Username}@{user.Domain}");

                }
            }
            catch (Exception reqExcp)
            {
                Log.LogWarning($"Exception handling {sipRequest.Method}. {reqExcp.Message}");
            }
        }

        /// <summary>
        /// Example of how to create a basic RTP session object and hook up the event handlers.
        /// </summary>
        /// <param name="ua">The user agent the RTP session is being created for.</param>
        /// <param name="dst">THe destination specified on an incoming call. Can be used to
        /// set the audio source.</param>
        /// <returns>A new RTP session object.</returns>
        private static VoIPMediaSession CreateRtpSession(SIPUserAgent ua, string dst)
        {
            List<AudioCodecsEnum> codecs = new List<AudioCodecsEnum> { AudioCodecsEnum.PCMU, AudioCodecsEnum.PCMA, AudioCodecsEnum.G722 };

            var audioSource = AudioSourcesEnum.SineWave;
            if (string.IsNullOrEmpty(dst) || !Enum.TryParse(dst, out audioSource))
            {
                audioSource = AudioSourcesEnum.Music;
            }

            Log.LogInformation($"RTP audio session source set to {audioSource}.");

            AudioExtrasSource audioExtrasSource = new AudioExtrasSource(new AudioEncoder(), new AudioSourceOptions { AudioSource = audioSource });
            audioExtrasSource.RestrictFormats(formats => codecs.Contains(formats.Codec));
            var rtpAudioSession = new VoIPMediaSession(new MediaEndPoints { AudioSource = audioExtrasSource });
            rtpAudioSession.AcceptRtpFromAny = true;

            // Wire up the event handler for RTP packets received from the remote party.
            rtpAudioSession.OnRtpPacketReceived += (ep, type, rtp) => OnRtpPacketReceived(ua, type, rtp);
            rtpAudioSession.OnTimeout += (mediaType) =>
            {
                if (ua?.Dialogue != null)
                {
                    Log.LogWarning($"RTP timeout on call with {ua.Dialogue.RemoteTarget}, hanging up.");
                }
                else
                {
                    Log.LogWarning($"RTP timeout on incomplete call, closing RTP session.");
                }

                ua.Hangup();
            };

            return rtpAudioSession;
        }

        private static void PlaySound(byte[] pcmSample)
        {
            using (var stream = new MemoryStream(pcmSample))
            {
                stream.Position = 0;

                // Create a WaveChannel32 to ensure the audio is in the correct format.
                var waveChannel = new WaveChannel32(new WaveFileReader(stream));

                // Initialize the WaveOutDevice with the WaveChannel32.
                waveOutDevice.Init(waveChannel);

                // Start playing the audio.
                waveOutDevice.Play();
            }

        }

        /// <summary>
        /// Event handler for receiving RTP packets.
        /// </summary>
        /// <param name="ua">The SIP user agent associated with the RTP session.</param>
        /// <param name="type">The media type of the RTP packet (audio or video).</param>
        /// <param name="rtpPacket">The RTP packet received from the remote party.</param>
        private static void OnRtpPacketReceived(SIPUserAgent ua, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket)
        {
            if (mediaType == SDPMediaTypesEnum.audio)
            {
                var sample = rtpPacket.Payload;
                for (int index = 0; index < sample.Length; index++)
                {
                    short pcm;
                    if (rtpPacket.Header.PayloadType == (int)SDPWellKnownMediaFormatsEnum.PCMA)
                    {
                        pcm = NAudio.Codecs.ALawDecoder.ALawToLinearSample(sample[index]);
                        //byte[] pcmSample = new byte[] { (byte)(pcm & 0xFF), (byte)(pcm >> 8) };
                        //_waveFile.Write(pcmSample, 0, 2);
                    }
                    else
                    {
                        pcm = NAudio.Codecs.MuLawDecoder.MuLawToLinearSample(sample[index]);
                        //byte[] pcmSample = new byte[] { (byte)(pcm & 0xFF), (byte)(pcm >> 8) };
                        //_waveFile.Write(pcmSample, 0, 2);
                    }

                    byte[] pcmSample = new byte[] { (byte)(pcm & 0xFF), (byte)(pcm >> 8) };
                    _waveFile.Write(pcmSample, 0, 2);

                    //PlaySound(pcmSample);
                }
            }
        }

        /// <summary>
        /// Event handler for receiving a DTMF tone.
        /// </summary>
        /// <param name="ua">The user agent that received the DTMF tone.</param>
        /// <param name="key">The DTMF tone.</param>
        /// <param name="duration">The duration in milliseconds of the tone.</param>
        private static void OnDtmfTone(SIPUserAgent ua, byte key, int duration)
        {
            string callID = ua.Dialogue.CallId;
            Log.LogInformation($"Call {callID} received DTMF tone {key}, duration {duration}ms.");
        }

        /// <summary>
        /// Remove call from the active calls list.
        /// </summary>
        /// <param name="dialogue">The dialogue that was hungup.</param>
        private static void OnHangup(SIPDialogue dialogue)
        {
            _waveFile?.Close();

            if (waveOutDevice != null)
            {
                waveOutDevice.Stop();
                waveOutDevice.Dispose();
                waveOutDevice = null;
            }

            if (dialogue != null)
            {
                string callID = dialogue.CallId;
                if (ActiveCalls.ContainsKey(callID))
                {
                    if (ActiveCalls.TryRemove(callID, out var ua))
                    {
                        // This app only uses each SIP user agent once so here the agent is 
                        // explicitly closed to prevent is responding to any new SIP requests.
                        ua.Close();
                    }
                }
            }
        }

        private static Microsoft.Extensions.Logging.ILogger AddConsoleLogger()
        {
            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                .WriteTo.Console()
                .CreateLogger();
            var factory = new SerilogLoggerFactory(serilogLogger);
            SIPSorcery.LogFactory.Set(factory);
            return factory.CreateLogger<Server>();
        }
    }
}