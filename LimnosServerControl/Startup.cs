using System.Net;
using LimnosServerControl.Services;
using Microsoft.AspNetCore.HttpOverrides;

namespace LimnosServerControl
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            //Proxy Header
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.KnownProxies.Add(IPAddress.Parse("2a01:4f8:c2c:2f37::1"));
            });


            //Add Singleton Services
            services.AddSingleton<AuthService>();
            services.AddSingleton<ArmaProcessService>();
            services.AddSingleton<RConService>();
            services.AddSingleton<PlayerService>();
            services.AddSingleton<ScheduleService>();
            services.AddSingleton<BanService>();


            //Add API MVC Controllers
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseAuthentication();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
