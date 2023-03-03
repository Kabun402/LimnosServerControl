using System.Net;

namespace LimnosServerControl.Services.PlayerFilter
{

    internal class IPPrivaceInformation
    {
        public bool vpn { get; set; }
        public bool proxy { get; set; }
        public bool tor { get; set; }
        public bool relay { get; set; }
        public bool hosting { get; set; }
        public string? service { get; set; }
    }

    internal class IPAPIResponse
    {
        public string? ip { get; set; }
        public string? hostname { get; set; }
        public string? city { get; set; }
        public string? region { get; set; }
        public string? country { get; set; }
        public string? loc { get; set; }
        public string? org { get; set; }
        public string? postal { get; set; }
        public string? timezone { get; set; }
        public IPPrivaceInformation privacy { get; set; } = new IPPrivaceInformation();

        public DateTime requestedTime = DateTime.Now;
    }

    public class IPFilterService
    {
        private IConfiguration configuration;
        private static List<IPAPIResponse> _checkedIPAddresses = new List<IPAPIResponse>();

        private List<string> _countryWhitelist = new List<string>
        {
            "DE",
            "AT",
            "CH"
        };

        public IPFilterService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task<bool> IsWhitelistedCountry(string ipAddress)
        {
            var ipInfo = _checkedIPAddresses.SingleOrDefault(info => info.ip == ipAddress);
            if (ipInfo == null)
            {
                ipInfo = await requestIPInfo(ipAddress);
                _checkedIPAddresses.Add(ipInfo);
            }
            else if (ipInfo.requestedTime.AddHours(24) <= DateTime.Now)
            {
                ipInfo = await requestIPInfo(ipAddress);
            }

            return _countryWhitelist.Contains(ipInfo.country ?? "DE");
        }

        public async Task<bool> IsVPNActive(string ipAddress)
        {
            var ipInfo = _checkedIPAddresses.SingleOrDefault(info => info.ip == ipAddress);
            if (ipInfo == null)
            {
                ipInfo = await requestIPInfo(ipAddress);
                _checkedIPAddresses.Add(ipInfo);
            }
            else if (ipInfo.requestedTime.AddHours(24) <= DateTime.Now)
            {
                ipInfo = await requestIPInfo(ipAddress);
            }

            return ipInfo.privacy.vpn;
        }

        private async Task<IPAPIResponse> requestIPInfo(string ipAddress)
        {
            IPAPIResponse? responseObj = null;

            Log($"requesting info for {ipAddress}");

            var client = new HttpClient();
            var response = await client.GetAsync($"https://ipinfo.io/{ipAddress}?token={configuration["IPAPIToken"]}");
            if (response.IsSuccessStatusCode)
            {
                var responseMsg = await response.Content.ReadAsStringAsync();
                responseObj = System.Text.Json.JsonSerializer.Deserialize<IPAPIResponse>(responseMsg);
            }
            else
            {
                Log($"ipapi returned status {response.StatusCode} with contend: {await response.Content.ReadAsStringAsync()}");
            }

            if (responseObj == null)
            {
                Log($"response contend: \"{await response.Content.ReadAsStringAsync()}\" could not be parsed to object");
                responseObj = new IPAPIResponse();
                responseObj.ip = ipAddress.ToString();
                responseObj.privacy = new IPPrivaceInformation();
            }

            return responseObj;
        }


        private void Log(string message)
        {
            Logger.Log(message, "IPFilterService");
        }
    }
}
