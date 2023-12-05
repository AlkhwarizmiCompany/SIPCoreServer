using SIPSorcery.SIP;

namespace SIPServer.Models
{


    public class SIPRemote
    {
        public string protocol { get; set; }
        public string address { get; set; }
        public int port { get; set; }
        public string connectionID { get; set; }
        public string channelID { get; set; }
    }
    public class LogDocument
    {
        public string callId { get; set; }
        public string from { get; set; }
        public string to { get; set; }
        public string uri { get; set; }
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        public SIPRemote Remote { get; set; } = new SIPRemote();

        public List<LogEntry> logs { get; set; } = new List<LogEntry>();

    }
}