# Working Assumptions

This register distinguishes accepted decision baselines from discovery assumptions. Implementation may rely on accepted ADRs but must not silently resolve remaining proposed decisions or open issues.

| Assumption | State | Decision path |
| --- | --- | --- |
| Product is standalone and consumer-focused. | Accepted | ADR-0001 |
| Web is the first client and mobile follows later. | Working assumption | OI-0001 |
| .NET 10 and ASP.NET Core Web API form the application baseline. | Accepted | ADR-0006 |
| Azure Database for PostgreSQL stores product and booking state. | Accepted | ADR-0004, ADR-0006 |
| Keycloak handles customer authentication and token issuance. | Accepted | ADR-0003 |
| Clients call only the platform API. | Accepted | ADR-0002 |
| LiteAPI/Nuitee Connect is the initial supplier candidate. | Working assumption | OI-0002 through OI-0006 |
| Duffel is accommodated as a later supplier but its customer search and booking capabilities are disabled by default for the initial launch. | Accepted | ADR-0007 |
| The platform does not handle raw cardholder data and selects customer collection separately from supplier settlement. | Accepted | ADR-0007 |
| Booking current state and immutable versions are separate. | Accepted | ADR-0004 |
| Reconciliation and live operational flight status are separate. | Accepted | ADR-0005 |
| A containerised modular monolith with one private general-purpose worker is the initial application topology. | Accepted | ADR-0006 |
