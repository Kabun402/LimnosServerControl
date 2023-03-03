using LimnosServerControl.Services;

namespace LimnosServerControl
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                _ = scope.ServiceProvider.GetRequiredService<AuthService>();

                var armaProcessService = scope.ServiceProvider.GetRequiredService<ArmaProcessService>();
                await armaProcessService.StartAsync();

                var rconService = scope.ServiceProvider.GetRequiredService<RConService>();
                await rconService.StartAsync();

                var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
                await playerService.StartAsync();

                var scheduleService = scope.ServiceProvider.GetRequiredService<ScheduleService>();
                await scheduleService.StartAsync();
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://*:5000");
                });
    }
}