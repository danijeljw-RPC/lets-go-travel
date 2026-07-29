<!-- markdownlint-disable MD013 -->

# API Documentation

The ASP.NET Core Web API is the proposed public application boundary for web and future mobile clients. It exposes platform concepts and hides supplier credentials, contracts, errors and selection logic.

See [API Principles](api-principles.md) and [ADR-0002](../adr/accepted/ADR-0002-platform-api-and-supplier-boundary.md).

## Implemented Consumer Surface

Public discovery:

- `GET /api/v1/platform`;
- `GET /api/v1/locales`; and
- `GET /api/v1/privacy/sensitive-traveller-storage`.

Keycloak-authenticated consumers:

- `GET` and idempotent `PUT /api/v1/me`;
- owned list, create, detail and archive operations under `/api/v1/trips`; and
- owned list, create and remove operations under `/api/v1/travellers`.

Ownership comes only from the validated `sub` claim. A resource owned by another subject is indistinguishable from a missing resource. Unknown JSON properties are rejected, so booking-time date-of-birth or identity-document fields cannot leak into reusable traveller profiles.

## Implemented Search Surface

Public, capability-gated discovery:

- `GET /api/v1/search/capabilities?pointOfSale=AU`;
- `POST /api/v1/search/hotels`; and
- `POST /api/v1/search/flights`.

Search requests and results are platform-owned. Results expose opaque offer IDs, expiry, required revalidation, minimum total, returned and requested currencies, currency provenance, base amount, included taxes and included fees. They do not expose supplier references or payloads.

Development uses sanitized deterministic LiteAPI sandbox fixtures. Default production configuration contains no search provider and returns `503 search_capability_unavailable`. Qantas, Jetstar and Virgin Australia appear only as observed sandbox search capability; production booking, ticketing and servicing remain disabled.

Checkout, booking, supplier webhooks, reconciliation and support endpoints are later slices and are not implied by the search surface.
