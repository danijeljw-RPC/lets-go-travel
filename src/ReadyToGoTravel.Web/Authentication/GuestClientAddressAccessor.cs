using System.Net;

namespace ReadyToGoTravel.Web.Authentication;

// IHttpContextAccessor.HttpContext is only valid while an actual HTTP request is executing. For an
// Interactive Server (Blazor Server) circuit, that is true for the initial render but not for any
// later event raised over the already-established SignalR connection - which is most of a guest's
// activity on a ticket page (replies, uploads, downloads, refreshes). A Blazor circuit's DI scope
// is created from the scope of the HTTP request that establishes its SignalR connection, so a value
// captured into a Scoped service during that request (see the middleware in Program.cs) remains
// available for the circuit's entire lifetime even once HttpContext itself is gone.
public sealed class GuestClientAddressAccessor
{
    public IPAddress? ClientAddress { get; private set; }

    public void Capture(IPAddress? address) => ClientAddress ??= address;
}
