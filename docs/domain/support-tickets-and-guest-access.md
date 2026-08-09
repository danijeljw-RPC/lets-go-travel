<!-- markdownlint-disable MD013 -->

# Support Tickets, Guest Access and Private Attachments

This page documents the Slice 6 support model implemented against closed [OI-0012](../issues/closed/OI-0012-customer-support-model.md) and [ADR-0010](../adr/accepted/ADR-0010-mvp-runtime-storage-and-delivery-baseline.md). See the [Slice 6 design](../superpowers/specs/2026-08-09-slice-6-support-tickets-magic-links-design.md), [implementation plan](../superpowers/plans/2026-08-09-slice-6-support-tickets-magic-links.md) and [outcome report](../delivery/2026-08-09-slice-6-support-tickets-magic-links-outcome.md) for the full record.

## Ticket Lifecycle

A support ticket starts `New`. An accepted customer or guest reply moves it to `WaitingOnSupport`; an accepted support reply moves it to `WaitingOnCustomer`; an authorised support user closes it to `Closed`. An accepted customer or guest reply against a closed ticket reopens it to `WaitingOnSupport` while the closure remains a permanent entry in the thread rather than being rewritten. A support reply is rejected while the ticket is closed; only the customer or guest can reopen it.

The correspondence thread is append-only, mirroring the Slice 5 immutable booking-version pattern: the `SupportDbContext` change tracker and a PostgreSQL trigger both reject any `UPDATE` or `DELETE` against `support.support_ticket_messages`. Status transitions that have no accompanying customer/guest/support text (closure, reopening) are recorded as `System`-authored thread entries, so the full history stays visible to the ticket's owner without a second customer-facing table.

## Ownership and Categories

`SupportTicket.CustomerSubject` stores the immutable Keycloak subject for a ticket created by an authenticated customer, or `null` for a guest-created ticket. An authenticated customer's contact email always comes from the validated `sub`/`email` claims and cannot be overridden by the request body. `Category` follows the MVP Operational Policy's urgent set: `TravelWithin24Hours`, `PaymentBookingMismatch`, `SupplierCancellationOrRelocation` and `TravellerSafety` are urgent; `General` and `AccountOrOther` are not.

## Guest Magic Links

A guest-created ticket receives a `SupportGuestAccessToken`: 32 bytes from `RandomNumberGenerator`, transported as an unpadded Base64Url string, with only its SHA-256 hash persisted (`support.support_guest_access_tokens.token_hash`, unique). The raw token exists only in memory during issuance and in the single acknowledgement email that carries it; no API response ever returns it again. A token is valid for 30 days from issuance and is multi-use — it authenticates every request until it expires, is revoked, or is superseded by rotation — rather than being a single-use credential. Rotation and revocation are staff-only actions and immediately invalidate the prior token.

Every guest route resolves its ticket exclusively from the token: the token is hashed and looked up, and the resulting `TicketId` scopes every subsequent query. No guest route accepts a client-supplied ticket or attachment identifier as an authorization input. A missing, malformed, expired or revoked token produces the same `401` outcome without revealing which condition failed, and every resolution attempt — success or failure — is recorded in the internal, non-customer-visible `support.support_audit_events` table.

## Private Attachments

`SupportAttachment` rows are private objects in an S3-compatible store (`AWSSDK.S3` against MinIO locally, Amazon S3 `ap-southeast-2` in production once activated). The storage key is always server-generated as `support/{ticketId}/{attachmentId}{extension}`, where the extension comes from a fixed content-type-to-extension map — never from the client-supplied filename — eliminating path traversal from the object key. The original filename is retained only as sanitised display metadata.

Uploads are validated against both the declared content type and the file's leading bytes before anything is persisted, enforcing the ADR-0010 limits: 10 MiB per file, 5 files per message, 50 MiB per ticket, and only PDF, JPEG, PNG and UTF-8 plain text. A validated upload is stored immediately with `ScanStatus = Pending` and is not downloadable.

A claim-lease worker cycle (`AttachmentScanProcessor`, following the exact pattern of the Slice 5 webhook inbox and notification outbox processors) retrieves the object and calls `IAttachmentScanner`, whose production implementation speaks the ClamAV `INSTREAM` protocol directly. Only an explicit `Clean` result makes an attachment downloadable; `Infected` deletes the object and marks it permanently unavailable, and a scanner failure or unreachable scanner is treated as transient and retried, never inferred as safe. Production registers a disabled scanner and a disabled object-storage backend by default, so attachments remain quarantined until both capabilities are explicitly activated and evidenced. Downloads require the caller to already be authorized for the ticket, then return a presigned URL valid for at most five minutes rather than a public object URL.

## Staff Access

The privileged support console (`/staff/support`) is a protected Blazor SSR area gated by a `support-agent` authorization policy backed by a custom handler that inspects the Keycloak `realm_access` role claim, since ASP.NET Core does not flatten that nested claim automatically. Per ADR-0010, impersonation is not implemented in this slice; every staff action records the acting subject in the thread or audit trail.

## Retention

Slice 6 stores the fields Slice 7's retention enforcement will need — ticket closure timestamps and attachment creation timestamps — without implementing scheduled purge jobs itself. The applicable default periods (booking-related ticket evidence: 7 years from closure or final linked dispute resolution; general tickets: 2 years from closure; support attachments: 90 days from ticket closure) are recorded in [Data Retention and Legal Hold](../security/data-retention-and-legal-hold.md) and remain unimplemented enforcement, consistent with PLAN-0002's Slice 7 scope.
