# API Principles

## Resource Model

Use customer-facing platform resources such as trips, travellers, offers, bookings, payments, cancellations, documents and notifications. Durable resources receive platform identifiers; supplier references remain internal except where a customer-facing confirmation reference is useful.

## Client Parity

Web is first but not privileged. Supported customer operations and material business rules remain server-side so later mobile clients use the same product behaviour.

## Compatibility

The versioning mechanism, deprecation window and supported mobile release window remain open. Prefer additive changes and stable errors; do not use versioning to normalise routine breaking changes.

## Rate Limiting

Apply limits by unauthenticated source, customer, client, endpoint category, supplier quota and concurrency where justified. Search limits follow supplier quotas and look-to-book terms. Booking/payment endpoints use conservative limits plus idempotency.

## Errors

Return a stable platform error contract covering authentication, authorisation, validation, rate limit, offer expiry, price/availability change, supplier unavailable, payment incomplete, booking pending/failed, duplicate request, cancellation not permitted and retry guidance. Never send raw supplier error bodies to clients.

## Traceability

Propagate a correlation ID across client request, API, database audit, background work, supplier call, webhook and notification. Retain supplier request IDs where available.
