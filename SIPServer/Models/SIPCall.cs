using SIPSorcery.SIP.App;
using System.Collections.Concurrent;

namespace SIPServer.Models
{
    struct SIPCall
    {
        public string User { get; set; }
        public SIPUserAgent UA { get; set; }
        public SIPServerUserAgent UAS { get; set; }

        public BlockingCollection<byte[]> CallAudio         { get; set; }
        public BlockingCollection<string> TranscriptedText  { get; set; }
        public BlockingCollection<string> ChatbotAnswers    { get; set; }
        public BlockingCollection<byte[]> ResponseAudio     { get; set; }

        public SIPCall(SIPUserAgent ua, SIPServerUserAgent uas, string user)
        {
            UA = ua;
            UAS = uas;
            User = user;

            CallAudio           = new BlockingCollection<byte[]>();
            TranscriptedText    = new BlockingCollection<string>();
            ChatbotAnswers      = new BlockingCollection<string>();
            ResponseAudio       = new BlockingCollection<byte[]>();
        }
    }
}