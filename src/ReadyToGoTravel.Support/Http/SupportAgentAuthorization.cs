using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace ReadyToGoTravel.Support.Http;

public sealed class SupportAgentRequirement : IAuthorizationRequirement;

public sealed class SupportAgentAuthorizationHandler : AuthorizationHandler<SupportAgentRequirement>
{
    private const string SupportAgentRole = "support-agent";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SupportAgentRequirement requirement)
    {
        var realmAccess = context.User.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrWhiteSpace(realmAccess) && HasSupportAgentRole(realmAccess))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasSupportAgentRole(string realmAccessJson)
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
                    string.Equals(role.GetString(), SupportAgentRole, StringComparison.Ordinal))
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
