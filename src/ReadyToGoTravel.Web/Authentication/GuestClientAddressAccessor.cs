using System.Net;

namespace ReadyToGoTravel.Web.Authentication;

// IHttpContextAccessor.HttpContext is only valid while an actual HTTP request is executing. For an
// Interactive Server (Blazor Server) circuit, that is true for the prerender pass only - a circuit
// gets its own DI scope, independent of whichever HTTP request established its SignalR connection,
// so nothing captured into a Scoped service during that request is visible to the circuit. Every
// later event raised over the already-established connection (which is most of a guest's activity
// on a ticket page: replies, uploads, downloads, refreshes) therefore has no HttpContext at all.
// This holder is populated instead via PersistentComponentState, carrying the address captured
// during prerender into the fresh circuit that follows it - see SupportGuestTicket.razor.

public sealed class GuestClientAddressAccessor
{
    public IPAddress? ClientAddress { get; private set; }

    public void Capture(IPAddress? address) => ClientAddress ??= address;
}
