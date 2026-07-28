<!-- markdownlint-disable MD013 -->

# API Principles

## Resource Model

Use customer-facing platform resources such as trips, travellers, offers, bookings, payments, cancellations, documents and notifications. Durable resources receive platform identifiers; supplier references remain internal except where a customer-facing confirmation reference is useful.

Internal mappings identify supplier, account/environment, entity type, external identifier, booking or confirmation reference, and first/last-seen timestamps. One platform booking may contain multiple external references without exposing provider selection or raw supplier structures in the public contract.

## Client Parity

Web is first but not privileged. Supported customer operations and material business rules remain server-side so later mobile clients use the same product behaviour.

## Compatibility

The versioning mechanism, deprecation window and supported mobile release window remain open. Prefer additive changes and stable errors; do not use versioning to normalise routine breaking changes.

## Rate Limiting

Apply limits by unauthenticated source, customer, client, endpoint category, supplier quota and concurrency where justified. Search limits follow supplier quotas and look-to-book terms. Booking/payment endpoints use conservative limits plus idempotency.

## Idempotency

Prebook where supported, payment-session creation, booking confirmation, cancellation, refund, material document generation and notification-event creation need defined key ownership, lifetime, request fingerprint, repeated response, conflicting reuse, supplier-key mapping and crash recovery. Throttling is not a substitute for idempotency.

## Errors

Return a stable platform error contract covering authentication, authorisation, validation, rate limit, offer expiry, price/availability change, supplier unavailable, payment incomplete, booking pending/failed, duplicate request, cancellation not permitted and retry guidance. Never send raw supplier error bodies to clients.

## Traceability

Propagate a correlation ID across client request, API, database audit, background work, supplier call, webhook and notification. Retain supplier request IDs where available.

## Client Boundary

Clients authenticate, present input, display platform results, manage local UI state, cache only approved offline data, refresh tokens, follow retry guidance, submit idempotency keys and receive notifications. They do not calculate authoritative totals, decide discount eligibility or booking success, store supplier credentials, infer confirmation from a payment screen, reconcile suppliers or determine data ownership.
