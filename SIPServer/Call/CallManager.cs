using SIPServer.Models;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIPServer.Call
{
    class CallManager
    {
        private readonly SpeechToText   STT;
        private readonly TextToSpeech   TTS;
        private readonly Chatbot        Chatbot;
        private readonly MicAudio       MicAudio;

        private readonly SIPCall        Call;
        private readonly Action<string> AppendToLog;
        public CallManager(SIPCall call, Action<string> appendToLog)
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", @"C:\credentials\credentials.json");

            Call        = call;
            AppendToLog = appendToLog;

            MicAudio        = new MicAudio(Call, AppendToLog);
            STT             = new SpeechToText(Call, AppendToLog);
            Chatbot         = new Chatbot(Call, AppendToLog);
            TTS             = new TextToSpeech(Call, AppendToLog);

            Chatbot.Run();
            TTS.Run();
        }

        public async Task<bool> AnswerAsync()
        {
            var rtpSession = CreateRtpSession(Call.UA, Call.User);

            await Call.UA.Answer(Call.UAS, rtpSession);

            if (Call.UA.IsCallActive)
                await rtpSession.Start();


            return true;
        }
        private VoIPMediaSession CreateRtpSession(SIPUserAgent ua, string dst)
        {
            List<AudioCodecsEnum> codecs = new List<AudioCodecsEnum> { AudioCodecsEnum.PCMU, AudioCodecsEnum.PCMA, AudioCodecsEnum.G722 };

            var audioSource = AudioSourcesEnum.SineWave;
            if (string.IsNullOrEmpty(dst) || !Enum.TryParse(dst, out audioSource))
            {
                audioSource = AudioSourcesEnum.Music;
            }

            AppendToLog($"RTP audio session source set to {audioSource}.");

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
                    AppendToLog($"RTP timeout on call with {ua.Dialogue.RemoteTarget}, hanging up.");
                }
                else
                {
                    AppendToLog($"RTP timeout on incomplete call, closing RTP session.");
                }

                ua.Hangup();
            };

            return rtpAudioSession;
        }

        private void OnRtpPacketReceived(SIPUserAgent ua, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket)
        {
            if (mediaType == SDPMediaTypesEnum.audio)
            {
                var sample = rtpPacket.Payload;
                Call.CallAudio.Add(sample);

            }
        }

    }
}
