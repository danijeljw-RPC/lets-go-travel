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
- `GET /api/v1/checkouts/{checkoutId}/history` returns the owned supplier-neutral component version timeline and stable diffs without raw or provider references;
- `POST /api/v1/checkouts/{checkoutId}/acceptance` accepts the exact current price and terms revision;
- `POST /api/v1/checkouts/{checkoutId}/payment-session` revalidates the offer and prepares provider-hosted payment;
- `POST /api/v1/checkouts/{checkoutId}/payment-return` submits one opaque browser completion reference for server-side verification;
- `POST /api/v1/checkouts/{checkoutId}/book` submits eligible component bookings; and
- `POST /api/v1/checkouts/{checkoutId}/recover` retrieves pending or unknown provider state without issuing a new charge or booking.

`Idempotency-Key` is required on checkout creation, acceptance, payment-session, payment-return and booking submission. A replay with the same request returns the durable response, while changed input under the same key returns `409 idempotency_conflict`. Recovery is retrieval-only and does not require a key.

Development uses sanitized deterministic LiteAPI fixtures and the checkout endpoint limit is 20 requests per minute per source. Fixture searches issue cryptographically random opaque offer handles held only in the current server process with immutable expiries. A server restart invalidates outstanding sandbox handles, which then fail closed as `checkout_offer_not_found`; a new search issues new handles. Production registers no payment or booking provider and returns `503 booking_capability_unavailable`; credentials alone cannot enable it.

## Implemented Webhook and Reconciliation Surface

`POST /api/v1/webhooks/liteapi/{environment}` is a provider callback route outside consumer JWT authentication. It accepts only configured `sandbox` or `production` envelopes, requires the configured current or previous shared secret in `Authorization`, enforces JSON and bounded-body validation, and returns `202` only after durable insert or identical-duplicate confirmation. The route is disabled by default in every checked-in configuration. A reused `LiteAPI + environment + event_id` identity with a different body returns `409` and quarantines the record.

Inbox processing never trusts the event payload as booking truth. Known lifecycle events enqueue supplier retrieval; unsupported or malformed events become inspectable cases. The general worker handles the inbox, hotel schedules and notification outbox, while the dedicated flight worker handles flight schedules. Active hotels and flights outside their final 24 hours reconcile daily; flights inside the final 24 hours reconcile hourly. Terminal outcomes stop future work. Retrieval changes append supplier-neutral versions and update current component state in one transaction; unchanged hashes add no version.

Customer history exposes only version number, observation/effective time, source, canonicalisation version, severity, flags and a provider-neutral diff. It does not expose raw webhook evidence, canonical snapshots, provider binding/reference, notification destination or internal errors.

Public state values are case-sensitive and returned exactly as follows:

- `CheckoutStatus`: `AwaitingAcceptance`, `ReadyForPayment`, `PaymentPending`, `BookingPending`, `Completed`, `Failed`, `RequiresSupport`, `Expired`;
- `PaymentStatus`: `NotStarted`, `ActionRequired`, `Processing`, `Authorised`, `Captured`, `Failed`, `OutcomeUnknown`, `RefundRequired`; and
- `ComponentBookingStatus`: `OfferSelected`, `PaymentPending`, `BookingPending`, `Confirmed`, `Failed`, `RefundRequired`, `RequiresSupport`, `Cancelled`, `Completed`.

## Implemented Support Surface

`POST /api/v1/support/tickets` accepts an optional consumer JWT. An authenticated caller's contact email always comes from the validated `sub`/`email` claims and cannot be overridden by the request body; an anonymous caller supplies a name and email and receives no bearer token in the response. Every accepted ticket queues a durable acknowledgement notification; a guest ticket's acknowledgement carries a separate high-entropy magic-link token that is never returned by any API response.

Authenticated consumer routes (`RequireAuthorization("consumer")`, ownership from `sub`, another customer's ticket returns `404 ticket_not_found`):

- `GET /api/v1/support/tickets` lists the caller's own tickets;
- `GET /api/v1/support/tickets/{ticketId}` returns the owned ticket, its immutable message thread and attachment metadata;
- `POST /api/v1/support/tickets/{ticketId}/messages` appends an owned reply and applies the ticket state transition; and
- `POST /api/v1/support/tickets/{ticketId}/attachments` and `GET .../attachments/{attachmentId}/download` upload and retrieve an owned attachment.

Guest routes (`/api/v1/support/guest/ticket...`) carry no `ticketId` route or query parameter. The caller presents the magic-link token as `Authorization: Bearer <token>`; the server resolves the token to its ticket by hashing and looking up the stored hash, so the token is the only input that can ever select a ticket. A missing, malformed, expired or revoked token returns `401 guest_link_invalid` without distinguishing the reason. Guest tokens are multi-use until they expire (30 days from issuance) or are explicitly revoked or rotated; rotation and revocation are staff-only and immediately invalidate the prior token.

Staff routes (`/api/v1/support/staff/tickets...`) require the `support-agent` policy, backed by the Keycloak `realm_access` role claim. Staff can list and view any ticket, reply, close a ticket, and rotate or revoke its guest link. No staff route returns a raw guest token.

Attachments are private S3-compatible objects addressed by a server-generated key that never derives from the client-supplied filename. Uploads are limited to 10 MiB per file, 5 files per message and 50 MiB per ticket, and only PDF, JPEG, PNG and UTF-8 plain text pass both declared-type and byte-signature validation. An attachment is downloadable only after an explicit `Clean` malware-scan result; downloads return a presigned URL valid for at most five minutes rather than a public object URL. Production registers no object-storage or malware-scanning backend by default, so attachments remain quarantined until that capability is activated.

Public state values are case-sensitive and returned exactly as follows:

- `SupportTicketStatus`: `New`, `WaitingOnSupport`, `WaitingOnCustomer`, `Closed`;
- `SupportTicketCategory`: `General`, `TravelWithin24Hours`, `PaymentBookingMismatch`, `SupplierCancellationOrRelocation`, `TravellerSafety`, `AccountOrOther`;
- `SupportAuthorType` (thread message author): `Customer`, `Guest`, `Support`, `System`; and
- `AttachmentScanStatus`: `Pending`, `Clean`, `Infected`, `Failed`.
