<!-- markdownlint-disable MD013 -->

# Remaining Review Register

## Status

Evidence reconciliation completed on 2026-07-29. **MVP implementation is approved to start.** Supplier-neutral code, sandbox adapters, disabled production integrations and compliance-first controls may proceed. The remaining items are production-activation evidence, exercised operational controls or named approvals; none is an unanswered product-scope question.

Implementation checkpoint: webhook ingress, scheduled reconciliation, immutable booking-version and notification-intent Slice 5 is complete. Sanitized LiteAPI fixtures run only in development/testing; Production registers no payment, booking or notification provider, and webhook ingress defaults disabled. Qantas, Jetstar and Virgin Australia remain observed sandbox carriers only. Slice 6 first-party support tickets, guest magic-link access and private attachments are complete; Production registers no object-storage or malware-scanning backend by default, so attachments remain quarantined until that capability is activated. Slice 7 retention/privacy operations and production-readiness certification is complete: legal-hold-aware retention sweeps, a staff-only legal-hold API and a code-verifiable [production-readiness catalog](../operations/production-readiness-certification.md) are implemented and locally verified against a live PostgreSQL drill; `Retention:Enabled` defaults disabled in every checked-in configuration. OI-0002, OI-0003, OI-0005 and OI-0006 remain open production gates; Slice 5, 6 and 7 completion does not supply their contract, carrier, webhook-delivery, email-provider or PCI approval evidence.

## Production Activation Reviews

| Review | Implementation disposition | Production evidence still required | Outcome |
| --- | --- | --- | --- |
| [OI-0002 LiteAPI commercial/MOR](../issues/open/OI-0002-liteapi-commercial-and-merchant-of-record.md) | Implement the LiteAPI/provider payment route behind a disabled production capability. Do not model `readytogo.travel` as merchant of record. | Executed merchant, settlement, descriptor, refund, dispute, chargeback, tax and booking-failure allocation. | [Public evidence](../evidence/liteapi/OI-0002-commercial-mor-review.md); remains in review for contract evidence. |
| [OI-0003 Australian carrier capability](../issues/open/OI-0003-australian-airline-and-qantas-coverage.md) | Implement carrier-neutral flight search/booking and an environment/market capability registry. Qantas, Jetstar and Virgin Australia are observed search carriers. | Production entitlement plus dated verify, prebook, book, ticket, retrieve and servicing evidence per enabled carrier. | [Capability evidence](../evidence/liteapi/OI-0003-australian-carrier-capability-review.md); remains in review for production enablement. |
| [OI-0004 flight servicing](../issues/closed/OI-0004-flight-servicing-and-schedule-changes.md) | Implement partly manual, capability-gated servicing, daily/hourly reconciliation and direct-airline day-of-travel guidance. | Supplier contacts, account-specific SLA and exercised production capabilities before enabling each self-service action. | Closed as a product/architecture decision. |
| [OI-0005 webhook guarantees](../issues/open/OI-0005-liteapi-webhook-coverage.md) | Implement authenticated at-least-once ingress, durable inbox, `event_id` deduplication, asynchronous retrieval and scheduled fallback. | Account subscriptions, secrets, retry settings and controlled production delivery/duplicate/missing-event tests. | [Webhook evidence](../evidence/liteapi/OI-0005-webhook-guarantees.md); remains in review for production reliance. |
| [OI-0006 payment/PCI](../issues/open/OI-0006-mobile-payment-and-pci-scope.md) | Implement Blazor JavaScript interop for the LiteAPI hosted/SDK component, server-side checkout state and idempotent return/recovery. Native payment remains deferred. | Current AOC, responsibility matrix, qualified scope, 3-D Secure and production-domain/payment-method evidence. | [Payment evidence](../evidence/liteapi/OI-0006-payment-sdk-pci-scope.md); remains in review for production activation. |
| [OI-0011 retention](../issues/closed/OI-0011-supplier-payload-retention.md) | Implement the approved seven-year canonical, 90-day successful raw, 12-month exceptional raw and 35-day backup baseline. | Executed LiteAPI/DPA review, Australian approval and tested enforcement before claiming production compliance. | Closed as a data-governance/architecture decision. |
| [Australian market legal pack](../australian-market-legal-pack/00-README.md) | Implement minimum-total pricing, confirmation-state accuracy, APP baseline, intermediary disclosures, no insurance/wallet, adult purchaser, breach response and customer-document hooks. | Operating entity, executed terms, funds flow, final customer documents and formal checklist approvals. | Closed as an implementation baseline; launch checklist remains pending. |
| Object storage and malware-scanning activation (Slice 6, [ADR-0010](../adr/accepted/ADR-0010-mvp-runtime-storage-and-delivery-baseline.md)) | Implement S3-compatible private attachment storage (MinIO locally) and a fail-closed ClamAV `INSTREAM` scan adapter behind disabled-by-default production capability flags; attachments stay quarantined with no registered backend. | Provisioned Australian-region S3-compatible bucket and credentials, a production ClamAV deployment/monitoring path, and exercised quarantine/failure-mode tests. | Implementation baseline complete; production activation evidence outstanding. |
| Retention sweep production activation (Slice 7) | Implement legal-hold-aware retention sweeps for every concretely-scoped record class behind a disabled-by-default `Retention:Enabled` flag; verify against a live PostgreSQL drill. | `Retention:Enabled` flipped on in a verified production configuration, plus a completed production retention/legal-hold drill. | Implementation complete and locally verified; production activation evidence outstanding. |
| Legal-hold-officer role provisioning (Slice 7) | Implement a `legal-hold-officer` Keycloak realm role, authorization policy and staff-only legal-hold administration API. | Named legal/compliance staff assigned the `legal-hold-officer` role. | Implementation complete; staffing assignment outstanding (non-blocking operational evidence). |

## Operational Reviews Before Launch

The [MVP Operational Policy](../operations/mvp-operational-policy.md) fixes the implementation defaults:

- ticket-only support, 08:00–20:00 Australia/Sydney every day, with urgent categories and outside-hours direct-supplier guidance;
- informational, minor, material and travel-blocking notification severity with 22:00–07:00 customer-local quiet hours;
- five-minute PostgreSQL RPO and four-hour RTO, plus stated Keycloak/object/notification objectives; and
- restore, reconciliation, retention-tombstone and exercise requirements.

Named staffing, supplier contacts and successful exercises remain production evidence rather than design questions.

## Technical Planning Reviews

The technical review is resolved by [ADR-0009](../adr/accepted/ADR-0009-mvp-application-contract-and-module-foundation.md) and [ADR-0010](../adr/accepted/ADR-0010-mvp-runtime-storage-and-delivery-baseline.md):

- `/api/v1`, additive compatibility and a 12-month external-client major-version support window;
- ASP.NET Core `ProblemDetails`, correlation IDs, built-in OpenAPI and endpoint-specific rate limits;
- Azure Container Apps and Azure PostgreSQL in Australia East with PostgreSQL durable inbox/outbox/schedules;
- S3-compatible storage using MinIO locally and Amazon S3 `ap-southeast-2` for production, with fail-closed ClamAV scanning and fixed upload/download limits;
- Azure Key Vault, OpenTelemetry/Azure Monitor, no initial distributed cache and no MVP impersonation;
- GitHub Actions immutable container promotion with protected production approval; and
- feature-oriented modular-monolith projects for API, Blazor SSR, general work and dedicated flight reconciliation.

## Settled Non-blocking Deferrals

- OI-0010 live operational flight status is a post-MVP wishlist item and does not block the MVP.
- Native Android/iOS applications, push/SMS, offline trip packs, trip sharing, loyalty, AI planning and manually added itinerary items are deferred from the initial transactional release.
- Duffel customer inventory remains disabled until its separate production settlement gate is satisfied.

## No Further Product-owner Answer Currently Required

The repository review found no remaining MVP scope question that requires another immediate product-owner answer. MVP code may begin now. Production capabilities remain disabled until their corresponding evidence row is approved; this preserves a safe launch gate without holding provider-neutral implementation hostage to contracts or production account access.
