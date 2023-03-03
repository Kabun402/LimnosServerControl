using Microsoft.Extensions.Primitives;
using System.Text.RegularExpressions;

namespace LimnosServerControl.Services
{
    public class AuthService
    {
        private string? token;

        public AuthService(IConfiguration configuration)
        {
            validateConfigToken(configuration);
        }

        private void validateConfigToken(IConfiguration configuration)
        {
            try
            {
                token = configuration["LSCAuthToken"];
                if (token is null)
                    throw new Exception();

                if (!Regex.IsMatch(token, "^[a-zA-Z0-9äöüÄÖÜ!?%(){}]{64,}$"))
                    throw new Exception();
            }
            catch
            {
                throw new Exception("LSCAuthToken not validated");
            }
        }

        public bool IsAuhorized(HttpRequest request)
        {
            StringValues headerVal;
            if (!request.Headers.TryGetValue("LSCAuthToken", out headerVal))
                return false;

            if (headerVal.Count() != 1)
                return false;

            var providedToken = headerVal.Single();
            if (providedToken is null || providedToken != token)
                return false;

            return true;
        }
    }
}
