using LimnosServerControl.RCon;
using System.Net;

namespace LimnosServerControl.Services
{
    public class RConService
    {
        private IConfiguration configuration;
        private RConClient? rconClient;
        private RConLoginCredentials credentials;

        private ArmaProcessService armaProcessService;

        bool stopRequested = false;

        public RConService(IConfiguration configuration, ArmaProcessService armaProcessService)
        {
            this.configuration = configuration;
            this.armaProcessService = armaProcessService;

            credentials = new RConLoginCredentials(
                IPAddress.Parse(this.configuration["RCon:Host"]),
                int.Parse(this.configuration["RCon:Port"]),
                this.configuration["RCon:Password"]
            );

            Log($"rconservice constructed...");
        }

        public Task StartAsync()
        {
            stopRequested = false;

            Log($"starting service");

            _ = Connect();

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            stopRequested = true;

            Log($"stopping service");

            if (rconClient is null)
                return;

            rconClient.Disconnect();

            while (rconClient.Connected)
            {
                await Task.Delay(100);
            }
            
            Log($"service stopped");
        }

        private async Task Connect()
        {
            rconClient = new RConClient(credentials);
            rconClient.RConDisconnected += RconClient_RConDisconnected;
            Log($"trying to connect...");
            while (armaProcessService.ArmaProcessID is null)
            {
                await Task.Delay(1000);
            }

            while (!rconClient.Connect() && !stopRequested)
            {
                Log($"rcon connection could not be established... trying again");
                await Task.Delay(1000);

                while (armaProcessService.ArmaProcessID is null)
                {
                    await Task.Delay(1000);
                }
            }
            Log($"rcon connected");
        }

        public bool SendGlobalMsg(string msg)
        {
            return sendCommand($"say -1 {msg}");
        }

        public bool SendPrivateMsg(string msg, int playerID)
        {
            return sendCommand($"say {playerID} {msg}");
        }

        public bool KickPlayer(string msg, int playerID)
        {
            return sendCommand($"kick {playerID} {msg}");
        }

        public bool ReloadBans()
        {
            return sendCommand($"loadBans");
        }

        private bool sendCommand(string cmd)
        {
            Log($"sendcommand request: {cmd}");

            if (rconClient is null || !rconClient.Connected)
            {
                Log($"rcon is not connected... command not executed");
                return false;
            }

            Log($"executing command...");
            try
            {
                rconClient.SendCommand(cmd);
                Log($"command executed");
                return true;
            } catch (Exception ex)
            {
                Log($"exception thrown while sending command... exception msg: \"{ex.Message}\"");
                return false;
            }
        }
        private void RconClient_RConDisconnected(RCon.Events.RConDisconnectEventArgs args)
        {
            if (stopRequested)
                return;

            Log($"rcon connection lost... trying to connect again");

            _ = Connect();
        }

        private void Log(string message)
        {
            Logger.Log(message, "RConService");
        }
    }
}
