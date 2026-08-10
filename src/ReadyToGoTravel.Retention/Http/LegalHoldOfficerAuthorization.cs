using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace ReadyToGoTravel.Retention.Http;

public sealed class LegalHoldOfficerRequirement : IAuthorizationRequirement;

/// <summary>
/// Mirrors ReadyToGoTravel.Support.Http.SupportAgentAuthorizationHandler exactly: ASP.NET Core
/// does not flatten the Keycloak realm_access claim's nested roles array, so this handler parses
/// it directly. Legal-hold administration is a distinct, more sensitive privilege than the
/// support console - a support-agent role does not grant it, and vice versa.
/// </summary>
public sealed class LegalHoldOfficerAuthorizationHandler : AuthorizationHandler<LegalHoldOfficerRequirement>
{
    private const string LegalHoldOfficerRole = "legal-hold-officer";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LegalHoldOfficerRequirement requirement)
    {
        var realmAccess = context.User.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrWhiteSpace(realmAccess) && HasLegalHoldOfficerRole(realmAccess))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasLegalHoldOfficerRole(string realmAccessJson)
    {
        try
        {
            using var document = JsonDocument.Parse(realmAccessJson);
            if (!document.RootElement.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var role in roles.EnumerateArray())
            {
                if (role.ValueKind == JsonValueKind.String &&
                    string.Equals(role.GetString(), LegalHoldOfficerRole, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
