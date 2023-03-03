using System.Net;
using System.Text.Json.Serialization;

namespace LimnosServerControl.Models
{
    [Flags]
    public enum PlayerCheck
    {
        None = 0,
        Name = 1,
        Steam = 2,
        IP = 4,
    }

    [Flags]
    public enum NameWhitelist
    {
        None = 0,
        Charset = 1,
        MinLength = 2,
        Trimmed = 4,
        FullName = 8,
        Blacklist = 16,
    }

    [Flags]
    public enum IPWhitelist
    {
        None = 0,
        Country = 1,
        VPN = 2,
    }

    [Flags]
    public enum SteamWhitelist
    {
        None = 0,
        PlayTime = 1,
        AccountAge = 2,
    }


    public class Player
    {
        public int ID { get; set; }
        public string Guid { get; set; } = "";
        public string SteamID { get; set; } = "";
        public string Name { get; set; }
        [JsonIgnore]
        private IPAddress _ipAddress { get; set; }
        public string IPAddress { get { return _ipAddress.ToString(); } set { _ipAddress = System.Net.IPAddress.Parse(value); } }


        public PlayerCheck PlayerCheck { get; set; } = PlayerCheck.None;

        public Player(int ID, string Name, string IPAddress)
        {
            this.ID = ID;
            this.Name = Name;
            this._ipAddress = System.Net.IPAddress.Parse(IPAddress);
        }
    }
}
