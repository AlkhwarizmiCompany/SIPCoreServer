using NAudio.Wave;
using SIPSorcery.Media;
using SIPSorcery.SIP.App;
using System.Collections.Concurrent;
using Windows.Media.SpeechRecognition;

namespace SIPServer.Models
{
    class SIPCall
    {
        public string User { get; set; }
        public SIPUserAgent UA { get; set; }
        public SIPServerUserAgent UAS { get; set; }
        public VoIPMediaSession RtpSession { get; set; }
        public BlockingCollection<byte[]> CallAudio         { get; set; }
        public BlockingCollection<string> TranscriptedText  { get; set; }
        public BlockingCollection<string> ChatbotAnswers    { get; set; }
        public BlockingCollection<byte[]> ResponseAudio     { get; set; }

        public bool IsRunning { get; set; }
        public WaveFileWriter WaveFile;
        public byte[] pcmSamples;

        private static readonly WaveFormat _waveFormat = new WaveFormat(8000, 16, 1);


        public SIPCall(SIPUserAgent ua, SIPServerUserAgent uas, string user)
        {
            UA = ua;
            UAS = uas;
            User = user;

            CallAudio           = new BlockingCollection<byte[]>();
            TranscriptedText    = new BlockingCollection<string>();
            ChatbotAnswers      = new BlockingCollection<string>();
            ResponseAudio       = new BlockingCollection<byte[]>();

            IsRunning = false;

            WaveFile = new WaveFileWriter("G:\\src\\SIP\\SIPServer\\SIPServer\\Assets\\audio\\output5.mp3", _waveFormat);
            
            pcmSamples = new byte[0];
            RtpSession = null;
        }
    }
}