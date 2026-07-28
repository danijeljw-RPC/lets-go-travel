<!-- markdownlint-disable MD013 -->

# readytogo.travel

`readytogo.travel` is a standalone consumer travel product centred on customer-owned trips, travellers and bookings. The product shortcode is `RTGT`, and `ReadyToGoTravel` is the default root namespace/package prefix where ecosystem conventions permit it.

The repository contains the .NET 10 MVP foundation and its canonical product documentation. Start with the [documentation index](docs/README.md) for product direction, accepted decisions, remaining production reviews and active delivery plans.

## Application Foundation

The solution currently produces four independently deployable processes:

- `ReadyToGoTravel.Api` — versioned ASP.NET Core Web API, health checks and OpenAPI.
- `ReadyToGoTravel.Web` — Blazor SSR customer web host that consumes the public API.
- `ReadyToGoTravel.Worker` — general durable background-work host.
- `ReadyToGoTravel.FlightReconciliation.Worker` — isolated scheduled flight reconciliation host.

Build and verify locally with:

```bash
dotnet restore ReadyToGoTravel.slnx --locked-mode
dotnet format ReadyToGoTravel.slnx --verify-no-changes --no-restore
dotnet build ReadyToGoTravel.slnx --configuration Release --no-restore
dotnet test ReadyToGoTravel.slnx --configuration Release --no-build
./scripts/validate-docs.sh
```

The application foundation and sandbox development are approved. Supplier booking, payment and webhook capabilities must remain disabled in production until the corresponding gates in the [review register](docs/decisions/review-register.md) are approved.

Package locks cover projects with explicit NuGet dependencies. The Blazor web project deliberately follows the installed SDK's implicit ASP.NET servicing assets, avoiding a machine-specific framework-asset lock while retaining locked restore for its dependencies elsewhere in the solution.

## Repository Layout

- [`docs/`](docs/README.md) — canonical product, architecture, decision and planning documentation.
- [`src/`](src/) — API, web, worker and shared .NET projects.
- [`scripts/`](scripts/) — project utilities, currently including travel-domain discovery.
- [`tests/`](tests/) — contract, architecture and shared-hosting tests.

The original numbered planning pack was consolidated into `docs/` and removed from the working tree. Its source remains available through Git history.
