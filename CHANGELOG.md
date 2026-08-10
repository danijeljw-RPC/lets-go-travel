# Changelog

All notable changes to this project are documented in this file.

This project is delivered as a sequence of vertical implementation slices tracked in [PLAN-0002 — MVP Delivery](docs/plans/active/PLAN-0002-mvp-delivery.md) rather than semantic version numbers, so entries below are grouped by slice. Each slice has its own detailed [outcome report](docs/delivery/README.md) with full architecture, verification and test evidence; this file summarizes what shipped and links to that record. Production supplier, payment, webhook, notification, object-storage, malware-scanning and retention-sweep capabilities remain disabled by default throughout every slice below until their [review-register](docs/decisions/review-register.md) gate is approved with real evidence — no slice claims a production activation it did not actually complete.

## Slice 7 — Retention, Privacy Operations and Production-Readiness Certification (2026-08-10)

[Outcome report](docs/delivery/2026-08-10-slice-7-retention-privacy-production-readiness-outcome.md)

### Added

- New `ReadyToGoTravel.Retention` module: an explicit record-class/policy/trigger/expiry model for the approved retention schedule, legal hold with matter scoping and an append-only, tamper-evident audit trail, and centralised deletion receipts.
- Legal-hold-aware retention sweeps, each owned by its existing module against its own schema: Booking (webhook payload bodies, notification rendered content, abandoned checkout state), Support (attachments, general and booking-related tickets, security audit records), Consumer (account-closure profile minimisation).
- A `legal-hold-officer` Keycloak realm role and a staff-only legal-hold administration API (`/api/v1/retention/legal-holds`), mirroring the Slice 6 `support-agent` staff-console pattern.
- Customer-initiated account closure (`POST /api/v1/consumer/me/close`), idempotent, with minimisation gated on every module confirming no protected evidence remains.
- A code-verifiable production-readiness catalog and a matching hand-authored [Production-Readiness Certification](docs/operations/production-readiness-certification.md) document.

### Fixed

- Four defects found during adversarial self-review and a live PostgreSQL drill before the initial PR, including one that completely blocked retention deletion in production (a Slice 6 append-only trigger on `support_ticket_messages` rejected `DELETE`, invisible to the SQLite-backed test suite).
- Seven further defects found by an automated Codex review after the PR was opened (tracked as [#13](https://github.com/danijeljw-RPC/lets-go-travel/issues/13)–[#19](https://github.com/danijeljw-RPC/lets-go-travel/issues/19), all closed): legal-hold protection gaps for child record classes and mismatched subject-kind scopes, an audit-record retention sweep that could get permanently stuck behind a held prefix, deletion receipts overcounting under concurrent workers, a failed operational-case insert that could silently abort the rest of a worker cycle, and an unrelated launch-page signup mistagging.

### Notes

- Retention sweeps and the legal-hold API default disabled/inert in every checked-in configuration until their production-readiness gate is approved.
- Canonical booking evidence and raw supplier payload record classes remain policy-and-calculator only (no live sweep) since no live data of those kinds exists yet.

### Tests

553/553 passing (up from 357 at the end of Slice 6).

## Slice 6 — Support Tickets, Guest Magic Links and Private Attachments (2026-08-09)

[Outcome report](docs/delivery/2026-08-09-slice-6-support-tickets-magic-links-outcome.md)

### Added

- First-party support tickets with an immutable, append-only correspondence thread, for both authenticated customers and anonymous guests.
- Guest access via a high-entropy magic-link bearer token: only its SHA-256 hash is persisted, the raw token is never returned by any API response after issuance, and it is multi-use with staff-only rotate/revoke.
- Private S3-compatible ticket attachments (MinIO locally, Amazon S3 in production once activated) with content-type/signature validation and fail-closed ClamAV malware scanning; only an explicit `Clean` scan result makes an attachment downloadable.
- A Keycloak-role-gated (`support-agent`) staff console for ticket handling, attachment review and guest-link administration.

### Fixed

Three rounds of automated Codex review after the initial PR, all resolved before merge — most significantly a raw guest magic-link token that could sit indefinitely in the durable notification outbox in a Production configuration with no registered sender (fixed by deferring token minting to actual send time so the durable payload never contains a token), a race that could leave two simultaneously active guest tokens for one ticket (fixed with a database-level unique partial index as the real correctness boundary), and — after this branch had already merged into `dev` — a four-commit hardening chain for the guest-ticket rate-limit ceiling ([#6](https://github.com/danijeljw-RPC/lets-go-travel/issues/6), [#7](https://github.com/danijeljw-RPC/lets-go-travel/issues/7), [#10](https://github.com/danijeljw-RPC/lets-go-travel/issues/10)) that closed a shared-bucket-per-deployment gap surfaced by three further rounds of review, ending with both hosts failing fast at startup if the caller-forwarding secret is left unconfigured in Production.

### Tests

357/357 passing (up from 235 at the end of Slice 5).

## Slice 5 — Booking Reconciliation and Notifications (2026-08-08)

[Outcome report](docs/delivery/2026-08-08-slice-5-booking-reconciliation-outcome.md)

### Added

- Authenticated, fail-closed webhook ingress with a durable inbox and `event_id` deduplication.
- Scheduled hotel and flight reconciliation with retrieval-driven, append-only immutable canonical booking versions and an owner-only booking history API.
- Deduplicated, severity-classified customer-notification intents with quiet-hours handling, delivered through a durable outbox.

### Tests

235/235 passing (up from 158 at the end of Slice 4).

## Slice 4 — Checkout, Hosted Payment and Booking (2026-07-29)

[Outcome report](docs/delivery/2026-07-29-slice-4-checkout-hosted-payment-booking-outcome.md)

### Added

- Authenticated, server-resolved checkout with renewed price acceptance and provider-hosted payment isolation (no card data ever touches this platform's servers).
- Durable idempotency and immediate retrieval-based recovery for payment/booking mismatches, so a failure never produces a duplicate charge or duplicate booking.
- Separately evidenced hotel and flight component bookings, including combined multi-component journeys, and an interactive Blazor checkout experience.

### Tests

158/158 passing (up from 52 at the end of Slice 3).

## Slice 3 — Search and Capability Registry (2026-07-29)

[Outcome report](docs/delivery/2026-07-29-slice-3-search-capability-outcome.md)

### Added

- Supplier-neutral hotel and flight search contracts with minimum-total pricing.
- An explicit environment/market/operation/carrier capability registry, so unsupported combinations fail closed rather than silently returning wrong results.
- Sanitized, deterministic LiteAPI fixtures for development/testing; production search remains disabled until its supplier gate is approved.
- Public search API routes and a Blazor SSR search shell.

### Tests

52 tests passing (17 Search, 15 Consumer, 7 Architecture, 7 Web, 5 API, 1 Building Blocks).

## Slice 2 — Consumer Foundation (2026-07-29)

Already present on `main` prior to this changelog's creation.

### Added

- Subject-owned consumer profiles, locale handling, trips and low-risk traveller records.
- PostgreSQL migrations for the Consumer schema and authenticated API routes.
- Blazor SSR account pages and a pinned local Keycloak/PostgreSQL development runtime.

## Slice 1 — Platform Foundation (2026-07-29)

Already present on `main` prior to this changelog's creation.

### Added

- The initial .NET 10 solution, public API contract and Blazor SSR host.
- Cooperative background workers, non-root containers, locked CI and automated documentation validation.
