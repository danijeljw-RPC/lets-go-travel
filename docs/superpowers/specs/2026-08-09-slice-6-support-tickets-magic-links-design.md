<!-- markdownlint-disable MD013 -->

# Slice 6 Support Tickets, Guest Magic Links and Private Attachments Design

## Status

Approved for autonomous implementation on 2026-08-09 by the request to complete PLAN-0002 Slice 6. This design refines the already accepted PLAN-0002, closed OI-0012, ADR-0003, ADR-0009, ADR-0010, the MVP Operational Policy and the Data Retention and Legal Hold baseline. It does not approve production object-storage or malware-scanning activation and does not open any new product-scope question.

## Objective and scope

Implement first-party support tickets with an immutable correspondence thread, authenticated customer access, anonymous/guest ticket creation and access through a high-entropy magic-link bearer token, private S3-compatible attachments with fail-closed malware scanning, and a Keycloak-role-gated staff console. Guest access must resolve strictly through the token, never through a client-supplied ticket identifier, and must never exceed the single ticket the token was issued for.

Slice 7 retention enforcement, legal-hold tooling and production-readiness certification remain excluded. This slice stores the fields retention enforcement will need (closure timestamps, token/attachment lifecycle state) without implementing scheduled purge jobs. Native mobile, AI planning, insurance, loyalty, stored value, credit, live operational flight status and unrelated supplier/sharing capabilities are out of scope, consistent with PLAN-0002's deferred-scope list.

## Approaches considered

### Approach A — New `ReadyToGoTravel.Support` feature module, same shape as Booking/Consumer

Add a new feature project with its own PostgreSQL schema (`support`), its own `SupportDbContext`, and `Http`/`Domain`/`Persistence`/`Storage`/`Scanning`/`Notifications`/`Guest` sub-namespaces, mirroring the Booking module's internal organisation. The module owns ticket, message, guest-token, attachment, scan-work, notification-outbox and audit-event tables. Deployable hosts (API, Web, the existing general Worker) reference it as a composition root only, exactly as `BookingModule` is composed today.

This keeps one atomic persistence boundary per concern (ticket status changes with its thread entry; attachment metadata changes with its scan work row) and avoids a new module querying another module's private tables, per ADR-0009.

### Approach B — Extend the Consumer module with support tables

Support tickets are not primarily about the authenticated customer profile; a large share of activity (guest creation, guest correspondence, staff handling) has no `Consumer` subject at all. Folding this into Consumer would blur an unrelated module boundary and force every guest code path to depend on the Consumer schema for no benefit.

### Approach C — Implement attachment storage as a shared `BuildingBlocks` abstraction from the start

No other module needs blob storage yet. Introducing a shared abstraction now, before a second consumer exists, would guess at a contract this slice cannot validate. The interface is written narrowly enough (`IObjectStorage` with put/get-signed-url/delete) that promoting it to `BuildingBlocks` later, if another slice needs it, is a mechanical move.

## Selected approach

Choose Approach A with the storage interface kept inside the new module per Approach C's reasoning. `ReadyToGoTravel.Support` becomes a fifth feature project, wired into the existing `ReadyToGoTravel.Api` host and the existing general `ReadyToGoTravel.Worker` host (no new deployable container; the general worker gains an attachment-scan and support-notification processing cycle exactly as it gained webhook/reconciliation/notification cycles in Slice 5).

## Ticket domain and lifecycle

`SupportTicket` is the current-projection aggregate: internal UUIDv7 `Id`, optional `CustomerId` (populated only when created by an authenticated subject), `ContactName`, `ContactEmail` (sourced from the account and immutable for authenticated customers; a required editable field for guests), `Category` (`General`, `TravelWithin24Hours`, `PaymentBookingMismatch`, `SupplierCancellationOrRelocation`, `TravellerSafety`, `AccountOrOther`), a derived `IsUrgent` flag matching the operational policy's urgent categories, optional free-text `BookingReference`, `Status`, `CreatedAtUtc`, `UpdatedAtUtc`, `ClosedAtUtc` and a concurrency token.

`Status` follows the closed OI-0012 decision exactly: every ticket starts `New`; an accepted customer/guest reply moves it to `WaitingOnSupport`; an accepted support reply moves it to `WaitingOnCustomer`; an authorised support user sets `Closed`; an accepted customer/guest reply to a closed ticket reopens it to `WaitingOnSupport` while the closure remains a permanent thread entry rather than being rewritten.

`SupportTicketMessage` is the append-only correspondence thread: `Id`, `TicketId`, `SequenceNumber`, `AuthorType` (`Customer`, `Guest`, `Support`, `System`), `AuthorCustomerId` (nullable), `Body`, `CreatedAtUtc`. System-authored entries record status transitions (for example "Ticket closed by support") in the same ordered thread the customer/guest can read, so closure and reopening stay visible without a second customer-facing table. Messages have no update or delete path in the domain API. `SupportDbContext.SaveChanges`/`SaveChangesAsync` reject a modified or deleted `SupportTicketMessage`, and the PostgreSQL migration installs a trigger that rejects `UPDATE`/`DELETE` against the table outside EF, mirroring the Slice 5 `booking.reject_booking_version_mutation()` pattern exactly.

Internal-only `SupportAuditEvent` rows record security-relevant operations that are never shown to a customer or guest: guest-link issuance, rotation, revocation, authentication failure, attachment scan outcomes and download authorisation/denial. This is the Slice 6 analogue of the Slice 5 `OperationalCase` table: an internal record, not a support thread entry.

## Guest magic links

Every ticket that permits guest access has zero or one active `SupportGuestAccessToken` row (plus historical rows left behind by rotation). Issuance generates 32 bytes from `RandomNumberGenerator.GetBytes` (256 bits of entropy) and encodes them unpadded Base64Url for transport. Only `SHA256.HashData` over the raw token, hex-encoded, is persisted in a unique `token_hash` column; the raw token exists only in memory during issuance and inside the one outbound email that carries it. It is never returned by an API response, never logged, and never re-displayed after issuance — a "resend" action rotates to a new token rather than re-exposing the original.

A token expires 30 days after issuance, is not indefinitely valid, and is not single-use: a still-valid token authenticates every request until it expires or is explicitly revoked or rotated, matching OI-0012's "can be revoked or rotated" framing rather than a one-time password-reset-style token. Rotation marks the prior token revoked and links the new row to it for audit lineage; revocation alone leaves no replacement. A guest request presents the token as an `Authorization: Bearer <token>` header (never a query string, keeping it out of typical URL-based access-log capture); the server hashes the supplied value and performs an exact-match lookup by `token_hash` — the 256-bit search space makes a lookup-timing side channel immaterial, so no constant-time comparison is required here (unlike the small fixed webhook-secret set in Slice 5, which needed `FixedTimeEquals`). A missing, malformed, expired or revoked token produces the same `401` outcome and a `SupportAuditEvent` authentication-failure entry; it never distinguishes "wrong token" from "right token, wrong state" in the response.

Every guest endpoint resolves `TicketId` from the authenticated token alone. No guest route accepts a client-supplied ticket or attachment identifier as the sole authorization input; attachment lookups additionally verify the attachment's `TicketId` matches the token's resolved ticket before any storage operation. This directly prevents both cross-ticket IDOR and the token from ever granting broader access than its own ticket.

The Blazor guest page lives at `/support/guest/{token}` (no `[Authorize]` — there is no Keycloak session) and calls the guest API using the token as a bearer header rather than placing it in the API route, so the token appears in exactly one browser-facing URL (the emailed link itself) and never in the platform API's own request path.

## Private attachments

`SupportAttachment` records `Id`, `TicketId`, `MessageId`, `OriginalFileName` (sanitised for display only), `ContentType`, `SizeBytes`, `StorageKey`, `Sha256Checksum`, `ScanStatus` (`Pending`, `Clean`, `Infected`, `Failed`), `UploadedByCustomerId`/`UploadedByGuestTokenId`, `CreatedAtUtc`. The storage key is always server-generated as `support/{ticketId}/{attachmentId}{extension}`, where `extension` comes from a fixed content-type-to-extension map — never from the client-supplied filename — which removes path-traversal and injection risk from the object key entirely. The original filename is retained only as metadata, sanitised of path separators and control characters, and used solely to build a safely encoded `Content-Disposition` header on download.

Upload validation enforces the ADR-0010 MVP limits before anything is persisted: 10 MiB per file, 5 files per message, 50 MiB cumulative per ticket, and only PDF, JPEG, PNG and UTF-8 plain text. The server checks both the declared content type and the file's leading bytes (`%PDF-` for PDF; `FF D8 FF` for JPEG; the eight-byte PNG signature; strict UTF-8 decode with no embedded NUL for text) so a relabelled executable cannot pass as an allowed type. A validated upload is written to the object store immediately with `ScanStatus = Pending`; nothing is downloadable yet.

`IObjectStorage` is a narrow S3-compatible abstraction (`PutAsync`, `CreateDownloadUrlAsync` returning a presigned URL capped at five minutes per ADR-0010, `DeleteAsync`) implemented with `AWSSDK.S3`, which speaks the same protocol against MinIO (local, path-style addressing) and Amazon S3 `ap-southeast-2` (production, once activated). `IAttachmentScanner` abstracts malware scanning; the concrete `ClamAvScanner` speaks the ClamAV `INSTREAM` TCP protocol directly (a bounded, well-documented framing protocol, not cryptography, so hand-rolling it does not conflict with the "no custom cryptography" constraint and avoids adding a third-party scanning dependency). A scan is claimed and processed by a new `AttachmentScanWork` outbox, using the identical claim-lease-`ExecuteUpdateAsync` pattern as the Slice 5 webhook inbox and notification outbox, run from the existing general worker.

Scanning is fail-closed everywhere: `Clean` is the only outcome that makes an attachment downloadable. `Infected` deletes the object from storage, marks the row permanently unavailable and raises a `SupportAuditEvent`. A scanner failure or unreachable scanner is treated as a transient failure, retried with the same bounded exponential backoff and `MaxAttempts = 8` used elsewhere, and lands on `Failed` (not `Clean`) when attempts are exhausted — never inferring safety from an absent result. Production registers a disabled scanner by default (the Slice 6 analogue of Slice 5's disabled notification sender), so production attachments remain quarantined until a real ClamAV endpoint is configured and evidenced; this is recorded as a new production gate rather than assumed complete.

Download requires the caller to already be authorized for the ticket (owning customer, matching guest token, or a `support-agent`), then the endpoint returns a presigned GET URL valid for at most five minutes rather than proxying bytes or exposing a public object URL. Attachments are never publicly addressable; the bucket denies anonymous access entirely, and every issued URL is scoped, time-limited and single-object.

## Staff console and authorization

`ReadyToGoTravel.Api`'s `AuthenticationExtensions` gains a `support-agent` authorization policy backed by a new `AuthorizationHandler` that inspects the Keycloak `realm_access` claim's `roles` array for `support-agent`, since ASP.NET Core does not flatten that nested claim automatically. The local Keycloak realm export gains a `support-agent` realm role so a real session can be granted it; the existing `TestAuthenticationHandler` gains the ability to set the same role claim for tests, exactly as it already fakes `sub`. Per ADR-0010, impersonation is not implemented in this slice — staff act as themselves, and every staff action records its actor's own subject in the audit trail and thread.

The Blazor staff console (`/staff/support` list, `/staff/support/{ticketId}` detail) is a protected SSR area requiring the `support-agent` policy, following the existing typed-`HttpClient` page pattern. Staff can view any ticket, reply (moving it to `WaitingOnCustomer`), close it, and revoke or rotate its guest link. There is no cross-tenant browsing surface for customers or guests; only staff can list tickets outside their own ownership.

## Notifications

A `SupportNotificationOutboxItem` table, populated transactionally alongside the triggering ticket/message write, mirrors the Slice 5 `NotificationOutboxItem` shape: a durable, retried, deduplicated (unique per ticket, message, channel) delivery record processed by the same worker cycle pattern. Every accepted ticket creation and every accepted thread update queues an immediate acknowledgement per OI-0012; a guest-created ticket's acknowledgement carries the magic link, and only that one email ever carries the raw token. `ISupportNotificationSender` is the outbound boundary; Production registers no live sender by default, matching the Slice 5 precedent of failing closed to an inspectable failed outbox item rather than inventing an email vendor decision.

## Security, concurrency and failure rules

- Guest authorization derives only from the token hash lookup; a ticket UUID or attachment ID alone never grants access, per the accepted trust-and-data-boundaries.md rule.
- Owner-mismatch on an authenticated route returns the same `404` shape already used by Consumer/Booking endpoints rather than a `403` that would confirm existence.
- The raw guest token is never logged, never returned by any endpoint after issuance, and never embedded in an internal API route.
- Attachment object keys are always server-generated; user-supplied filenames only ever appear in a sanitised, encoded response header, never in a storage path or shell/SQL context.
- All storage and scanning calls receive cancellation tokens and bounded timeouts; a claim lease prevents duplicate scan/notification processing exactly as in Slice 5.
- Malware scanning and the "clean" gate fail closed; nothing becomes downloadable without an explicit `Clean` result.
- Every guest-link lifecycle event and every attachment scan/download decision is audited in `SupportAuditEvent`, independent of the customer-visible thread.

## Testing and verification

Tests cover ticket creation (authenticated and guest), ownership isolation and cross-customer/cross-guest denial, the full status transition table including reopen-after-close, message thread immutability, guest token issuance/expiry/revocation/rotation/replay-of-a-still-valid-token/malformed-token/cross-ticket-token-misuse, attachment upload validation (size, count, type, signature mismatch), the scan pipeline (clean, infected, scanner-unavailable-retry, exhausted-attempts), download authorization for owner/guest/staff and denial for everyone else, storage-key/path-traversal safety, persistence/migration parity, the new `support-agent` policy, and existing Slice 1–5 regressions. Repository completion checks remain locked restore, formatting, warning-as-error Release build, the full solution test suite, EF pending-model parity, documentation validation, `git diff --check`, and all container builds (API, Web, general worker, flight-reconciliation worker — unchanged count, since Support runs inside the existing API/Web/general-worker hosts).

## Production gates

OI-0002, OI-0003, OI-0005 and OI-0006 remain unchanged and open. This slice adds one new production-readiness item: object storage (S3-compatible bucket, credentials, Australian region) and ClamAV production activation remain unevidenced; both default to a disabled/unavailable posture that keeps attachments quarantined rather than assuming production readiness. No email vendor is selected; `ISupportNotificationSender` has no production implementation, matching the Slice 5 notification posture. Named support staffing, service-hour coverage and supplier escalation contacts remain the operational evidence already tracked by the MVP Operational Policy and are not created by this implementation.

## Self-review record

- Placeholder scan: no TBD or unresolved implementation placeholder remains.
- Consistency: ticket lifecycle, guest-token handling and attachment limits match OI-0012, ADR-0010 and the MVP Operational Policy verbatim; no product fact was invented.
- Scope: retention enforcement, legal holds, live chat, impersonation and production storage/scanning/email activation remain explicitly excluded.
- Ambiguity: guest tokens are multi-use until expiry/revocation (not one-time), expiry is a fixed 30 days documented here as an engineering default (not a legal/commercial fact), and guest routes never accept a client-supplied resource identifier as an authorization input.
