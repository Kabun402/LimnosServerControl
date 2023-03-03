using System.Net;

namespace LimnosServerControl.RCon
{
    public class RConLoginCredentials
    {
        public IPAddress Host { get; set; }
        public int Port { get; set; }
        public string Password { get; set; }

        public RConLoginCredentials(IPAddress host, int port, string password)
        {
            Host = host;
            Port = port;
            Password = password;
        }
    }
}
