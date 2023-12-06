using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using System.Collections.Concurrent;

namespace SIPServer.Models
{
    class SIPCall
    {
        public string User { get; set; }
        public SIPRequest SipRequest { get; set; }
        public SIPUserAgent UA { get; set; }
        public SIPServerUserAgent UAS { get; set; }
        public VoIPMediaSession RtpSession { get; set; }
        public BlockingCollection<byte[]> CallAudio         { get; set; }
        public BlockingCollection<string> TranscriptedText  { get; set; }
        public BlockingCollection<string> ChatbotAnswers    { get; set; }
        public BlockingCollection<byte[]> ResponseAudio     { get; set; }

        public bool IsRunning { get; set; }
        public byte[] pcmSamples;

        public Logger logger;
        private readonly Action<string> AppendToLog;

        public SIPCall(SIPUserAgent ua, SIPServerUserAgent uas, SIPRequest sipRequest, Action<string> appendToLog)
        {
            UA = ua;
            UAS = uas;
            SipRequest = sipRequest;
            User = sipRequest.RemoteSIPEndPoint.ToString();
            AppendToLog = appendToLog;

            CallAudio           = new BlockingCollection<byte[]>();
            TranscriptedText    = new BlockingCollection<string>();
            ChatbotAnswers      = new BlockingCollection<string>();
            ResponseAudio       = new BlockingCollection<byte[]>();

            IsRunning = false;


            logger = new Logger("mongodb://localhost:27017", "SIP", "CallLogs");

            pcmSamples = new byte[0];
            RtpSession = null;
        }
        
        private LogEntry GetEntry(string msg)
        {
            LogEntry entry = new LogEntry();
            entry.message = msg;
            entry.timestamp = DateTime.UtcNow;

            AppendToLog(msg);

            return entry;
        }
        public void Log(string msg)
        {

            logger.Log(SipRequest.RemoteSIPEndPoint.ToString(), GetEntry(msg));
        }

        public void InitCallLog(string msg)
        {
            LogDocument logDocument = new LogDocument();

            logDocument.callId = SipRequest.RemoteSIPEndPoint.ToString();
            //logDocument.from = UA.CallDescriptor.From;
            //logDocument.to = UA.CallDescriptor.To;
            //logDocument.uri = UA.CallDescriptor.Uri;

            logDocument.Remote.protocol = SipRequest.RemoteSIPEndPoint.Protocol.ToString();
            logDocument.Remote.address = SipRequest.RemoteSIPEndPoint.Address.ToString();
            logDocument.Remote.port = SipRequest.RemoteSIPEndPoint.Port;
            logDocument.Remote.channelID = SipRequest.RemoteSIPEndPoint.ChannelID;
            logDocument.Remote.connectionID = SipRequest.RemoteSIPEndPoint.ConnectionID;


            logDocument.logs.Add(GetEntry(msg));

            logger.CreateLogDocument(logDocument);
        }
    }
}