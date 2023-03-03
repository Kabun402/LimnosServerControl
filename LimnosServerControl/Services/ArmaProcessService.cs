using System.Diagnostics;

namespace LimnosServerControl.Services
{
    public class ArmaProcessService
    {
        private IConfiguration configuration;

        private bool runLoop = false;
        private bool intentiualStopped = true;
        public int? ArmaProcessID { get; private set; }


        public ArmaProcessService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public Task StartAsync()
        {
            runLoop = true;
            _ = Loop();

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            runLoop = false;

            return Task.CompletedTask;
        }

        private async Task Loop()
        {
            while (runLoop)
            {
                try
                {
                    var processes = Process.GetProcessesByName("arma3server_x64");
                    if (processes.Length < 1)
                    {
                        ArmaProcessID = null;

                        if (intentiualStopped)
                        {
                            await Task.Delay(5000);
                            continue;
                        }

                        Log($"no arma process found ...");
                        StartServer();
                    }
                    else
                    {
                        ArmaProcessID = processes.First().Id;
                        intentiualStopped = false;
                    }
                }
                catch (Exception e)
                {
                    Log(e.Message);
                }

                await Task.Delay(5000);
            }
        }

        public async void RestartServer()
        {
            StopServer();
            await Task.Delay(int.Parse(configuration["Restart:StartDelay"]));
            StartServer();
        }

        public void StopServer()
        {
            intentiualStopped = true;
            ArmaProcessID = null;
            Log($"stopping arma server processes ...");
            var processes = Process.GetProcessesByName("arma3server_x64");
            
            foreach (var proc in processes)
            {
                Log($"killing process with id: {proc.Id}");
                proc.Kill();
            }
        }

        public async void StartServer()
        {
            Log($"starting server ...");

            var processes = Process.GetProcessesByName("arma3server_x64");
            if (processes.Count() > 0)
            {
                Log($"server is already running ...");
                return;
            }

            var startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = configuration["Restart:Path"];
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/C {configuration["Restart:Filename"]}";
            ArmaProcessID = Process.Start(startInfo)?.Id;
            await Task.Delay(3000);
            intentiualStopped = false;
        }

        private void Log(string message)
        {
            Logger.Log(message, "ArmaProcessService");
        }
    }
}
