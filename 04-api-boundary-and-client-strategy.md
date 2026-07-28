# API Boundary and Client Strategy

## Public API position

The platform Web API is the product contract.

Every first-party client should use the same application API unless a clearly justified client-specific gateway is introduced later.

The API must hide:

- LiteAPI authentication;
- supplier identifiers where they are not part of a customer-facing reference;
- supplier response structures;
- supplier-specific error formats;
- pricing internals not intended for the customer;
- provider-selection logic;
- retry and reconciliation implementation;
- internal audit data.

## Supplier-neutral resource model

Public API resources should use platform concepts such as:

- Trip;
- Traveller;
- Accommodation;
- Flight;
- Offer;
- Booking;
- PaymentSession;
- Cancellation;
- Document;
- Notification.

Avoid exposing resources named after LiteAPI or another supplier.

## Platform identifiers

Each durable platform entity should receive its own identifier.

Supplier mappings should be internal and support:

- supplier type;
- supplier account or environment;
- supplier entity type;
- supplier identifier;
- supplier booking reference;
- carrier or hotel confirmation reference;
- creation and last-seen timestamps.

The same platform booking may eventually contain more than one external reference.

## Web and mobile parity

The API should be designed so that:

- the web client is not privileged merely because it was built first;
- mobile clients can perform supported customer operations directly;
- all material business rules remain server-side;
- client releases do not need supplier SDK updates for ordinary supplier changes;
- response contracts remain compatible across a mobile release window.

## API evolution

API evolution should be planned from the beginning.

The repository's existing API versioning convention should be used. The planning phase should decide:

- URL, header, or media-type versioning;
- backward-compatibility window;
- deprecation policy;
- mobile minimum-supported version;
- additive change rules;
- removal process;
- error-contract stability.

Versioning should not be used as an excuse for frequent breaking changes.

## Rate limiting

Rate limiting should be layered.

Potential dimensions include:

- unauthenticated IP;
- authenticated customer;
- client application;
- endpoint category;
- supplier;
- supplier quota;
- concurrent requests;
- commercial plan if partners are added.

Search endpoints will usually need tighter control than cached reference endpoints. Booking, payment, and cancellation endpoints should use low limits plus idempotency rather than relying only on throttling.

Limits should be based on measured supplier quotas and look-to-book obligations, not arbitrary example numbers.

## Idempotency

Operations with financial or reservation effects require idempotency.

At minimum, consider it for:

- prebook initiation where supplier behaviour permits;
- booking confirmation;
- payment-session creation;
- cancellation request;
- refund request;
- document generation where duplicates matter;
- notification event creation.

The platform must define:

- key ownership;
- key lifetime;
- request fingerprinting;
- repeated-response behaviour;
- conflict behaviour when a key is reused with different content;
- supplier idempotency mapping;
- crash recovery.

## Correlation and traceability

Every request should have a correlation identifier propagated through:

- client request;
- API logs;
- application commands;
- database audit;
- background messages;
- LiteAPI requests;
- webhook processing;
- notification dispatch.

Supplier request IDs and booking references should be retained where available.

## Error model

Clients should receive a stable platform error contract.

It should distinguish at least:

- authentication failure;
- authorisation failure;
- validation failure;
- rate limit;
- offer expired;
- price changed;
- availability changed;
- supplier unavailable;
- booking pending;
- booking failed;
- payment incomplete;
- duplicate request;
- cancellation not permitted;
- retryable network failure;
- internal failure.

Raw supplier error bodies should not be sent directly to clients.

## Client responsibilities

Clients should:

- authenticate;
- present inputs;
- display platform results;
- manage local UI state;
- safely cache approved offline data;
- handle token refresh;
- handle retry guidance returned by the API;
- submit idempotency keys where required;
- receive notifications.

Clients should not:

- calculate authoritative totals;
- decide discount eligibility;
- decide whether a booking is confirmed;
- store supplier credentials;
- infer booking success from payment UI alone;
- directly reconcile supplier records;
- determine data ownership.
