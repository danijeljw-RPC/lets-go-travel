using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ReadyToGoTravel.Support.Tests;

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "Test";
    public const string SubjectHeader = "X-Test-Subject";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var subject = Request.Headers[SubjectHeader].ToString();
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("sub", subject),
            new("email", $"{subject}@example.test"),
        };

        var roles = Request.Headers[RolesHeader].ToString();
        if (!string.IsNullOrWhiteSpace(roles))
        {
            var roleList = string.Join(',', roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(role => $"\"{role.Trim()}\""));
            claims.Add(new Claim("realm_access", $"{{\"roles\":[{roleList}]}}"));
        }

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme)));
    }
}
