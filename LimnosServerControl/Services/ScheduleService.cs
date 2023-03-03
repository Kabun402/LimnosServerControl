using MySqlConnector;

namespace LimnosServerControl.Services
{
    public class ScheduleService
    {
        private IConfiguration configuration;
        private RConService rconService;
        private ArmaProcessService armaProcessService;

        private MySqlConnectionStringBuilder conStringBuilder;

        private bool keepRunning = false;

        public ScheduleService(IConfiguration configuration, RConService rconService, ArmaProcessService armaProcessService)
        {
            this.configuration = configuration;
            this.rconService = rconService;
            this.armaProcessService = armaProcessService;

            conStringBuilder = new MySqlConnectionStringBuilder
            {
                Server = this.configuration["DB:IpAddr"],
                Database = this.configuration["DB:Name"],
                UserID = this.configuration["DB:User"],
                Password = this.configuration["DB:Password"]
            };
        }

        public Task StartAsync()
        {
            Log("starting service ...");
            keepRunning = true;
            _ = runLoop();

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            keepRunning = false;

            return Task.CompletedTask;
        }

        private async Task runLoop()
        {
            try
            {
                using var dbSelectCon = new MySqlConnection(conStringBuilder.ConnectionString);
                using var dbUpdateCon = new MySqlConnection(conStringBuilder.ConnectionString);
                await dbSelectCon.OpenAsync();
                await dbUpdateCon.OpenAsync();
                var selectCmd = dbSelectCon.CreateCommand();
                var updateCmd = dbUpdateCon.CreateCommand();

                selectCmd.CommandText = "SELECT id, taskName, params, execTime, ignoreOnce, timeInterval, missedTimeTolerance FROM lsc_schedule WHERE execTime <= now()";

                while (dbSelectCon.State == System.Data.ConnectionState.Open && keepRunning)
                {
                    var reader = await selectCmd.ExecuteReaderAsync();
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string taskName = reader.GetString(1);
                        string? taskParams = reader.IsDBNull(2) ? null : reader.GetString(2);
                        DateTime execTime = reader.GetDateTime(3);
                        bool ignoreOnce = reader.GetBoolean(4);
                        int timeInterval = reader.GetInt32(5);
                        int missedTimeTolerance = reader.GetInt32(6);

                        bool skipDBUpdate = false;

                        Log($"recognized task: <{taskName}>({id}) scheduled for {execTime}");

                        if ((missedTimeTolerance == -1 || execTime.AddSeconds(missedTimeTolerance) >= DateTime.Now) && !ignoreOnce)
                        {
                            try
                            {
                                switch (taskName)
                                {
                                    case "restart":
                                        Log($"running task: <{taskName}>({id})");
                                        armaProcessService.RestartServer();
                                        break;
                                    case "globalmessage":
                                        if (taskParams != null)
                                        {
                                            Log($"running task: <{taskName}>({id}) with parmas: <{taskParams}>");
                                            if (!rconService.SendGlobalMsg(taskParams))
                                            {
                                                Log($"task: <{taskName}>({id}) not executed -> see rcon logs for further details");
                                                skipDBUpdate = true;
                                            }
                                        }
                                        else
                                        {
                                            Log($"task: <{taskName}>({id}) requires params to be not null");
                                        }
                                        break;
                                    default:
                                        Log($"task: <{taskName}>({id}) is not implemented");
                                        break;
                                }
                            }
                            catch (Exception e)
                            {
                                Log($"task: <{taskName}>({id}) params: '{taskParams}' throwed exeption: {e.Message}");
                            }
                        }
                        else
                        {
                            Log($"task: <{taskName}>({id}) skipped because either ignoreOnce <{ignoreOnce}> was set to true or execTime was too far in the past");
                        }

                        if (!skipDBUpdate)
                        {
                            updateCmd.CommandText = $"UPDATE lsc_schedule SET execTime = '{execTime.AddSeconds(timeInterval).ToString("yyyy-MM-dd HH:mm:ss")}', ignoreOnce = 0 WHERE id = {id}";
                            await updateCmd.ExecuteNonQueryAsync();
                        }
                    }
                    reader.Close();

                    await Task.Delay(1000);
                }
            }
            catch (Exception e)
            {
                Log($"service throwed exception: {e.Message}");
            }

            await Task.Delay(500);
            _ = runLoop();
        }

        private void Log(string message)
        {
            Logger.Log(message, "ScheduleService");
        }
    }
}
