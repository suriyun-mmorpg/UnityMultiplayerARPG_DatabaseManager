using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace MultiplayerARPG.MMO
{
    public class AppSecretAuthenticationHandler : AuthenticationHandler<AppSecretAuthenticationSchemeOptions>
    {
        public const string SCHEME = "APP_SECRET";

        public AppSecretAuthenticationHandler(IOptionsMonitor<AppSecretAuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            await Task.Yield();
            AuthenticationHeaderValue authHeader;
            try
            {
                authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
            }
            catch (FormatException ex)
            {
                return AuthenticateResult.Fail($"Wrong authentication header format: {ex.Message}");
            }

            if (string.IsNullOrEmpty(authHeader.Parameter) || authHeader.Parameter != Options.AppSecret)
            {
                return AuthenticateResult.Fail("Wrong app secret");
            }

            Claim[] claims = new Claim[] { new Claim(ClaimTypes.Name, authHeader.Parameter) };
            ClaimsIdentity identity = new ClaimsIdentity(claims, Scheme.Name);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            AuthenticationTicket ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
    }
}
