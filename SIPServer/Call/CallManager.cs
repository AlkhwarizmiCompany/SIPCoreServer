using Microsoft.Extensions.Configuration;
using SIPServer.Models;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.IO;
using System.Net;

namespace SIPServer.Call
{
    class CallManager
    {
        private readonly SpeechToText   STT;
        private readonly TextToSpeech   TTS;
        private readonly Chatbot        Chatbot;
        private readonly MicAudio       MicAudio;

        private SIPCall                 Call;

        private readonly Action<string> AppendToLog;
        IConfigurationRoot              configuration;


        private const string WELCOME_8K = "G:\\src\\console\\sipsorcery\\examples\\SIPExamples\\PlaySounds\\Sounds\\hellowelcome8k.raw";
        private const string GOODBYE_16K = "G:\\src\\console\\sipsorcery\\examples\\SIPExamples\\PlaySounds\\Sounds\\goodbye16k.raw";


        public CallManager(SIPCall call, Action<string> appendToLog, IConfiguration configuration)
        {
            var cred = configuration["GoogleAppCredentials"];

            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "G:\\src\\SIP\\SIPServer\\SIPServer\\Assets\\tensile-axiom-281814-5428b0b1f7b0.json");

            Call        = call;
            AppendToLog = appendToLog;


            //MicAudio        = new MicAudio(Call, AppendToLog);
            STT             = new SpeechToText(Call, AppendToLog);
            Chatbot         = new Chatbot(Call, AppendToLog);
            TTS             = new TextToSpeech(Call, AppendToLog);

            Chatbot.Run();
            TTS.Run();
        }

        public async Task<bool> AnswerAsync()
        {
            Call.RtpSession = CreateRtpSession();

            await Call.UA.Answer(Call.UAS, Call.RtpSession);

            if (Call.UA.IsCallActive)
            {

                await Call.RtpSession.Start();
                //await Call.RtpSession.AudioExtrasSource.StartAudio();
                AudioFormat audioFormat = new AudioFormat(AudioCodecsEnum.PCMA, 1);
                Call.RtpSession.AudioExtrasSource.SetAudioSourceFormat(audioFormat);

                await Call.RtpSession.AudioExtrasSource.SendAudioFromStream(new FileStream(GOODBYE_16K, FileMode.Open), AudioSamplingRatesEnum.Rate16KHz);

                //await Call.RtpSession.AudioExtrasSource.SendAudioFromStream(new FileStream(WELCOME_8K, FileMode.Open), AudioSamplingRatesEnum.Rate8KHz);
            }

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

            AppendToLog($"RTP audio session source set to {audioSource}.");
            
            return rtpAudioSession;
        }

        private void OnTimeout(SDPMediaTypesEnum mediaType)
        {
            if (Call.UA?.Dialogue != null)
            {
                AppendToLog($"RTP timeout on call with {Call.UA.Dialogue.RemoteTarget}, hanging up.");
            }
            else
            {
                AppendToLog($"RTP timeout on incomplete call, closing RTP session.");
            }

            Call.UA.Hangup();
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
                    //Call.WaveFile.Write(pcmSample, 0, 2);
                    Call.pcmSamples = Call.pcmSamples.Concat(pcmSample).ToArray();

                    if (Call.pcmSamples.Length >= 1600)
                    {
                        Call.CallAudio.Add(Call.pcmSamples);
                        Call.pcmSamples = new byte[0];
                    }
                }
            }
            
            //stopwatch.Stop();

            //System.TimeSpan elapsedTime = stopwatch.Elapsed;

            //double milliseconds = elapsedTime.TotalMilliseconds;


            //AppendToLog($"milliseconds: {milliseconds}");
        }

    }
}
