using Microsoft.AspNetCore.Authentication;

namespace MultiplayerARPG.MMO
{
    public class AppSecretAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {
        public string AppSecret { get; set; } = string.Empty;
    }
}
