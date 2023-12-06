using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIPServer.Models;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.Net;

namespace SIPServer.Call
{
    class CallManager
    {
        private readonly SpeechToText   _STT;
        private readonly TextToSpeech   _TTS;
        private readonly Chatbot        _chatbot;
        private readonly MicAudio       _micAudio;


        private SIPCall                     _call;
        private readonly IConfiguration     _configuration;
        private readonly IServiceProvider   _serviceProvider;

        private readonly bool               USE_MIC;

        private const string WELCOME_8K = "G:\\src\\console\\sipsorcery\\examples\\SIPExamples\\PlaySounds\\Sounds\\hellowelcome8k.raw";
        private const string GOODBYE_16K = "G:\\src\\console\\sipsorcery\\examples\\SIPExamples\\PlaySounds\\Sounds\\goodbye16k.raw";


        public CallManager(IConfiguration configuration, IServiceProvider serviceProvider, SIPCall call)
        {


            _call = call;
            _configuration = configuration;
            _serviceProvider = serviceProvider;

            if (!bool.TryParse(_configuration["useMic"], out USE_MIC))
                USE_MIC = false;

            if(USE_MIC)
                _micAudio   = ActivatorUtilities.CreateInstance<MicAudio>(_serviceProvider, _call);
            
            _STT        = ActivatorUtilities.CreateInstance<SpeechToText>(_serviceProvider, _call);
            _chatbot    = ActivatorUtilities.CreateInstance<Chatbot>(_serviceProvider, _call);
            _TTS        = ActivatorUtilities.CreateInstance<TextToSpeech>(_serviceProvider, _call);

            if (USE_MIC)
                _micAudio.Start();

            _STT.Start();
            _chatbot.Start();
            _TTS.Start();
        }

        public async Task<bool> AnswerAsync()
        {
            _call.RtpSession = CreateRtpSession();


            await _call.UA.Answer(_call.UAS, _call.RtpSession);

            if (!_call.UA.IsCallActive)
                return false;
            
            await _call.RtpSession.Start();

            //await _call.RtpSession.AudioExtrasSource.StartAudio();
            //AudioFormat audioFormat = new AudioFormat(AudioCodecsEnum.PCMA, 1);
            //_call.RtpSession.AudioExtrasSource.SetAudioSourceFormat(audioFormat);
            //await _call.RtpSession.AudioExtrasSource.SendAudioFromStream(new FileStream(GOODBYE_16K, FileMode.Open), AudioSamplingRatesEnum.Rate16KHz);
            //await _call.RtpSession.AudioExtrasSource.SendAudioFromStream(new FileStream(WELCOME_8K, FileMode.Open), AudioSamplingRatesEnum.Rate8KHz);
            

            return true;
        }
        private VoIPMediaSession CreateRtpSession()
        {
            List<AudioCodecsEnum> codecs = new List<AudioCodecsEnum> { AudioCodecsEnum.PCMU, AudioCodecsEnum.PCMA, AudioCodecsEnum.G722, AudioCodecsEnum.L16  };

            AudioSourcesEnum audioSource        = AudioSourcesEnum.Silence;
            AudioSourceOptions audioOptions     = new AudioSourceOptions { AudioSource = audioSource };
            AudioExtrasSource audioExtrasSource = new AudioExtrasSource(new AudioEncoder());

            audioExtrasSource.RestrictFormats(format => format.Codec == AudioCodecsEnum.PCMA);
            //audioExtrasSource.RestrictFormats(formats => codecs.Contains(formats.Codec));

            MediaEndPoints mediaEndPoints       = new MediaEndPoints { AudioSource = audioExtrasSource };
            VoIPMediaSession rtpAudioSession    = new VoIPMediaSession(mediaEndPoints);
            
            rtpAudioSession.AcceptRtpFromAny = true;
            rtpAudioSession.OnRtpPacketReceived +=  OnRtpPacketReceived;
            rtpAudioSession.OnTimeout +=  OnTimeout;

            _call.Log($"RTP audio session source set to {audioSource}.");
            
            return rtpAudioSession;
        }

        private void OnTimeout(SDPMediaTypesEnum mediaType)
        {
            if (_call.UA?.Dialogue != null)
            {
                _call.Log($"RTP timeout on call with {_call.UA.Dialogue.RemoteTarget}, hanging up.");
            }
            else
            {
                _call.Log($"RTP timeout on incomplete call, closing RTP session.");
            }

            _call.UA.Hangup();
        }

        private void OnRtpPacketReceived(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket)
        {
            //Stopwatch stopwatch = new Stopwatch();

            //stopwatch.Start();

            if (mediaType == SDPMediaTypesEnum.audio)
            {
                var sample = rtpPacket.Payload;

                for (int index = 0; index < sample.Length; index++)
                {
                    short pcm;

                    if (rtpPacket.Header.PayloadType == (int)SDPWellKnownMediaFormatsEnum.PCMA)
                        pcm = NAudio.Codecs.ALawDecoder.ALawToLinearSample(sample[index]);
                    else
                        pcm = NAudio.Codecs.MuLawDecoder.MuLawToLinearSample(sample[index]);

                    byte[] pcmSample = new byte[] { (byte)(pcm & 0xFF), (byte)(pcm >> 8) };
                    //_call.WaveFile.Write(pcmSample, 0, 2);
                    _call.pcmSamples = _call.pcmSamples.Concat(pcmSample).ToArray();

                    if (_call.pcmSamples.Length >= 1600)
                    {
                        _call.CallAudio.Add(_call.pcmSamples);
                        _call.pcmSamples = new byte[0];
                    }
                }
            }
            
            //stopwatch.Stop();

            //System.TimeSpan elapsedTime = stopwatch.Elapsed;

            //double milliseconds = elapsedTime.TotalMilliseconds;


            //AppendToLog($"milliseconds: {milliseconds}");
        }

        public void Stop()
        {
            _call.UA.Hangup();
            _call.RtpSession.Close("");

            if(USE_MIC)
                _micAudio.Stop();

            _STT.Stop();
            _chatbot.Stop();
            _TTS.Stop();

            _call.Log($"Call ended.");
        }
    }
}
