using System.Net;
using System.Text.Json.Serialization;

namespace LimnosServerControl.Models
{
    public class Ban
    {
        public string? Guid { get; set; }
        [JsonIgnore]
        private IPAddress? _ipAddress { get; set; }
        public string? IPAddress { get { return _ipAddress?.ToString(); } set { _ipAddress = System.Net.IPAddress.Parse(value); } }
        public long Timestamp { get; set; }
        public string Reason { get; set; }


        public DateTime Until { get { return Timestamp == -1 ? DateTime.MaxValue : DateTimeOffset.FromUnixTimeSeconds(Timestamp).DateTime.ToLocalTime(); } }
        public TimeSpan TimeLeft { get { return Until - DateTime.Now; } }
        public int SecondsLeft { get { return TimeLeft.Seconds; } }
        public int MinutesLeft { get { return TimeLeft.Minutes; } }
        public int HoursLeft { get { return TimeLeft.Hours; } }
        public int DaysLeft { get { return TimeLeft.Days; } }
    }
}
