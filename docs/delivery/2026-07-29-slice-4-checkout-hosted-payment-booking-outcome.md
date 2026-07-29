<!-- markdownlint-disable MD013 -->

# Slice 4 Outcome — Checkout, Hosted Payment and Booking

## Outcome

Slice 4 is implemented on branch `codex/slice-4-checkout-booking` for a pull request into `dev`. It delivers authenticated server-resolved checkout, renewed price acceptance, provider-hosted payment isolation, durable idempotency, separately evidenced hotel and flight component bookings, immediate retrieval-based recovery, a PostgreSQL Booking schema and an interactive Blazor checkout experience. The pull-request URL is assigned only after independent task and whole-branch review, push and `gh pr create`; this report is the branch's authoritative delivery record.

The completed implementation follows the [Checkout, Hosted Payment and Booking Implementation Plan](../superpowers/plans/2026-07-29-checkout-hosted-payment-booking.md) and advances [PLAN-0002](../plans/active/PLAN-0002-mvp-delivery.md) to Slice 5.

## Delivered Scope and Ownership

- Added `ReadyToGoTravel.Booking` as one internal feature-module assembly and no new deployable; the API, web, general worker and flight-reconciliation worker remain the four processes.
- Booking exclusively owns checkout sessions/revisions, price acceptance, booking-time traveller snapshots, payment attempts, component bookings, recovery cases and external-operation idempotency records in PostgreSQL schema `booking`.
- Consumer exposes only an authenticated owned-trip and low-risk traveller application contract. Search exposes only server-side opaque-offer resolution. Booking never queries Consumer or Search tables.
- The deterministic initial Booking migration is `20260729030000_InitialBookingSchema`. It creates only Booking tables with module-local foreign keys, `numeric(18,2)` money, bounded enum strings and unique idempotency, provider-return, provider-booking and recovery indexes.

## Public API and Web Flow

Seven authenticated routes under `/api/v1/checkouts` create, read, accept, prepare hosted payment, verify payment return, submit booking and recover a checkout. Checkout creation, acceptance, payment-session, payment-return and booking submission require `Idempotency-Key`; recovery retrieves durable provider state without issuing a new external effect.

The Blazor `/checkout` page consumes only the public API. Search selection passes at most one hotel and one flight platform offer ID. The customer chooses an owned trip and travellers, reviews the server-authoritative revision, accepts it, launches a provider-scoped JavaScript wrapper and submits one opaque browser completion reference. The wrapper receives only a DOM element ID and short-lived browser token; it contains no custom card field, PAN/CVV handling, supplier credential or production provider script.

## Revalidation and Renewed Acceptance

Every selected offer is resolved on the server at checkout creation and immediately before payment preparation. A changed price, currency, material term, product detail, provider revision/binding or expiry clears acceptance and returns `price_acceptance_required`; payment cannot continue until the customer accepts the exact current revision.

The live Development smoke observed the QF sandbox search total of AUD 289.40 become an authoritative checkout total of AUD 309.40. The customer accepted the checkout revision before payment. Repeated resolution of an unchanged sanitized fixture now preserves its original provider expiry, so unchanged revalidation does not manufacture a false reprice.

## Hosted Payment and PCI Boundary

Payment is provider hosted and the server treats browser completion as non-authoritative. It retrieves/verifies provider state before updating durable payment state. Fixture payment, browser and return references are opaque and deterministic per provider operation, so separate checkouts do not collide while same-intent replay converges.

Raw PAN, CVV, sensitive authentication data, supplier credentials, reusable payment tokens and full identity-document values are absent from API contracts, ordinary Booking storage and the web wrapper. OI-0002 and OI-0006 remain open; Slice 4 does not claim merchant-of-record allocation, production payment approval or a completed PCI assessment.

## Separate Payment and Component States

Checkout, payment and each component booking retain independent states. Only affirmative supplier confirmation with an external reference produces `Confirmed`; captured payment alone never produces a booking confirmation. A combined journey is `Completed` only when both hotel and flight components are confirmed. Partial final failure retains the confirmed component, marks the affected component/refund outcome and moves the checkout to `RequiresSupport` rather than presenting a false combined confirmation.

Fixture provider booking references are deterministic per idempotent provider command and remain retrievable. This preserves the database-wide uniqueness constraint without losing duplicate-command convergence or pending recovery.

## Journey and Recovery Results

The fresh PostgreSQL Development smoke produced:

- hotel checkout: `Completed` with confirmed hotel component;
- flight checkout: `BookingPending`, matching the deliberate QF pending fixture;
- combined checkout: `BookingPending`, with hotel confirmation and QF pending state shown independently;
- duplicate hotel payment return: converged to the existing `Completed` checkout without another payment attempt;
- pending flight recovery: remained safely `BookingPending` with no duplicate booking and no recovery case while the provider still reported pending; and
- Production checkout creation: HTTP `503` with `booking_capability_unavailable` when no provider was registered.

Unit and in-process API tests additionally cover confirmed flight and combined happy paths, captured-payment/failed-booking support and refund outcomes, pending-to-confirmed retrieval, unknown-to-one-recovery-case convergence, ownership isolation, idempotency conflicts and the 20-per-minute checkout limiter.

## Test-first Evidence

Tasks 1 through 5 recorded observed RED/GREEN cycles for the Consumer boundary, Booking domain, persistence/idempotency, fixture providers, API orchestration and Blazor boundary. Fresh live verification exposed three fixture-only integration defects and each received a focused RED before its minimum fix:

- unchanged fixture revalidation changed expiry by one second and invalidated acceptance; the regression first observed expected `00:20:00` versus actual `00:20:01`;
- separate component bookings returned the same provider booking reference; the regression first observed equal `book_flight_pending` references and PostgreSQL had reported unique-index violation `23505`; and
- separate hosted sessions returned the same provider payment reference; the regression first observed equal `pay_action_required` references and PostgreSQL had reported a duplicate provider-return reference.

The focused GREEN checks passed, followed by Search 31/31 and Booking 65/65.

## Verification Record

Fresh completion checks on 2026-07-29 produced:

- locked solution restore: passed;
- solution formatter verification: passed after normalizing the generated migration and the inherited Slice 3 import ordering;
- warning-as-error Release build: passed with zero warnings and zero errors;
- full solution test run before the live-smoke regressions: 146/146 passed; final post-review verification records 152/152 below in the implementation plan execution record;
- repository documentation structure/link validation: passed for all 106 files after documentation completion;
- Consumer and Booking migrations: applied successfully to a fresh PostgreSQL 17.10 database, with both migration IDs and expected `consumer`/`booking` schemas inspected;
- Development live smoke: hotel, flight, combined, repricing, duplicate-return and pending-recovery paths completed with the states recorded above;
- Production live smoke: HTTP `503 booking_capability_unavailable`;
- container builds: API, web, general worker and flight-reconciliation worker all passed; and
- vulnerable-package audit: not executed because the approval boundary rejected transmitting repository package metadata to the external advisory service; no workaround was attempted.

The raw `markdownlint-cli2 "docs/**/*.md"` command reports 345 pre-existing violations because the repository has no markdownlint configuration matching its established long-line/table conventions. Repository `scripts/validate-docs.sh` is the authoritative passing docs check; changed Slice 4 documents are also linted separately with their explicit MD013 convention.

## Commit Sequence and Pull-request Reference

- `9b0b4ef` — design checkout and booking.
- `3809a40` — plan checkout and booking.
- `6554a7d` and `ce55915` — add the domain and harden payment/repricing transitions.
- `bea65b7`, `ef3b1e5` and `31c1ef1` — add persistence/idempotency and harden concurrency.
- `c320fcc` and `3500aa1` — add and capability-gate sandbox checkout providers.
- `c7dc367`, `04b306a` and `0f68fbf` — expose and harden the authenticated checkout API.
- `c117ed7` and `ff05439` — add and harden the hosted checkout experience.
- `c4b1a51` — add the deterministic migration, live-smoke fixes, public/operational documentation, verification record and this outcome report.

Branch: `codex/slice-4-checkout-booking`. Pull-request base: `dev`. The controller creates the pull request with `gh` after review and does not merge it as part of Slice 4 implementation.

## Production Gates and Explicit Exclusions

OI-0002 commercial/merchant allocation, OI-0003 Australian carrier booking entitlement and OI-0006 hosted-payment/PCI evidence remain open and authoritative. Production registers no search/payment/booking fixtures or live providers; a secret alone cannot activate them. No live LiteAPI, Stripe or Duffel customer route is enabled.

Slice 4 does not implement cancellation/refund execution, ticket servicing, supplier webhooks, scheduled reconciliation, immutable canonical booking versions, customer notifications, first-party support tickets, native mobile payment, custom card forms, stored value, insurance, loyalty, sharing, AI planning or enterprise/TMC behavior.

## Slice 5 Handoff

Slice 5 should add authenticated at-least-once webhook ingress, durable inbox/outbox processing, scheduled retrieval fallback, immutable canonical booking versions and customer notifications. It must preserve Slice 4 idempotency, separate payment/component evidence and fail-closed provider capability gates. OI-0005 remains the production webhook evidence gate; Slice 5 implementation must not treat sandbox delivery behavior as production proof.
