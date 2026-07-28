<!-- markdownlint-disable MD013 -->

# System Context

## Actors and Systems

| Actor or system | Responsibility |
| --- | --- |
| Customer | Uses web and later mobile clients to manage trips and bookings. |
| Web/mobile client | Presents platform data, authenticates with Keycloak and calls the platform API. |
| Platform API | Enforces ownership, business rules, pricing, idempotency and supplier orchestration. |
| Keycloak | Owns credentials, authentication flows, sessions and token issuance. |
| PostgreSQL | Stores platform customer, trip, booking, pricing, reconciliation and audit records. |
| LiteAPI/Nuitee Connect | Initial supplier candidate for inventory, payment and fulfilment. |
| Notification provider | Delivers email and later push/SMS messages. |
| Operational flight-status provider | Optional future source for live operational data, separate from booking fulfilment. |

## Primary Flow

The customer authenticates with Keycloak. The client sends the resulting access token to the platform API. The platform authorises platform resources, reads or writes PostgreSQL, calls suppliers using backend credentials, and returns supplier-neutral platform models.

## Boundary Rule

Clients never receive supplier API keys and never treat a supplier booking response as the platform contract.
