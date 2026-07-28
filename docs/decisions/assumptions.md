<!-- markdownlint-disable MD013 -->

# Working Assumptions

This register distinguishes accepted decision baselines from discovery assumptions. Implementation may rely on accepted ADRs but must not silently resolve remaining proposed decisions or open issues.

| Assumption | State | Decision path |
| --- | --- | --- |
| Product is standalone and consumer-focused. | Accepted | ADR-0001 |
| Web is the first client and mobile follows later. | Accepted | OI-0001, OI-0009 |
| .NET 10 and ASP.NET Core Web API form the application baseline. | Accepted | ADR-0006 |
| Azure Database for PostgreSQL stores product and booking state. | Accepted | ADR-0004, ADR-0006 |
| Keycloak handles customer authentication and token issuance. | Accepted | ADR-0003 |
| Clients call only the platform API. | Accepted | ADR-0002 |
| LiteAPI/Nuitee Connect is the initial supplier direction; production activation remains evidence-gated. | In review | OI-0002 through OI-0006 |
| Duffel is accommodated as a later supplier but its customer search and booking capabilities are disabled by default for the initial launch. | Accepted | ADR-0007 |
| The platform does not handle raw cardholder data and selects customer collection separately from supplier settlement. | Accepted | ADR-0007 |
| Booking current state and immutable versions are separate. | Accepted | ADR-0004 |
| Reconciliation and live operational flight status are separate. | Accepted | ADR-0005 |
| Hotel, flight and combined hotel-plus-flight journeys are initial product scope. | Accepted | OI-0001 |
| Australia is the operating focus with global access, `en-AU`/AUD defaults and selectable locale. | Accepted | OI-0007 |
| Sensitive reusable traveller data is off by default and requires granular opt-in. | Accepted | OI-0008 |
| The web application uses .NET 10 Blazor SSR and ASP.NET Core Web API. | Accepted | OI-0009, ADR-0002 |
| A containerised modular monolith uses a general worker plus a dedicated flight-reconciliation worker. | Accepted | ADR-0006, ADR-0008 |
| The initial support channel is first-party asynchronous ticket support with email updates. | Accepted | OI-0012 |
| Canonical booking/financial evidence uses a seven-year baseline; raw supplier payloads are allowlisted and short-lived; legal holds are matter-specific. | In review | OI-0011 |
