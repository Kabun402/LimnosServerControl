using LimnosServerControl.Models;
using LimnosServerControl.Services.PlayerFilter;
using MySqlConnector;
using System.Data;
using System.Net;
using System.Text.RegularExpressions;

namespace LimnosServerControl.Services
{
    internal class FilterWhitelist
    {
        public NameWhitelist nameWhitelist = NameWhitelist.None;
        public SteamWhitelist steamWhitelist = SteamWhitelist.None;
        public IPWhitelist ipWhitelist = IPWhitelist.None;
    }

    public class PlayerService
    {
        private IConfiguration configuration;
        private ArmaProcessService armaProcessService;
        private RConService rconService;

        private IPFilterService ipFilterService;
        private SteamFilterService steamFilterService;

        private bool runLoop = false;
        private int? armaProcessId;
        private int lastPorcessedLine = int.MinValue;

        public List<Player> Players { get; private set; } = new List<Player>();
        private Dictionary<string, FilterWhitelist> Whitelist = new Dictionary<string, FilterWhitelist>();

        public PlayerService(IConfiguration configuration, ArmaProcessService armaProcessService, RConService rconService)
        {
            this.configuration = configuration;
            this.armaProcessService = armaProcessService;
            this.rconService = rconService;
            this.ipFilterService = new IPFilterService(this.configuration);
            this.steamFilterService = new SteamFilterService(this.configuration);
        }

        public Task StartAsync()
        {
            ReloadWhitelist();

            runLoop = true;
            _ = Loop();

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            runLoop = false;

            return Task.CompletedTask;
        }

        public void KickPlayerByGuid(string guid, string reason)
        {
            var player = Players.SingleOrDefault(p => p.Guid == guid);
            if (player == null)
                return;

            rconService.KickPlayer(reason, player.ID);
        }

        public void KickPlayerBySteamID(string steamID, string reason)
        {
            var player = Players.SingleOrDefault(p => p.SteamID == steamID);
            if (player == null)
                return;

            rconService.KickPlayer(reason, player.ID);
        }

        public List<Player> GetPlayers()
        {
            return Players.ToList();
        }

        private async Task Loop()
        {
            while (runLoop)
            {
                await Task.Delay(5000);

                try
                {
                    if (armaProcessService.ArmaProcessID is null)
                        continue;

                    parseArmaLog();
                    await checkPlayers();

                } catch (Exception e)
                {
                    Log(e.Message);
                }
            }
        }

        private void parseArmaLog()
        {
            if (armaProcessId != armaProcessService.ArmaProcessID)
            {
                if (armaProcessId != null)
                {
                    try
                    {
                        File.Move(
                            Path.Combine(configuration["ArmaLog:Path"], $"arma3server_{armaProcessId}.log"),
                            Path.Combine(configuration["ArmaLog:HistoryPath"], $"arma3server_{DateTime.Now.ToString("yyyy_MM_dd__HH_mm_ss")}.log")
                        );
                    }
                    catch (Exception e)
                    {
                        Log(e.Message);
                    }
                }
                armaProcessId = armaProcessService.ArmaProcessID;
                lastPorcessedLine = int.MinValue;
                Players = new List<Player>();
            }

            if (armaProcessId is null)
            {
                armaProcessId = armaProcessService.ArmaProcessID;
                return;
            }

            var lines = new List<string>();
            using (FileStream stream = File.Open(Path.Combine(configuration["ArmaLog:Path"], $"arma3server_{armaProcessId}.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (line == null)
                            continue;

                        lines.Add(line);
                    }
                }
            }

            Match m;
            int i = 0;
            while (i < lines.Count()) {
                if (i <= lastPorcessedLine)
                {
                    i++;
                    continue;
                }

                m = Regex.Match(lines[i], "^ {0,1}[0-9]{1,2}:[0-9]{2}:[0-9]{2} Host identity created.$");
                if (m.Success)
                {
                    Players = new List<Player>();
                    i++;
                    continue;
                }

                m = Regex.Match(lines[i], "^ {0,1}[0-9]{1,2}:[0-9]{2}:[0-9]{2} BattlEye Server: Player #(?<id>[0-9]{1,}) (?<name>.+) \\((?<ip>[0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}):[0-9]{1,}\\) connected$");
                if (m.Success)
                {
                    Players.Add(new Player(int.Parse(m.Groups["id"].Value), m.Groups["name"].Value, m.Groups["ip"].Value));

                    i++;
                    continue;
                }

                m = Regex.Match(lines[i], "^ {0,1}[0-9]{1,2}:[0-9]{2}:[0-9]{2} Player (?<name>.+) connected \\(id=(?<steamid>[0-9]{17})\\)\\.$");
                if (m.Success)
                {
                    var player = Players.Single(p => p.Name == m.Groups["name"].Value);
                    player.SteamID = m.Groups["steamid"].Value;

                    i++;
                    continue;
                }

                m = Regex.Match(lines[i], "^ {0,1}[0-9]{1,2}:[0-9]{2}:[0-9]{2} BattlEye Server: Verified GUID \\((?<guid>[0-9a-f]{32})\\) of player #(?<id>[0-9]{1,}) (?<name>.+)$");
                if (m.Success)
                {
                    var player = Players.Single(p => p.ID == int.Parse(m.Groups["id"].Value));
                    player.Guid = m.Groups["guid"].Value;

                    i++;
                    continue;
                }

                m = Regex.Match(lines[i], "^ {0,1}[0-9]{1,2}:[0-9]{2}:[0-9]{2} Player (?<name>.+) disconnected\\.$");
                if (m.Success)
                {
                    Players = Players.Where(p => p.Name != m.Groups["name"].Value).ToList();

                    i++;
                    continue;
                }

                i++;
            }
            lastPorcessedLine = i - 1;
        }

        private async Task checkPlayers()
        {
            foreach (var player in Players)
            {
                if (string.IsNullOrEmpty(player.Name) || string.IsNullOrEmpty(player.SteamID) || string.IsNullOrEmpty(player.Guid) || string.IsNullOrEmpty(player.IPAddress))
                    continue;

                if (!player.PlayerCheck.HasFlag(PlayerCheck.Name))
                {
                    if (checkName(player))
                        continue;
                    player.PlayerCheck |= PlayerCheck.Name;
                }

                if (!player.PlayerCheck.HasFlag(PlayerCheck.Steam))
                {
                    if (await checkSteamAsync(player))
                        continue;
                    player.PlayerCheck |= PlayerCheck.Steam;
                }

                if (!player.PlayerCheck.HasFlag(PlayerCheck.IP))
                {
                    if (await checkIPAsync(player))
                        continue;
                    player.PlayerCheck |= PlayerCheck.IP;
                }
            }
        }

        private bool checkName(Player player)
        {
            FilterWhitelist? w;
            if (!Whitelist.TryGetValue(player.SteamID, out w))
                w = new FilterWhitelist();


            if (!w.nameWhitelist.HasFlag(NameWhitelist.Blacklist) && NameFilterService.ContainsBlacklistEntry(player.Name))
            {
                Log($"playername \"{player.Name}\" ({player.SteamID}) violated blacklist");
                rconService.KickPlayer("Namensfilter Regel Nr. 1 | https://limnos.life/regelwerk.html", player.ID);
                return true;
            }

            if (!w.nameWhitelist.HasFlag(NameWhitelist.Charset) && !NameFilterService.CharactersAllowed(player.Name))
            {
                Log($"playername \"{player.Name}\" ({player.SteamID}) violated allowed charlist");
                rconService.KickPlayer("Namensfilter Regel Nr. 2 | https://limnos.life/regelwerk.html", player.ID);
                return true;
            }

            if (!w.nameWhitelist.HasFlag(NameWhitelist.Trimmed) && !NameFilterService.IsTrimmed(player.Name))
            {
                Log($"playername \"{player.Name}\" ({player.SteamID}) is not trimmed");
                rconService.KickPlayer("Namensfilter Regel Nr. 3 | https://limnos.life/regelwerk.html", player.ID);
                return true;
            }

            if (!w.nameWhitelist.HasFlag(NameWhitelist.MinLength) && !NameFilterService.HasMinLength(player.Name))
            {
                Log($"playername \"{player.Name}\" ({player.SteamID}) is to short");
                rconService.KickPlayer("Namensfilter Regel Nr. 4 | https://limnos.life/regelwerk.html", player.ID);
                return true;
            }

            if (!w.nameWhitelist.HasFlag(NameWhitelist.FullName) && !NameFilterService.HasSurAndLastName(player.Name))
            {
                Log($"playername \"{player.Name}\" ({player.SteamID}) violated full name regex");
                rconService.KickPlayer("Namensfilter Regel Nr. 5 | https://limnos.life/regelwerk.html", player.ID);
                return true;
            }

            Log($"player \"{player.Name}\" ({player.SteamID}) passed name check");

            return false;
        }


        private async Task<bool> checkSteamAsync(Player player)
        {



            return false;
        }


        private async Task<bool> checkIPAsync(Player player)
        {
            FilterWhitelist? w;
            if (!Whitelist.TryGetValue(player.SteamID, out w))
                w = new FilterWhitelist();


            if (!w.ipWhitelist.HasFlag(IPWhitelist.Country) && !await ipFilterService.IsWhitelistedCountry(player.IPAddress))
            {
                Log($"player \"{player.Name}\" ({player.SteamID}) dit not pass ip check with ip {player.IPAddress} ... not inside whitelisted country");
                rconService.KickPlayer("Country Restriction DE, AT, CH | Whitelist ts.limnos.life", player.ID);
                return true;
            }

            if (!w.ipWhitelist.HasFlag(IPWhitelist.VPN) && await ipFilterService.IsVPNActive(player.IPAddress))
            {
                Log($"player \"{player.Name}\" ({player.SteamID}) dit not pass ip check with ip {player.IPAddress} ... vpn detected");
                rconService.KickPlayer("VPN is not allowed | Whitelist ts.limnos.life", player.ID);
                return true;
            }

            Log($"player \"{player.Name}\" ({player.SteamID}) passed ip check with ip {player.IPAddress}");
            return false;
        }
        
        public void ModifyWhitelist(string pid, NameWhitelist nameWhitelist, SteamWhitelist steamWhitelist, IPWhitelist ipWhitelist)
        {
            if (!Regex.IsMatch(pid, "^[0-9]{17}$"))
                return;

            Log("changing whitelist...");

            if (Whitelist.ContainsKey(pid))
            {
                Log("modifying entry");
                Whitelist[pid].nameWhitelist = nameWhitelist;
                Whitelist[pid].steamWhitelist = steamWhitelist;
                Whitelist[pid].ipWhitelist = ipWhitelist;
            }
            else
            {
                Log("adding entry");
                var w = new FilterWhitelist();
                w.nameWhitelist = nameWhitelist;
                w.steamWhitelist = steamWhitelist;
                w.ipWhitelist = ipWhitelist;
                Whitelist.Add(pid, w);
            }

            var conStringBuilder = new MySqlConnectionStringBuilder
            {
                Server = configuration["DB:IpAddr"],
                Database = configuration["DB:Name"],
                UserID = configuration["DB:User"],
                Password = configuration["DB:Password"]
            };

            using (var con = new MySqlConnection(conStringBuilder.ConnectionString))
            {
                con.Open();
                var cmd = con.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(pid) FROM lsc_whitelist WHERE pid = '{pid}'";
                var reader = cmd.ExecuteReader();
                reader.Read();
                var count = reader.GetInt32(0);
                reader.Close();
                if (count == 0)
                {
                    Log("inserting entry to db");
                    cmd.CommandText = $"INSERT INTO lsc_whitelist (pid, nameWhitelist, steamWhitelist, ipWhitelist) VALUES ('{pid}', {(int)nameWhitelist}, {(int)steamWhitelist}, {(int)ipWhitelist});";
                }
                else
                {
                    Log("updating entry in db");
                    cmd.CommandText = $"UPDATE lsc_whitelist SET nameWhitelist = {(int)nameWhitelist},  steamWhitelist = {(int)steamWhitelist}, ipWhitelist = {(int)ipWhitelist} WHERE pid = {pid};";
                }
                cmd.ExecuteNonQuery();
            }
        }

        public void ReloadWhitelist()
        {
            Log("reloading whitelist");

            var conStringBuilder = new MySqlConnectionStringBuilder
            {
                Server = configuration["DB:IpAddr"],
                Database = configuration["DB:Name"],
                UserID = configuration["DB:User"],
                Password = configuration["DB:Password"]
            };

            using (var con = new MySqlConnection(conStringBuilder.ConnectionString))
            {
                con.Open();
                var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT pid, nameWhitelist, steamWhitelist, ipWhitelist FROM lsc_whitelist";
                var reader = cmd.ExecuteReader();
                Whitelist.Clear();
                while (reader.Read())
                {
                    var w = new FilterWhitelist();
                    w.nameWhitelist = (NameWhitelist)reader.GetInt32(1);
                    w.steamWhitelist = (SteamWhitelist)reader.GetInt32(2);
                    w.ipWhitelist = (IPWhitelist)reader.GetInt32(3);
                    Whitelist.Add(reader.GetString(0), w);
                }
            }

            Log($"read {Whitelist.Count()} enties");
        }


        private void Log(string message)
        {
            Logger.Log(message, "PlayerService");
        }
    }
}
