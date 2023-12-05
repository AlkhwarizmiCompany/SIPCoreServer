namespace SIPServer.Models
{
    public class LogEntry
    {
        public string filename { get; set; }
        public string logLevel { get; set; }
        public string message { get; set; }
        public DateTime timestamp { get; set; }
    }
}