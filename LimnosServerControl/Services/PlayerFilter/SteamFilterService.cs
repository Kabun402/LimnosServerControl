namespace LimnosServerControl.Services.PlayerFilter
{
    public class SteamFilterService
    {
        private IConfiguration configuration;

        public SteamFilterService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }
    }
}
