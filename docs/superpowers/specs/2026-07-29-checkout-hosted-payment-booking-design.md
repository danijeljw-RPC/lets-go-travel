<!-- markdownlint-disable MD013 -->

# Checkout, Hosted Payment and Booking Design

## Status

Approved by the product owner on 2026-07-29 for implementation as Slice 4 of PLAN-0002. Production payment and booking capabilities remain disabled until the existing commercial, supplier, carrier and PCI evidence gates are approved.

## Objective

Deliver an authenticated, supplier-neutral checkout and booking slice for hotel-only, flight-only and combined hotel-plus-flight journeys. The slice must revalidate every selected offer on the server, preserve separate payment and booking outcomes, prevent browser replay from duplicating external effects and demonstrate the approved LiteAPI hosted-payment boundary without claiming or enabling production capability.

## Approaches Considered

### Approach A — One booking module with focused internal feature areas

Create one `ReadyToGoTravel.Booking` feature-module project containing Checkout, Payments, Bookings, Persistence and LiteAPI sandbox integration namespaces. The module owns one PostgreSQL schema and exposes only service registration, endpoint mapping and deliberate application contracts.

This keeps the modular monolith small while giving financially sensitive durable state a clear owner. Checkout, payment attempts and component bookings change together in this slice and do not yet justify separate assemblies or deployables.

### Approach B — Separate projects for accommodation, flights, payments and bookings

Create the full target module list as separate assemblies now, with public contracts and separate database contexts between them.

This creates stronger compile-time boundaries but adds migrations, registrations and orchestration overhead before scaling, ownership or independent-release evidence requires it.

### Approach C — Extend the existing Search or Consumer module

Put checkout and booking behavior next to provisional offers or customer trips.

This is initially shorter but gives a high-volume replaceable search module or low-risk consumer-profile module ownership of durable financial and supplier state. It conflicts with the accepted module boundaries.

## Decision

Choose Approach A. `ReadyToGoTravel.Booking` is one feature module with clear internal feature areas and one module-owned database context. It adds no deployable. Accommodation and flight component types remain distinct inside the module; they are not forced into one universal detail object.

The module may later split only when measured scaling, security, availability, ownership or independent-release needs justify extraction. Any extraction must preserve exclusive data ownership and versioned contracts.

## Module and Data Ownership

The Booking module owns:

- checkout sessions and revisions;
- customer price-acceptance records;
- booking-time traveller snapshots;
- payment attempts and opaque provider references;
- hotel and flight component bookings;
- external-operation idempotency records; and
- booking recovery cases created by an ambiguous or unsafe outcome.

Entity Framework Core mappings use a `booking` PostgreSQL schema. The API uses the existing product database connection and registers Booking readiness through the module context. Migrations remain explicit deployment steps and never run automatically at application startup.

The Booking module never queries Consumer or Search tables. Consumer exposes a deliberate application contract that resolves the authenticated customer's active profile, confirms ownership of an active trip and returns owned low-risk traveller data needed to create an immutable booking-time snapshot. Search exposes a deliberate checkout-resolution contract that resolves an opaque offer identifier into a server-owned, provider-neutral offer revision.

The web project continues to reference no server assembly and consumes Slice 4 only through `/api/v1`.

## Checkout Session

An authenticated customer creates a checkout session for one active owned trip and one or two selected offers. Valid compositions are:

- one hotel offer;
- one flight offer; or
- one hotel offer plus one flight offer.

Duplicate product components and more than two components are rejected. A combined journey is a platform trip containing separately evidenced component bookings; it is not represented as an atomic supplier package.

Each checkout session contains a UUIDv7 platform ID, owning customer and trip IDs, environment, status, current revision number, created/updated/expiry timestamps and its component revisions. A component revision contains product type, platform offer ID, internal provider binding, server-resolved product detail, price components, transaction currency, terms summary, provider revision, offer expiry and resolution timestamp. Supplier references remain internal and never become public identifiers.

Checkout status is one of:

- `AwaitingAcceptance` — the current server-resolved price and terms require customer acceptance;
- `ReadyForPayment` — the customer accepted the exact current revision;
- `PaymentPending` — the provider requires or is processing customer action;
- `BookingPending` — payment permits booking but at least one supplier outcome is not final;
- `Completed` — every component booking is supplier-confirmed;
- `Failed` — a known final failure occurred before any unresolved external outcome remains;
- `RequiresSupport` — automatic recovery cannot safely determine or complete the next action; or
- `Expired` — no external operation may begin because the checkout lifetime elapsed.

Only the current revision can be accepted. Acceptance records the revision, accepted total and currency, terms hash, Booking-owned checkout-policy version and UTC timestamp. The API compares all client-returned acceptance fields with current server-authoritative values; a stale, altered or unknown-policy acceptance cannot start payment.

Unconfirmed checkout state follows the approved 30-day retention class. Slice 7 implements expiry enforcement and deletion receipts; Slice 4 records the timestamps and classifications needed by that later work.

## Offer Resolution and Repricing

The browser submits only platform offer identifiers. The API does not accept supplier references, provider names, authoritative prices or booking status from the client.

At checkout creation and immediately before payment-session creation, Booking calls Search's checkout-resolution contract. The contract:

- resolves only offers produced by the configured environment provider;
- checks the matching market, product, operation and carrier capability;
- returns a fresh provider-neutral price and terms revision;
- fails closed when the offer is unknown, expired or not bookable; and
- keeps provider payloads and supplier references private to the server modules.

If price, currency, material terms or bookable product detail differs from the last accepted revision, the checkout returns to `AwaitingAcceptance`. The API returns the new revision and `price_acceptance_required`; it does not silently continue payment or booking. An unchanged revision preserves the prior acceptance only when its terms hash and accepted amount still match.

The sanitized sandbox resolver includes deterministic unchanged and repriced scenarios. Production resolution remains unavailable because the production prebook and booking capabilities are disabled.

## Traveller Snapshots

Checkout assigns one or more owned saved-traveller IDs to each component and supplies any booking-only fields required for that component. The Consumer contract verifies ownership and returns low-risk saved data; Booking then creates its own immutable snapshot so later profile edits or deletion cannot rewrite submitted booking evidence.

The snapshot records platform traveller ID, supplied name, minor status, guardian-authority evidence where applicable and the minimum booking-only personal fields used for the supplier operation. Date of birth and identity-document fields are never copied into the reusable Consumer profile. Full identity-document values are excluded from logs, general support cases and canonical long-term history.

The sandbox flow requires names and age/minor context but no passport number. Production field requirements remain capability-driven and cannot be activated until field protection, masking, access, audit, key management and supplier evidence are approved. Saving booking-only sensitive values for future use is absent from Slice 4.

## Payment Boundary

`IPaymentService` is the Booking application-facing orchestrator. It coordinates:

- `ICustomerPaymentProvider`, which prepares hosted customer checkout and verifies/retrieves payment state; and
- `ISupplierSettlementProvider`, which supplies the opaque settlement instruction required by the booking provider.

A `PaymentPlan` records provider, merchant model as `SupplierOrProviderManaged`, customer-payment route, settlement route, amount, currency, required customer action, refund owner and whether separate component charges occur. Slice 4 does not model `readytogo.travel` as merchant of record and does not add Stripe, Duffel Balance, stored value, credit, remittance or platform FX.

Payment state is one of `NotStarted`, `ActionRequired`, `Processing`, `Authorised`, `Captured`, `Failed`, `OutcomeUnknown` or `RefundRequired`. Booking state is never inferred from payment state.

The LiteAPI hosted-payment adapter is available only in Development, Testing or Sandbox with sanitized deterministic behavior. It creates an opaque payment-session reference and a short-lived browser token intended for the hosted component. Raw PAN, CVV, sensitive authentication data, supplier credentials and reusable payment tokens never enter an API request, application log or Booking table.

Production configuration registers no customer-payment or booking provider and fails closed with `booking_capability_unavailable`. Adding a secret alone cannot enable production behavior.

## Blazor Hosted-Payment Wrapper

The checkout page is the only Slice 4 page that requires interactive rendering. A small JavaScript module owns provider component creation, completion and disposal. Its .NET wrapper passes only the provider-scoped public session token and a component element identifier; it receives only an opaque completion or abandonment result.

The JavaScript module contains no platform API credential, supplier API key, card field or custom payment form. Development uses a local fixture implementation with the same wrapper contract and no third-party script. The production script URL and activation flag are absent or disabled until OI-0002 and OI-0006 evidence is approved.

Browser completion is not authoritative. The web client sends the opaque return reference to the API, and the server retrieves the payment state from the configured provider before changing durable state.

## Component Booking Orchestration

After verified payment permits booking, the checkout creates one durable component booking per selected offer. A component is `Hotel` or `Flight`, retains its own current state and evidence, and links to the shared trip and checkout.

Component booking state is one of:

- `OfferSelected`;
- `PaymentPending`;
- `BookingPending`;
- `Confirmed`;
- `Failed`;
- `RefundRequired`; or
- `RequiresSupport`.

Only an affirmative supplier confirmation containing the required external reference can produce `Confirmed`. Payment completion, browser return, a queued request or an unrecognized provider value cannot produce confirmation.

For a combined journey, components are attempted in deterministic order and each outcome is committed independently. If one component confirms and another fails or becomes unknown, the confirmed component remains confirmed, the checkout becomes `RequiresSupport`, and the failed or unknown component records its own recovery/refund requirement. The UI explicitly shows component outcomes and never displays the combined journey as wholly confirmed unless every component is confirmed.

The sanitized booking provider supports hotel and flight success, pending/unknown, known failure and payment-success/booking-failure scenarios. It stores only opaque fixture references and cannot run in Production.

## Idempotency and Duplicate Returns

Checkout creation, customer acceptance, payment-session creation, payment return and booking submission are idempotent commands. Commands that could create an external effect require an `Idempotency-Key` header.

The module stores customer ID, operation name, key, canonical request fingerprint, status, response status/body reference and expiry. The tuple `(customer_id, operation, key)` is unique.

- The same key and fingerprint returns the recorded response without repeating the provider call.
- The same key with a different fingerprint returns `409 idempotency_conflict`.
- A request while the first execution is unresolved returns the durable current state and does not issue another provider call.
- Provider return references are also unique, so duplicate browser returns converge on one payment attempt.

Keys and recorded responses remain valid for at least the checkout lifetime and for any longer ambiguous-operation investigation. Later retention processing owns final expiry.

## Pending and Unknown Recovery

Slice 4 provides customer-triggered and API-triggered recovery for a known checkout or booking. Recovery retrieves the payment and component booking states using their opaque provider references and applies only valid forward transitions.

A known final provider result updates the corresponding state. A still-pending result remains pending and returns retry metadata. An unknown value, provider contradiction or exhausted safe immediate lookup creates one idempotent Booking recovery case and moves the affected checkout/component to `RequiresSupport`.

The recovery case is an internal operational record, not a Slice 6 customer support ticket. It contains no full payment or identity-document data. Webhook ingress, durable scheduled retries, canonical booking versions, reconciliation workers and customer notifications remain Slice 5.

## Public API

All Slice 4 endpoints require an authenticated active customer and remain under `/api/v1`:

- `POST /checkouts` creates a hotel, flight or combined checkout from owned trip and platform offer IDs;
- `GET /checkouts/{checkoutId}` returns the owned checkout, component prices and separate payment/booking states;
- `POST /checkouts/{checkoutId}/acceptance` accepts the exact current revision;
- `POST /checkouts/{checkoutId}/payment-session` revalidates the offer and prepares hosted payment;
- `POST /checkouts/{checkoutId}/payment-return` verifies an opaque provider return and advances or recovers payment state;
- `POST /checkouts/{checkoutId}/book` idempotently submits eligible component bookings; and
- `POST /checkouts/{checkoutId}/recover` retrieves pending/unknown provider state without issuing a new charge or booking.

Owned lookup failure returns `404 checkout_not_found`, including another customer's identifier. Validation and state conflicts use existing `ProblemDetails` with stable `code`, `correlationId` and optional field errors or retry metadata. Unknown JSON members are rejected. The checkout group uses a stricter endpoint-specific rate limit than the general public API.

## Web Experience

Search results gain authenticated selection actions for a stay, a flight or one of each. The web client sends platform offer IDs to the API and never sends an authoritative total. Customers select an owned trip and travellers, review the server-resolved component totals and terms, affirm the current revision, then launch the fixture hosted-payment wrapper.

The checkout page displays sandbox status and production limitations prominently. It shows payment and each component booking separately, handles duplicate returns safely, exposes pending recovery and uses `Booking confirmed` only when every component carries supplier confirmation evidence.

The web host communicates only through the public API. It does not reference Booking, Search or Consumer server assemblies.

## Testing

- Domain tests cover checkout composition, revision acceptance, price changes, payment/booking separation, confirmation rules, combined partial outcomes and support escalation.
- Idempotency tests prove replay returns the original result, conflicting payloads return `409` and duplicate provider returns do not create another payment or booking.
- Persistence tests use SQLite to verify schema mappings, unique constraints, ownership and immutable traveller snapshots; PostgreSQL migration verification remains part of the repository completion checks.
- Adapter contract tests cover unchanged/repriced offer resolution, hosted-payment tokens, successful and ambiguous provider returns, confirmed/pending/failed hotel and flight bookings and fail-closed Production configuration.
- Authenticated in-process API tests cover ownership isolation, validation, state conflicts, stable errors, rate-limit assignment and hotel, flight and combined journeys.
- Web tests cover public-API-only consumption, wrapper input/output boundaries, renewed acceptance messaging, separate component outcomes and the absence of card fields or production scripts.
- The full repository restore, formatting, warning-as-error Release build, test suite, documentation validation, vulnerable-package audit and all four container builds remain required before completion.

Every production behavior is implemented test-first. Each test is observed failing for the missing behavior before the minimum implementation is added.

## Activation and Explicit Deferrals

Slice 4 does not enable live LiteAPI calls, production payment collection, production booking, carrier support claims, cancellation, refunds, changes, ticket servicing, webhooks, scheduled reconciliation, immutable canonical versions, notifications or customer support tickets.

OI-0002, OI-0003 and OI-0006 remain authoritative production gates. OI-0005 governs the later webhook work. Duffel and a platform-owned Stripe route remain disabled and absent from customer inventory.

No native mobile flow, custom card form, unsupported web view, package/principal commercial model, insurance, wallet, loyalty, sharing, AI planning or enterprise/TMC/tenant behavior enters this slice.

## Success Criteria

- An authenticated customer can create and complete sandbox hotel-only and flight-only journeys through server-resolved checkout, hosted-payment simulation and supplier-confirmed component booking.
- An authenticated customer can complete a combined journey while retaining independently visible component payment and booking evidence.
- A price or material-term change blocks payment until the exact new revision is accepted.
- Browser replay and duplicate returns cannot duplicate a payment session or supplier booking.
- Payment success without confirmed booking remains recoverable and never appears confirmed.
- Pending/unknown outcomes converge through safe retrieval or one internal recovery case without blind retry.
- Booking-time traveller snapshots do not create reusable sensitive traveller storage or rewrite when the Consumer profile changes.
- Production payment and booking fail closed without approved capabilities, irrespective of configured secrets.
- The solution remains the existing four deployables with one additional feature-module assembly.

## Self-review Record

- Placeholder scan: no implementation placeholder or unresolved product choice remains.
- Consistency: module ownership, provider boundaries, payment policy, traveller privacy, confirmation accuracy and combined-journey behavior match ADR-0002, ADR-0007, OI-0002, OI-0003, OI-0006 and the approved MVP delivery design.
- Scope: immediate checkout recovery belongs to Slice 4; webhook ingress, scheduled reconciliation, immutable versions and notifications remain Slice 5.
- Ambiguity: valid checkout composition, acceptance invalidation, separate states, confirmation evidence, idempotency conflicts, partial combined outcomes, support escalation and production fail-closed behavior are explicit.
