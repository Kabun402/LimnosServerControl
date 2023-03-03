using LimnosServerControl.Models;
using System;
using System.Net;
using System.Text.RegularExpressions;

namespace LimnosServerControl.Services
{
    public class BanService
    {
        private IConfiguration configuration;
        private PlayerService playerService;
        private RConService rconService;

        private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public BanService(IConfiguration configuration, PlayerService playerService, RConService rconService)
        {
            this.configuration = configuration;
            this.playerService = playerService;
            this.rconService = rconService;
        }

        public async Task<List<Ban>> GetBanListAsync()
        {
            var lines = await File.ReadAllLinesAsync(configuration["Banfile"]);
            var banlist = new List<Ban>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var m = Regex.Match(line, "^((?<guid>[a-f0-9]{32})|(?<ipAddr>[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3})) (?<timestamp>[0-9-]+) (?<reason>.+)$");
                if (m is null || !m.Success)
                    continue;

                var ban = new Ban();
                if (!string.IsNullOrWhiteSpace(m.Groups["guid"].Value))
                    ban.Guid = m.Groups["guid"].Value;
                else
                    ban.IPAddress = m.Groups["ipAddr"].Value;
                ban.Timestamp = long.Parse(m.Groups["timestamp"].Value);
                ban.Reason = m.Groups["reason"].Value;
                banlist.Add(ban);
            }

            return banlist;
        }

        private async Task writeBanList(List<Ban> banlist)
        {
            var lines = new List<string>();
            foreach (var ban in banlist.Where(ban => ban.Guid != null))
            {
                lines.Add($"{ban.Guid} {ban.Timestamp} {ban.Reason}");
            }

            foreach (var ban in banlist.Where(ban => ban.IPAddress != null))
            {
                lines.Add($"{ban.IPAddress} {ban.Timestamp} {ban.Reason}");
            }

            Log($"rewriting banfile");

            try
            {
                await File.WriteAllLinesAsync(configuration["Banfile"], lines);
            }
            catch (Exception e)
            {
                Log(e.Message);
            }

            rconService.ReloadBans();
        }

        public async void BanPlayerAsync(string guid, long timestamp, string reason)
        {
            await semaphoreSlim.WaitAsync();

            Log($"ban request for guid: {guid} with reason: \"{reason}\" until {timestamp}");

            try
            {
                var banlist = await GetBanListAsync();

                var ban = new Ban();
                ban.Guid = guid;
                ban.Timestamp = timestamp;
                ban.Reason = reason;
                banlist.Add(ban);

                await writeBanList(banlist);
            }
            finally
            {
                semaphoreSlim.Release();
            }

            playerService.KickPlayerByGuid(guid, reason);
        }

        public async void BanPlayerAsync(string guid, string ipAddr, long timestamp, string reason)
        {
            await semaphoreSlim.WaitAsync();

            try
            {
                var banlist = await GetBanListAsync();

                var ban = new Ban();
                ban.Guid = guid;
                ban.IPAddress = ipAddr;
                ban.Timestamp = timestamp;
                ban.Reason = reason;
                banlist.Add(ban);

                await writeBanList(banlist);
            }
            finally
            {
                semaphoreSlim.Release();
            }
            
            playerService.KickPlayerByGuid(guid, reason);
        }

        public async void UnbanPlayerAsync(string guid)
        {
            await semaphoreSlim.WaitAsync();

            Log($"unban request for guid: {guid}");

            try
            {
                var banlist = await GetBanListAsync();
                await writeBanList(banlist.Where(b => b.Guid != guid).ToList());
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public async void UnbanPlayerAsync(IPAddress ipAddr)
        {
            await semaphoreSlim.WaitAsync();

            Log($"unban request for ip: {ipAddr}");

            try
            {
                var banlist = await GetBanListAsync();
                await writeBanList(banlist.Where(b => b.IPAddress?.ToString() != ipAddr.ToString()).ToList());
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        private void Log(string message)
        {
            Logger.Log(message, "BanService");
        }
    }
}
