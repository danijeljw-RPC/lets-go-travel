namespace ReadyToGoTravel.Web.Authentication;

// The anonymous support-ticket-create endpoint, and the connection-scoped ceiling behind every
// support-guest request, rate-limit per client IP. Blazor Server means every call this host makes
// to the API originates from this host's own connection, not the browser's, so without this
// handler every visitor sharing a Web deployment collapses into one shared bucket/ceiling. Forwards
// the browser's address as seen by this host (itself only reliable if this host is not sitting
// behind its own unconfigured reverse proxy - a separate, existing concern this handler does not
// attempt to solve) alongside a shared secret the API only trusts from this configured value, so a
// caller other than this Web host cannot spoof a fresh bucket.
internal sealed class SupportClientIpForwardingHandler(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : DelegatingHandler
{
    private const string ForwardedClientIpHeader = "X-Rtgt-Forwarded-Client-Ip";
    private const string InternalCallerSecretHeader = "X-Rtgt-Internal-Caller-Secret";

    private readonly string? internalCallerSecret = configuration["Support:InternalCallerSecret"];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clientIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        if (clientIp is not null && !string.IsNullOrEmpty(internalCallerSecret))
        {
            request.Headers.TryAddWithoutValidation(ForwardedClientIpHeader, clientIp.ToString());
            request.Headers.TryAddWithoutValidation(InternalCallerSecretHeader, internalCallerSecret);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
