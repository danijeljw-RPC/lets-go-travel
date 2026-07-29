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

## Implemented Checkout and Booking Surface

All checkout routes require an authenticated active customer. Ownership comes from the validated `sub` claim, another customer's identifier returns `404 checkout_not_found`, and checkout requests accept platform offer IDs rather than supplier references or authoritative prices.

- `POST /api/v1/checkouts` creates a hotel, flight or hotel-plus-flight checkout from an owned trip and traveller assignment;
- `GET /api/v1/checkouts/{checkoutId}` returns the current owned checkout with separate payment and component-booking states;
- `POST /api/v1/checkouts/{checkoutId}/acceptance` accepts the exact current price and terms revision;
- `POST /api/v1/checkouts/{checkoutId}/payment-session` revalidates the offer and prepares provider-hosted payment;
- `POST /api/v1/checkouts/{checkoutId}/payment-return` submits one opaque browser completion reference for server-side verification;
- `POST /api/v1/checkouts/{checkoutId}/book` submits eligible component bookings; and
- `POST /api/v1/checkouts/{checkoutId}/recover` retrieves pending or unknown provider state without issuing a new charge or booking.

`Idempotency-Key` is required on checkout creation, acceptance, payment-session, payment-return and booking submission. A replay with the same request returns the durable response, while changed input under the same key returns `409 idempotency_conflict`. Recovery is retrieval-only and does not require a key.

Development uses sanitized deterministic LiteAPI fixtures and the checkout endpoint limit is 20 requests per minute per source. Production registers no payment or booking provider and returns `503 booking_capability_unavailable`; credentials alone cannot enable it. Supplier webhooks, scheduled reconciliation, immutable canonical booking versions and notifications remain Slice 5.
