<!-- markdownlint-disable MD013 -->

# Slice 6 Support Tickets, Guest Magic Links and Private Attachments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver first-party support tickets with an immutable correspondence thread, authenticated and guest magic-link access, private S3-compatible attachments with fail-closed malware scanning, and a Keycloak-role-gated staff console, without enabling production object storage or malware scanning.

**Architecture:** Add a new `ReadyToGoTravel.Support` feature module with its own `support` PostgreSQL schema, following the exact module/DbContext/migration/Http shape of `ReadyToGoTravel.Booking`. Wire it into the existing `ReadyToGoTravel.Api` host and the existing general `ReadyToGoTravel.Worker` host; no new deployable container is introduced.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core 10, PostgreSQL, SQLite integration tests, xUnit, `AWSSDK.S3` against MinIO locally, hand-rolled ClamAV `INSTREAM` client, existing cooperative worker and container build workflow.

## Global Constraints

- Support tickets, guest tokens, attachments, storage keys and audit events follow closed OI-0012 and ADR-0010 exactly: `New` → `WaitingOnSupport` → `WaitingOnCustomer` → `Closed` → reopen to `WaitingOnSupport`; UUIDv7 ticket ID plus a separate high-entropy bearer token; only the token hash is stored.
- Attachment limits are fixed: 10 MiB per file, 5 files per message, 50 MiB per ticket; only PDF, JPEG, PNG and UTF-8 plain text are accepted; downloads use application authorization then a signed URL valid for at most 5 minutes.
- Malware scanning is fail-closed: a scan failure or unavailable scanner never marks an attachment downloadable.
- Guest authorization always resolves from the token hash; no guest route accepts a client-supplied ticket or attachment identifier as its sole authorization input.
- No production object-storage, malware-scanning or email-sending capability is enabled by this slice; each defaults to a disabled/unavailable posture that fails closed.
- Slice 7 retention enforcement, legal holds, live chat and any PLAN-0002 deferred scope (native mobile, AI planning, insurance, loyalty, stored value, credit, live flight status) are excluded.
- `Directory.Build.props` `TreatWarningsAsErrors` stays `true`; the new project targets `net10.0` and is named `ReadyToGoTravel.Support`.

---

### Task 1: Support module skeleton, ticket and message domain, persistence

**Files:**
- Create: `src/ReadyToGoTravel.Support/ReadyToGoTravel.Support.csproj`
- Create: `src/ReadyToGoTravel.Support/SupportModule.cs`
- Create: `src/ReadyToGoTravel.Support/Domain/SupportTicket.cs`
- Create: `src/ReadyToGoTravel.Support/Domain/SupportTicketMessage.cs`
- Create: `src/ReadyToGoTravel.Support/Domain/SupportTicketStatus.cs`
- Create: `src/ReadyToGoTravel.Support/Domain/SupportTicketCategory.cs`
- Create: `src/ReadyToGoTravel.Support/Domain/SupportAuthorType.cs`
- Create: `src/ReadyToGoTravel.Support/Domain/SupportAuditEvent.cs`
- Create: `src/ReadyToGoTravel.Support/Application/SupportTicketService.cs`
- Create: `src/ReadyToGoTravel.Support/Persistence/SupportDbContext.cs`
- Create: `src/ReadyToGoTravel.Support/Persistence/SupportEntityConfigurations.cs`
- Create: `src/ReadyToGoTravel.Support/Persistence/SupportDesignTimeDbContextFactory.cs`
- Modify: `ReadyToGoTravel.slnx`
- Modify: `Directory.Packages.props`
- Test: `tests/ReadyToGoTravel.Support.Tests/ReadyToGoTravel.Support.Tests.csproj`
- Test: `tests/ReadyToGoTravel.Support.Tests/SupportTicketDomainTests.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/SupportTicketServiceTests.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/SupportPersistenceTests.cs`

**Interfaces:**
- Produces: `SupportTicketStatus { New, WaitingOnSupport, WaitingOnCustomer, Closed }`; `SupportTicketCategory { General, TravelWithin24Hours, PaymentBookingMismatch, SupplierCancellationOrRelocation, TravellerSafety, AccountOrOther }`; `SupportAuthorType { Customer, Guest, Support, System }`.
- Produces: `SupportTicketService.CreateTicketAsync(CreateSupportTicketCommand command, CancellationToken ct) : Task<SupportTicket>`, where `CreateSupportTicketCommand(Guid? CustomerId, string ContactName, string ContactEmail, SupportTicketCategory Category, string? BookingReference, string InitialMessageBody)`.
- Produces: `SupportTicketService.AddMessageAsync(Guid ticketId, SupportAuthorType authorType, Guid? authorCustomerId, string body, CancellationToken ct) : Task<SupportTicketMessage>` — applies the OI-0012 status transition table and appends a `System` message when status changes.
- Produces: `SupportTicketService.CloseAsync(Guid ticketId, Guid staffSubjectId, CancellationToken ct) : Task` and `SupportTicketService.GetForCustomerAsync(Guid customerId, Guid ticketId, CancellationToken ct) : Task<SupportTicket?>`.
- Produces: `SupportModule.AddSupportModule(IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase) : IServiceCollection`.

- [ ] **Step 1: Scaffold the project and register it in the solution**

Create `ReadyToGoTravel.Support.csproj` matching `ReadyToGoTravel.Booking.csproj`'s shape (`net10.0`, `ImplicitUsings`, `Nullable`, `FrameworkReference` to `Microsoft.AspNetCore.App`, `PackageReference` to `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`) plus a `ProjectReference` to `ReadyToGoTravel.Consumer` (needed later for the customer-owned checkout/booking-reference cross-check; Support does not query Consumer's tables, it only calls its public application contract in a later task). Add the project and its test project to `ReadyToGoTravel.slnx` under `/src/` and `/tests/`. Create `tests/ReadyToGoTravel.Support.Tests/ReadyToGoTravel.Support.Tests.csproj` matching `ReadyToGoTravel.Booking.Tests.csproj` (xUnit, `Microsoft.NET.Test.Sdk`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.AspNetCore.TestHost`, project references to `ReadyToGoTravel.Support`, `ReadyToGoTravel.Api`).

- [ ] **Step 2: Write failing domain and status-transition tests**

In `SupportTicketDomainTests.cs`, cover: a new ticket starts `New`; a `Customer`/`Guest` message on a `New` or `WaitingOnCustomer` ticket moves it to `WaitingOnSupport`; a `Support` message moves it to `WaitingOnCustomer`; closing a ticket sets `Closed` and appends a `System` message; a `Customer`/`Guest` message on a `Closed` ticket reopens it to `WaitingOnSupport`, appends a `System` "reopened" message, and leaves the prior `System` "closed" message unchanged (assert both messages remain in `SequenceNumber` order); `SequenceNumber` is strictly increasing per ticket; `IsUrgent` is `true` only for `TravelWithin24Hours`, `PaymentBookingMismatch`, `SupplierCancellationOrRelocation` and `TravellerSafety`.

In `SupportTicketServiceTests.cs` (against the SQLite fixture from Step 2 of Task at the persistence layer — construct `SupportDbContext` directly with `UseSqlite(connection)`), cover: `CreateTicketAsync` persists the ticket and its first message atomically; an authenticated `CustomerId` is stored while a guest ticket has `CustomerId == null`; `GetForCustomerAsync` returns `null` for a ticket owned by a different customer; `AddMessageAsync` throws/returns a domain error for an unknown ticket ID.

- [ ] **Step 3: Run the focused tests and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Support.Tests/ReadyToGoTravel.Support.Tests.csproj -c Release --filter FullyQualifiedName~SupportTicket`

Expected: compilation failure because none of the domain/service/context types exist yet.

- [ ] **Step 4: Implement the domain, service and persistence**

`SupportTicket` and `SupportTicketMessage` are `internal sealed` classes with private setters and factory/behavior methods only (`SupportTicket.Create(...)`, `ticket.Reply(...)`, `ticket.Close(staffSubjectId, now)`) — no public mutation surface beyond the `Application` layer. `SupportDbContext` sets `modelBuilder.HasDefaultSchema("support")`, calls `ApplyConfigurationsFromAssembly(typeof(SupportDbContext).Assembly)`, and overrides `SaveChanges`/`SaveChangesAsync` to throw `InvalidOperationException` if the change tracker contains a `Modified` or `Deleted` entry of type `SupportTicketMessage`, mirroring `BookingDbContext`'s version-immutability guard. `SupportEntityConfigurations.cs` maps `support_tickets`, `support_ticket_messages` (unique index on `(ticket_id, sequence_number)`), and `support_audit_events`, with snake_case columns, explicit `HasMaxLength` on all string columns, and a concurrency token on `support_tickets.row_version`. `SupportModule.AddSupportModule` registers `AddDbContext<SupportDbContext>(configureDatabase)`, `TimeProvider.System`, `SupportTicketService`, and `services.AddHealthChecks().AddDbContextCheck<SupportDbContext>("support_database", tags: ["ready"])`.

- [ ] **Step 5: Run the focused tests and verify GREEN**

Run the Step 3 command again and confirm all domain/service/persistence tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/ReadyToGoTravel.Support tests/ReadyToGoTravel.Support.Tests ReadyToGoTravel.slnx Directory.Packages.props
git commit -m "feat(support): add support module skeleton with ticket and message domain"
```

---

### Task 2: Guest magic-link issuance, validation, rotation and revocation

**Files:**
- Create: `src/ReadyToGoTravel.Support/Domain/SupportGuestAccessToken.cs`
- Create: `src/ReadyToGoTravel.Support/Guest/GuestAccessTokenGenerator.cs`
- Create: `src/ReadyToGoTravel.Support/Guest/GuestAccessTokenService.cs`
- Modify: `src/ReadyToGoTravel.Support/Persistence/SupportDbContext.cs`
- Modify: `src/ReadyToGoTravel.Support/Persistence/SupportEntityConfigurations.cs`
- Modify: `src/ReadyToGoTravel.Support/SupportModule.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/GuestAccessTokenTests.cs`

**Interfaces:**
- Produces: `GuestAccessTokenGenerator.Generate() : (string RawToken, string TokenHash)` — 32 bytes from `RandomNumberGenerator.GetBytes`, `Base64UrlEncode` (unpadded) for `RawToken`, `Convert.ToHexString(SHA256.HashData(...))` for `TokenHash`.
- Produces: `IGuestAccessTokenService.IssueAsync(Guid ticketId, TimeProvider time, CancellationToken ct) : Task<string>` (returns the raw token exactly once).
- Produces: `IGuestAccessTokenService.RotateAsync(Guid ticketId, CancellationToken ct) : Task<string>`, `IGuestAccessTokenService.RevokeAsync(Guid ticketId, CancellationToken ct) : Task`.
- Produces: `IGuestAccessTokenService.ResolveAsync(string rawToken, CancellationToken ct) : Task<Guid?>` — returns the resolved `TicketId` or `null`; records a `SupportAuditEvent` on both success and failure.
- Consumes: `TimeProvider` and `SupportDbContext` from Task 1.

- [ ] **Step 1: Write failing token lifecycle tests**

In `GuestAccessTokenTests.cs`, cover: `IssueAsync` returns a raw token whose `SHA256` hash matches the stored `token_hash`, and the raw value is never persisted anywhere; `ResolveAsync` with the freshly issued raw token returns the correct `TicketId`; `ResolveAsync` with a token that has never been issued returns `null`; `ResolveAsync` with a token 1 second past its `ExpiresAtUtc` (use a fake `TimeProvider` to advance time) returns `null`; `ResolveAsync` twice in a row with the same still-valid token both succeed (multi-use, not single-use); `RevokeAsync` then `ResolveAsync` with the same token returns `null`; `RotateAsync` invalidates the previous token (subsequent `ResolveAsync` with the old raw value returns `null`) and returns a new raw token that resolves to the same `TicketId`; `ResolveAsync` with a malformed/non-Base64Url string returns `null` without throwing; a token issued for ticket A never resolves against ticket B's data (assert by issuing tokens for two tickets and cross-checking `ResolveAsync` results); every `ResolveAsync` call (success and failure) writes exactly one `SupportAuditEvent` row with the correct `EventType`.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Support.Tests/ReadyToGoTravel.Support.Tests.csproj -c Release --filter FullyQualifiedName~GuestAccessToken`

Expected: compilation failure because the token generator/service/entity do not exist.

- [ ] **Step 3: Implement token generation, storage and resolution**

`SupportGuestAccessToken` maps to `support_guest_access_tokens` with a unique index on `token_hash`, columns `ticket_id`, `expires_at_utc`, `revoked_at_utc` (nullable), `rotated_from_token_id` (nullable self-reference), `last_used_at_utc` (nullable). `GuestAccessTokenService.IssueAsync` sets `ExpiresAtUtc = time.GetUtcNow().UtcDateTime.AddDays(30)`. `ResolveAsync` hashes the supplied raw value, looks up by `token_hash`, and returns `null` if no row matches, or the row is revoked, or `ExpiresAtUtc` is in the past; on a successful resolve it updates `LastUsedAtUtc` and writes a `SupportAuditEvent` of type `GuestLinkAuthenticated`; on failure (not found, expired, revoked) it writes `GuestLinkAuthenticationFailed` with no reference to the raw supplied value in the event's metadata. `RotateAsync` revokes the current active token (if any) and calls the same issuance path, setting `RotatedFromTokenId`. `RevokeAsync` sets `RevokedAtUtc` on the active token only, writing `GuestLinkRevoked`.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the Step 2 command again and confirm all token lifecycle tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/ReadyToGoTravel.Support tests/ReadyToGoTravel.Support.Tests
git commit -m "feat(support): add guest magic-link issuance, rotation and revocation"
```

---

### Task 3: Private attachment storage, scanning and upload/download authorization

**Files:**
- Create: `src/ReadyToGoTravel.Support/Domain/SupportAttachment.cs`
- Create: `src/ReadyToGoTravel.Support/Domain/AttachmentScanStatus.cs`
- Create: `src/ReadyToGoTravel.Support/Domain/AttachmentScanWork.cs`
- Create: `src/ReadyToGoTravel.Support/Storage/IObjectStorage.cs`
- Create: `src/ReadyToGoTravel.Support/Storage/S3ObjectStorage.cs`
- Create: `src/ReadyToGoTravel.Support/Storage/SupportStorageOptions.cs`
- Create: `src/ReadyToGoTravel.Support/Storage/AttachmentTypeValidator.cs`
- Create: `src/ReadyToGoTravel.Support/Scanning/IAttachmentScanner.cs`
- Create: `src/ReadyToGoTravel.Support/Scanning/ClamAvScanner.cs`
- Create: `src/ReadyToGoTravel.Support/Scanning/DisabledAttachmentScanner.cs`
- Create: `src/ReadyToGoTravel.Support/Scanning/ClamAvOptions.cs`
- Create: `src/ReadyToGoTravel.Support/Scanning/AttachmentScanProcessor.cs`
- Create: `src/ReadyToGoTravel.Support/Application/SupportAttachmentService.cs`
- Modify: `src/ReadyToGoTravel.Support/Persistence/SupportDbContext.cs`
- Modify: `src/ReadyToGoTravel.Support/Persistence/SupportEntityConfigurations.cs`
- Modify: `src/ReadyToGoTravel.Support/SupportModule.cs`
- Modify: `Directory.Packages.props` (add `AWSSDK.S3`)
- Test: `tests/ReadyToGoTravel.Support.Tests/AttachmentValidationTests.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/AttachmentUploadDownloadTests.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/AttachmentScanProcessorTests.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/InMemoryObjectStorage.cs` (test double)
- Test: `tests/ReadyToGoTravel.Support.Tests/RecordingAttachmentScanner.cs` (test double)

**Interfaces:**
- Produces: `IObjectStorage.PutAsync(string key, Stream content, string contentType, CancellationToken ct) : Task`, `IObjectStorage.CreateDownloadUrlAsync(string key, TimeSpan validFor, CancellationToken ct) : Task<Uri>`, `IObjectStorage.DeleteAsync(string key, CancellationToken ct) : Task`.
- Produces: `IAttachmentScanner.ScanAsync(Stream content, CancellationToken ct) : Task<AttachmentScanOutcome>` where `AttachmentScanOutcome { Clean, Infected, Unavailable }`.
- Produces: `AttachmentTypeValidator.Validate(string declaredContentType, ReadOnlySpan<byte> leadingBytes) : AttachmentTypeValidationResult` (`Accepted(string Extension)` or `Rejected(string Reason)`), covering `application/pdf` (`%PDF-`), `image/jpeg` (`FF D8 FF`), `image/png` (8-byte PNG signature), `text/plain` (strict UTF-8 decode, no NUL byte).
- Produces: `SupportAttachmentService.UploadAsync(Guid ticketId, Guid messageId, Guid? uploaderCustomerId, Guid? uploaderGuestTokenId, string originalFileName, string declaredContentType, Stream content, long sizeBytes, CancellationToken ct) : Task<SupportAttachment>` — enforces 10 MiB/file, 5 files/message, 50 MiB/ticket before calling `IObjectStorage.PutAsync`.
- Produces: `SupportAttachmentService.CreateDownloadUrlAsync(Guid attachmentId, CancellationToken ct) : Task<Uri?>` — returns `null` unless `ScanStatus == Clean`.
- Consumes: `SupportDbContext`, `TimeProvider` from Task 1.

- [ ] **Step 1: Write failing type-validation tests**

In `AttachmentValidationTests.cs`, cover: correct magic bytes for each of PDF/JPEG/PNG/UTF-8-text are accepted with the expected extension; a `.pdf`-declared file whose bytes start with the Windows PE header (`MZ`) is rejected; a declared `image/jpeg` with PNG bytes is rejected; text content containing a NUL byte is rejected; a declared content type outside the four allowed values is rejected regardless of bytes; an empty byte span is rejected.

- [ ] **Step 2: Run and verify RED, then implement `AttachmentTypeValidator`**

Run: `dotnet test tests/ReadyToGoTravel.Support.Tests/ReadyToGoTravel.Support.Tests.csproj -c Release --filter FullyQualifiedName~AttachmentValidationTests` — expect compilation failure. Implement `AttachmentTypeValidator` as a static class comparing the declared content type against a `FrozenDictionary<string, (byte[] Signature, string Extension)>` and validating the signature/UTF-8 rule per type. Re-run and confirm GREEN.

- [ ] **Step 3: Write failing upload/limit/download-authorization tests**

In `AttachmentUploadDownloadTests.cs` (using `InMemoryObjectStorage`, a dictionary-backed test double implementing `IObjectStorage`, and `RecordingAttachmentScanner` returning a configurable `AttachmentScanOutcome`), cover: uploading a valid 1 MiB PDF succeeds and creates a `Pending`-status row; uploading an 11 MiB file is rejected before any storage call; uploading a 6th file to the same message is rejected; uploading a file that pushes the ticket's cumulative size past 50 MiB is rejected even though the individual file and message-count limits pass; the storage key never contains the original filename (assert it matches `support/{ticketId}/{attachmentId}{extension}`); `CreateDownloadUrlAsync` returns `null` while `ScanStatus == Pending`; `CreateDownloadUrlAsync` returns a non-null `Uri` only after the row is updated to `Clean`; `CreateDownloadUrlAsync` for an `Infected` or `Failed` row returns `null`; the returned download URL's validity window, as recorded by `InMemoryObjectStorage`, is at most 5 minutes.

- [ ] **Step 4: Run and verify RED, then implement `IObjectStorage`, `S3ObjectStorage` and `SupportAttachmentService`**

Run the Step 3 filter and expect compilation failure. Implement `S3ObjectStorage` using `AWSSDK.S3`'s `IAmazonS3` (`PutObjectAsync`, `GetPreSignedURLAsync` with `Expires = now + validFor`, `DeleteObjectAsync`), constructed from `SupportStorageOptions` (`ServiceUrl`, `AccessKey`, `SecretKey`, `BucketName`, `ForcePathStyle`) so the same client code targets MinIO locally (`ForcePathStyle = true`) and AWS S3 `ap-southeast-2` in production. Implement `SupportAttachmentService` to compute the running per-message and per-ticket byte totals via a query before accepting a new file, generate the storage key from a fixed extension map (never from `originalFileName`), call `AttachmentTypeValidator.Validate` first, then `IObjectStorage.PutAsync`, then persist the `SupportAttachment` row with `ScanStatus = Pending` and enqueue one `AttachmentScanWork` row in the same transaction. Re-run and confirm GREEN.

- [ ] **Step 5: Write failing scan-pipeline tests**

In `AttachmentScanProcessorTests.cs`, cover: a `Clean` scan result updates `ScanStatus` to `Clean` and completes the work row; an `Infected` result updates `ScanStatus` to `Infected`, calls `IObjectStorage.DeleteAsync` for that key, and writes a `SupportAuditEvent`; an `Unavailable` result retries with the same bounded exponential backoff used by the Booking outbox processors and does not mark the attachment `Clean`; after 8 exhausted attempts the row becomes `Failed` and a deduplicated `SupportAuditEvent` is written, matching the Slice 5 `OperationalCase` exhaustion behavior; a claimed-but-expired lease is reclaimable by a different `workerId`; two concurrent claims of the same due row result in exactly one winner (assert via `ExecuteUpdateAsync`'s affected-row count).

- [ ] **Step 6: Run and verify RED, then implement `IAttachmentScanner`, `ClamAvScanner`, `DisabledAttachmentScanner` and `AttachmentScanProcessor`**

Run the Step 5 filter and expect compilation failure. Implement `ClamAvScanner` as a `TcpClient`-based `INSTREAM` protocol client: connect to `ClamAvOptions.Host`/`Port`, write `b"zINSTREAM\0"`, stream the content in `<= 2048`-byte chunks each prefixed by a 4-byte big-endian length, terminate with a 4-byte zero-length chunk, read the response line, and map `"stream: OK"` to `Clean`, a response containing `"FOUND"` to `Infected`, and any I/O exception/timeout to `Unavailable`. Implement `DisabledAttachmentScanner.ScanAsync` to always return `Unavailable` (the registered default). Implement `AttachmentScanProcessor.ProcessNextAsync(string workerId, CancellationToken ct) : Task<bool>` following the exact claim-lease-`ExecuteUpdateAsync` pattern documented for `WebhookInboxProcessor` in `src/ReadyToGoTravel.Booking/Webhooks/WebhookInboxProcessor.cs` (2-minute lease, `MaxAttempts = 8`, `Math.Min(60, Math.Pow(2, Math.Min(attempts, 5)))`-minute backoff). Re-run and confirm GREEN.

- [ ] **Step 7: Wire storage/scanning registration into `SupportModule`**

`AddSupportModule` binds `SupportStorageOptions` and `ClamAvOptions` with `AddOptions<T>().Validate(...).ValidateOnStart()` (require non-empty `BucketName`/`ServiceUrl`; require `ClamAvOptions.Enabled == false` unless `Host`/`Port` are set), registers `IObjectStorage` as `S3ObjectStorage`, registers `IAttachmentScanner` as `ClamAvOptions.Enabled ? ClamAvScanner : DisabledAttachmentScanner`, and registers `AttachmentScanProcessor` and `SupportAttachmentService`.

- [ ] **Step 8: Commit**

```bash
git add src/ReadyToGoTravel.Support tests/ReadyToGoTravel.Support.Tests Directory.Packages.props
git commit -m "feat(support): add private attachment storage, validation and fail-closed scanning"
```

---

### Task 4: Notification outbox for ticket acknowledgements and updates

**Files:**
- Create: `src/ReadyToGoTravel.Support/Notifications/SupportNotificationOutboxItem.cs`
- Create: `src/ReadyToGoTravel.Support/Notifications/ISupportNotificationSender.cs`
- Create: `src/ReadyToGoTravel.Support/Notifications/DisabledSupportNotificationSender.cs`
- Create: `src/ReadyToGoTravel.Support/Notifications/SupportNotificationOutboxProcessor.cs`
- Modify: `src/ReadyToGoTravel.Support/Application/SupportTicketService.cs`
- Modify: `src/ReadyToGoTravel.Support/Guest/GuestAccessTokenService.cs`
- Modify: `src/ReadyToGoTravel.Support/Persistence/SupportDbContext.cs`
- Modify: `src/ReadyToGoTravel.Support/Persistence/SupportEntityConfigurations.cs`
- Modify: `src/ReadyToGoTravel.Support/SupportModule.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/SupportNotificationTests.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/RecordingSupportNotificationSender.cs` (test double)

**Interfaces:**
- Produces: `ISupportNotificationSender.SendAsync(SupportNotification message, CancellationToken ct) : Task` where `SupportNotification(string RecipientEmail, string Template, IReadOnlyDictionary<string, string> Fields)`.
- Produces: `ISupportNotificationOutboxProcessor.ProcessNextAsync(string workerId, CancellationToken ct) : Task<bool>`.
- Consumes: `SupportDbContext`, `TimeProvider` from Task 1; ticket/message writes from `SupportTicketService` (Task 1) and token issuance from `GuestAccessTokenService` (Task 2).

- [ ] **Step 1: Write failing outbox tests**

In `SupportNotificationTests.cs`, cover: `CreateTicketAsync` for a guest-created ticket enqueues exactly one outbox row with template `TicketAcknowledgementGuest` whose `Fields` include the raw magic-link token exactly once, in the same transaction as the ticket insert (assert via a SQLite fixture that both rows exist after a single `SaveChangesAsync`, and that a forced failure after the ticket insert but before commit leaves neither row persisted); `CreateTicketAsync` for an authenticated customer enqueues `TicketAcknowledgementCustomer` with no token field; every accepted `AddMessageAsync` call enqueues an update notification; the outbox's unique key is `(ticket_id, message_id, channel)`, so calling `AddMessageAsync` logic twice for the same message never double-enqueues; `RecordingSupportNotificationSender` receives the enqueued item after `ProcessNextAsync`; a sender exception leaves the item retryable with the same bounded backoff as Task 3's scan processor; after 8 exhausted attempts the item is marked `Failed` and inspectable, never silently dropped; a completed outbox item is never reprocessed by a second `ProcessNextAsync` call.

- [ ] **Step 2: Run and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Support.Tests/ReadyToGoTravel.Support.Tests.csproj -c Release --filter FullyQualifiedName~SupportNotificationTests`

Expected: compilation failure because the outbox/sender/processor types do not exist.

- [ ] **Step 3: Implement the transactional outbox and worker processor**

Add outbox-row creation inside `SupportTicketService.CreateTicketAsync` and `AddMessageAsync` using the same `DbContext` instance/`SaveChangesAsync` call as the domain write (no second `SaveChanges`). `GuestAccessTokenService.IssueAsync`, when called from ticket creation, returns the raw token to the caller, which passes it into the notification `Fields` dictionary — the raw token is never stored in `SupportNotificationOutboxItem`'s own persisted columns beyond that one `Fields` payload needed to render the email, and `SupportNotificationOutboxItem.Fields` is mapped as an owned JSON column excluded from any customer-facing read model. `SupportNotificationOutboxProcessor.ProcessNextAsync` follows the identical claim-lease pattern as Task 3's `AttachmentScanProcessor`. `DisabledSupportNotificationSender.SendAsync` throws a `SupportNotificationUnavailableException`, causing every delivery attempt to retry and eventually fail closed, matching the Slice 5 `ICustomerNotificationSender` posture.

- [ ] **Step 4: Run and verify GREEN**

Run the Step 2 command again and confirm all notification tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/ReadyToGoTravel.Support tests/ReadyToGoTravel.Support.Tests
git commit -m "feat(support): add transactional notification outbox for ticket updates"
```

---

### Task 5: Authenticated, guest and staff HTTP API

**Files:**
- Create: `src/ReadyToGoTravel.Support/Http/SupportContracts.cs`
- Create: `src/ReadyToGoTravel.Support/Http/SupportHttpResults.cs`
- Create: `src/ReadyToGoTravel.Support/Http/SupportEndpoints.cs`
- Create: `src/ReadyToGoTravel.Support/Http/SupportGuestEndpoints.cs`
- Create: `src/ReadyToGoTravel.Support/Http/SupportStaffEndpoints.cs`
- Create: `src/ReadyToGoTravel.Api/Infrastructure/SupportAgentAuthorizationHandler.cs`
- Modify: `src/ReadyToGoTravel.Api/Infrastructure/AuthenticationExtensions.cs`
- Modify: `src/ReadyToGoTravel.Api/Program.cs`
- Modify: `src/ReadyToGoTravel.Api/appsettings.json`
- Modify: `src/ReadyToGoTravel.Api/appsettings.Development.json`
- Modify: `src/ReadyToGoTravel.Worker/Program.cs`
- Modify: `src/ReadyToGoTravel.Worker/Worker.cs`
- Modify: `src/ReadyToGoTravel.Worker/ReadyToGoTravel.Worker.csproj`
- Modify: `deploy/local/compose.yaml`
- Modify: `deploy/local/.env.example`
- Modify: `deploy/local/keycloak/rtgt-realm.json`
- Test: `tests/ReadyToGoTravel.Support.Tests/TestAuthenticationHandler.cs` (extend to set `realm_access` role claims)
- Test: `tests/ReadyToGoTravel.Support.Tests/SupportApiTests.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/SupportGuestApiTests.cs`
- Test: `tests/ReadyToGoTravel.Support.Tests/SupportStaffApiTests.cs`
- Test: `tests/ReadyToGoTravel.Architecture.Tests/SolutionConventionsTests.cs`

**Interfaces:**
- Produces routes: `POST /api/v1/support/tickets` (optional-auth; anonymous requires `ContactName`/`ContactEmail` in body, authenticated ignores any supplied email and uses the account's), `GET /api/v1/support/tickets` (`RequireAuthorization("consumer")`), `GET /api/v1/support/tickets/{ticketId}` (owner-only 404-on-mismatch), `POST /api/v1/support/tickets/{ticketId}/messages`, `POST /api/v1/support/tickets/{ticketId}/attachments` (multipart), `GET /api/v1/support/tickets/{ticketId}/attachments/{attachmentId}/download`.
- Produces routes: `GET /api/v1/support/guest/ticket`, `POST /api/v1/support/guest/ticket/messages`, `POST /api/v1/support/guest/ticket/attachments`, `GET /api/v1/support/guest/ticket/attachments/{attachmentId}/download` — every one reads `Authorization: Bearer <token>` manually (no `RequireAuthorization`), resolves `TicketId` via `IGuestAccessTokenService.ResolveAsync`, and 401s before touching any route parameter.
- Produces routes: `GET /api/v1/support/staff/tickets`, `GET /api/v1/support/staff/tickets/{ticketId}`, `POST /api/v1/support/staff/tickets/{ticketId}/messages`, `POST /api/v1/support/staff/tickets/{ticketId}/close`, `POST /api/v1/support/staff/tickets/{ticketId}/guest-link/rotate`, `POST /api/v1/support/staff/tickets/{ticketId}/guest-link/revoke` — all `RequireAuthorization("support-agent")`.
- Produces: `AddPolicy("support-agent", ...)` in `AuthenticationExtensions.cs` using a new `SupportAgentAuthorizationHandler : AuthorizationHandler<SupportAgentRequirement>` that parses the `realm_access` claim's JSON `roles` array for `"support-agent"`.
- Produces rate-limit policies in `Program.cs`: `"support"` (60/min/IP, authenticated customer/staff routes), `"support-guest"` (20/min/IP, guest routes), `"support-ticket-create"` (5/min/IP, the anonymous-reachable creation route).
- Consumes: `SupportTicketService`, `SupportAttachmentService`, `IGuestAccessTokenService` from Tasks 1–3.

- [ ] **Step 1: Write failing authenticated-endpoint tests**

In `SupportApiTests.cs` (extend `TestApplication` from `BookingApiTests.cs`'s pattern to also call `AddSupportModule`), cover: an authenticated customer creating a ticket ignores a client-supplied email and stores the account's; `GET /tickets/{ticketId}` for the owner returns `200` with the thread; for another authenticated customer returns `404` with `code: "ticket_not_found"`; anonymous `GET /tickets` returns `401`; posting a reply as the owner transitions status and appends a message; the response body never includes `TokenHash`, raw tokens, storage keys, or `SupportAuditEvent` rows.

- [ ] **Step 2: Write failing guest-endpoint tests**

In `SupportGuestApiTests.cs`, cover: a valid bearer token resolves and returns only that ticket's thread; a missing `Authorization` header returns `401`; an expired token returns `401`; a revoked token returns `401`; a token for ticket A cannot read or reply to ticket B even if an attacker guesses ticket B's UUID in a would-be route parameter (assert the guest routes have no `ticketId` route parameter at all — the token is the only input); a guest reply on a `Closed` ticket reopens it; uploading and then downloading an attachment through the guest routes works end-to-end with a `RecordingAttachmentScanner` configured to return `Clean`.

- [ ] **Step 3: Write failing staff-endpoint tests**

In `SupportStaffApiTests.cs`, cover: a caller without the `support-agent` role gets `403` from every staff route; a caller with the role can list and view any ticket regardless of owner; a staff reply moves status to `WaitingOnCustomer`; `POST .../close` moves status to `Closed`; `POST .../guest-link/rotate` returns no raw token in the HTTP response (it only triggers an email) and invalidates the previous token (assert via a follow-up guest-route call with the old token failing); `POST .../guest-link/revoke` likewise leaves the guest route inaccessible afterward.

- [ ] **Step 4: Run all three filters and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Support.Tests/ReadyToGoTravel.Support.Tests.csproj -c Release --filter FullyQualifiedName~SupportApiTests|FullyQualifiedName~SupportGuestApiTests|FullyQualifiedName~SupportStaffApiTests`

Expected: `404`/compilation failures because no Support routes exist yet.

- [ ] **Step 5: Implement contracts, endpoints and authorization wiring**

`SupportContracts.cs` holds request/response records (`CreateTicketRequest`, `TicketResponse`, `MessageResponse`, `AttachmentResponse`, `DownloadUrlResponse`) that never expose `TokenHash`, storage keys or audit data. `SupportHttpResults.cs` provides `Problem(HttpContext, int status, string code)` matching `BookingHttpResults`'s shape. `SupportEndpoints.MapSupportEndpoints(RouteGroupBuilder group)` reads `principal.Identity?.IsAuthenticated` to branch between the authenticated and anonymous ticket-creation paths within the same handler. `SupportGuestEndpoints.MapSupportGuestEndpoints` extracts and validates the bearer header manually (no ASP.NET auth scheme), exactly like `WebhookEndpoints.cs`'s header handling, then calls `IGuestAccessTokenService.ResolveAsync` before any other logic. `SupportStaffEndpoints.MapSupportStaffEndpoints` applies `.RequireAuthorization("support-agent")` at the group level. Add the three named rate-limit policies to `Program.cs`'s existing `AddRateLimiter` block alongside `"public-api"`/`"search"`/`"checkout"`/`"webhook"`. Add `SupportAgentAuthorizationHandler` registered via `services.AddSingleton<IAuthorizationHandler, SupportAgentAuthorizationHandler>()` and the `"support-agent"` policy in `AuthenticationExtensions.cs`. Add a `support-agent` realm role to `deploy/local/keycloak/rtgt-realm.json`. Add a `minio` service (image `minio/minio`, `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` from `.env`, `command: server /data --console-address ":9001"`, health-checked) to `deploy/local/compose.yaml` and matching variables to `.env.example`. Wire `AddSupportModule(...)` into `Program.cs` next to the other module registrations, using `ConnectionStrings:Support` (falling back to the same database as Consumer/Booking, matching the existing single-database local convention) and `Support:Storage`/`Support:Scanning` configuration sections; checked-in `appsettings.json` sets `Support:Scanning:Enabled = false` (registers `DisabledAttachmentScanner`) and `appsettings.Development.json` may enable it against the local MinIO/ClamAV compose services. Extend `src/ReadyToGoTravel.Worker/Program.cs`/`Worker.cs` to also call `AddSupportModule(...)` and run `AttachmentScanProcessor`/`SupportNotificationOutboxProcessor` cycles alongside the existing webhook/reconciliation/notification cycles.

- [ ] **Step 6: Run all three filters and verify GREEN**

Re-run the Step 4 command and confirm all authenticated, guest and staff endpoint tests pass.

- [ ] **Step 7: Add the architecture test for the new module boundary**

In `SolutionConventionsTests.cs`, add `SupportProjectFollowsReadyToGoTravelPrefix`-style coverage (it likely already passes via the existing generic `ProjectNamesUseReadyToGoTravelPrefix`/`AllProjectsTargetNet10` checks once `ReadyToGoTravel.Support.csproj` exists — run the full architecture test project and fix any new failure rather than adding a redundant test if the generic checks already cover it).

- [ ] **Step 8: Commit**

```bash
git add src/ReadyToGoTravel.Support src/ReadyToGoTravel.Api src/ReadyToGoTravel.Worker deploy/local tests/ReadyToGoTravel.Support.Tests tests/ReadyToGoTravel.Architecture.Tests
git commit -m "feat(support): add authenticated, guest and staff support ticket API"
```

---

### Task 6: Blazor UI — customer, guest and staff support pages

**Files:**
- Create: `src/ReadyToGoTravel.Web/Client/SupportApiClient.cs`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/Support.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/SupportNew.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/SupportTicket.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/SupportGuestTicket.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/StaffSupportQueue.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/StaffSupportTicket.razor`
- Modify: `src/ReadyToGoTravel.Web/Program.cs`
- Test: `tests/ReadyToGoTravel.Web.Tests/SupportApiClientTests.cs`
- Test: `tests/ReadyToGoTravel.Architecture.Tests/WebBoundaryTests.cs`

**Interfaces:**
- Produces: `SupportApiClient` typed `HttpClient` with `CreateTicketAsync`, `GetTicketsAsync`, `GetTicketAsync(Guid)`, `ReplyAsync(Guid, string)`, `UploadAttachmentAsync(Guid, IBrowserFile)`, `GetDownloadUrlAsync(Guid, Guid)` (customer-authenticated, uses `ApiAccessTokenHandler`) plus a separate `SupportGuestApiClient` with the same shape but taking the raw token instead of relying on the bearer-token handler.
- Consumes: `/api/v1/support/...` routes from Task 5.

- [ ] **Step 1: Write failing typed-client tests**

In `SupportApiClientTests.cs` (following `ConsumerApiClient`'s existing test pattern against a fake `HttpMessageHandler`), cover: `CreateTicketAsync` posts to `/api/v1/support/tickets`; a non-success response yields a fail-soft `null`/empty result rather than throwing; `SupportGuestApiClient` sends `Authorization: Bearer {token}` and never sends the platform's own Keycloak access token.

- [ ] **Step 2: Run and verify RED**

Run: `dotnet test tests/ReadyToGoTravel.Web.Tests/ReadyToGoTravel.Web.Tests.csproj -c Release --filter FullyQualifiedName~SupportApiClientTests`

Expected: compilation failure because `SupportApiClient` does not exist.

- [ ] **Step 3: Implement the typed clients and register them**

Follow `ConsumerApiClient.cs`'s exact shape: constructor takes `HttpClient`, each method wraps `GetFromJsonAsync`/`PostAsJsonAsync`/`PostAsync` (multipart for attachments) in `try/catch (HttpRequestException or TaskCanceledException)`. Register in `Program.cs`: `builder.Services.AddHttpClient<SupportApiClient>(client => ConfigureApiClient(client, apiBaseUrl)).AddHttpMessageHandler<ApiAccessTokenHandler>();` and a second `AddHttpClient<SupportGuestApiClient>(...)` without the token handler (it attaches the raw magic-link token per-call instead). Re-run Step 2's command and confirm GREEN.

- [ ] **Step 4: Implement the customer-facing pages**

`Support.razor` (`@page "/support"`, `@attribute [Authorize]`, `@attribute [StreamRendering]`) lists the authenticated customer's tickets with status and urgency. `SupportNew.razor` (`@page "/support/new"`, no `[Authorize]` — reachable by both authenticated and anonymous visitors) uses `EditForm`+`DataAnnotationsValidator`+`SupplyParameterFromForm`, pre-filling and disabling the email field when `HttpContext.User.Identity?.IsAuthenticated == true`. `SupportTicket.razor` (`@page "/support/{TicketId:guid}"`, `@attribute [Authorize]`) shows the thread, a reply form and an attachment upload/download control using `InputFile` bounded to the 10 MiB/file client-side hint (the server remains the authority on limits).

- [ ] **Step 5: Implement the guest page**

`SupportGuestTicket.razor` (`@page "/support/guest/{Token}"`, no `[Authorize]`) reads `Token` from the route, calls `SupportGuestApiClient` with it for every action on the page, and never calls the authenticated `SupportApiClient`. On a `401` from any guest call, the page shows a "this link is no longer valid" message rather than an unhandled error.

- [ ] **Step 6: Implement the staff pages**

`StaffSupportQueue.razor` (`@page "/staff/support"`, `@attribute [Authorize(Policy = "support-agent")]`) lists open tickets filterable by status/urgency. `StaffSupportTicket.razor` (`@page "/staff/support/{TicketId:guid}"`, same policy) shows the full thread, a reply form, a close button, and rotate/revoke controls for the guest link — none of which ever display the raw token.

- [ ] **Step 7: Add the Web architecture test for the new client**

In `WebBoundaryTests.cs`, add `WebConsumesSupportThroughThePublicV1Api` asserting `SupportApiClient.cs`'s source contains only `/api/v1/support/...` route literals, matching the existing per-client convention.

- [ ] **Step 8: Start the app and exercise the golden path manually**

Run the local stack per `docs/deployment/local-development.md` (Postgres, Keycloak, and the newly added MinIO compose service), apply the Task 7 migration, start `ReadyToGoTravel.Api` and `ReadyToGoTravel.Web`, and walk through: creating a ticket as an anonymous guest, receiving the acknowledgement in the recording/disabled sender's logs (no live email is sent), opening `/support/guest/{token}` with the logged token, replying, uploading a small PDF, confirming it is not downloadable until the scan work item is processed (or use `Support:Scanning:Enabled=false` locally and confirm it consistently reports unavailable rather than downloadable), then repeating the flow as an authenticated customer and as a `support-agent`-role user. Record exactly what was and was not exercised this way in the Task 8 outcome report — do not claim browser verification that was not actually performed.

- [ ] **Step 9: Commit**

```bash
git add src/ReadyToGoTravel.Web tests/ReadyToGoTravel.Web.Tests tests/ReadyToGoTravel.Architecture.Tests
git commit -m "feat(support): add customer, guest and staff support Blazor pages"
```

---

### Task 7: Persistence migration and PostgreSQL guarantees

**Files:**
- Create: `src/ReadyToGoTravel.Support/Persistence/Migrations/20260809010000_InitialSupportSchema.cs`
- Create: `src/ReadyToGoTravel.Support/Persistence/Migrations/20260809010000_InitialSupportSchema.Designer.cs`
- Create: `src/ReadyToGoTravel.Support/Persistence/Migrations/SupportDbContextModelSnapshot.cs`
- Modify: `tests/ReadyToGoTravel.Support.Tests/SupportPersistenceTests.cs`

**Interfaces:**
- Adds `support.support_tickets`, `support.support_ticket_messages`, `support.support_guest_access_tokens`, `support.support_attachments`, `support.attachment_scan_work`, `support.support_notification_outbox`, `support.support_audit_events` with all indexes described in Tasks 1–4.
- Installs a PostgreSQL trigger `support.reject_support_ticket_message_mutation()` rejecting `UPDATE`/`DELETE` on `support_ticket_messages`, mirroring `booking.reject_booking_version_mutation()`.

- [ ] **Step 1: Write failing persistence/model tests**

In `SupportPersistenceTests.cs`, assert: the unique index on `(ticket_id, sequence_number)` for messages; the unique index on `token_hash`; a direct SQL `UPDATE`/`DELETE` against `support_ticket_messages` (against a real PostgreSQL test database, matching how `BookingPersistenceTests.cs` exercises its trigger) is rejected by the trigger; all string columns have explicit `HasMaxLength`; no column exposes a raw token or storage credential; `dotnet ef migrations has-pending-model-changes` reports nothing once the migration is generated.

- [ ] **Step 2: Generate and normalize the migration**

Run: `dotnet ef migrations add InitialSupportSchema --project src/ReadyToGoTravel.Support --startup-project src/ReadyToGoTravel.Api --context SupportDbContext --output-dir Persistence/Migrations`

Rename the generated migration deterministically to `20260809010000`, add the PostgreSQL trigger/function SQL via `migrationBuilder.Sql(...)` in `Up` (with matching `DROP TRIGGER`/`DROP FUNCTION` in `Down`), and keep the designer/snapshot identifiers aligned with the renamed migration.

- [ ] **Step 3: Run persistence and EF parity checks**

Run the Support persistence tests, then: `dotnet ef migrations has-pending-model-changes --project src/ReadyToGoTravel.Support --startup-project src/ReadyToGoTravel.Api --context SupportDbContext` and confirm no pending changes.

- [ ] **Step 4: Run a live PostgreSQL upgrade drill from the current Slice 5 schema**

Start a local PostgreSQL 17 instance from `deploy/local/compose.yaml`, apply every migration through `20260808010000_Slice5BookingReconciliation` (Consumer + Booking), insert one authenticated-customer ticket row and one guest ticket row directly via SQL to simulate pre-Slice-6 absence, apply `20260809010000_InitialSupportSchema`, and confirm the schema, indexes and trigger exist and reject a manual `UPDATE`/`DELETE` against `support_ticket_messages`. Record the exact commands and output in the Task 8 outcome report.

- [ ] **Step 5: Commit**

```bash
git add src/ReadyToGoTravel.Support/Persistence/Migrations tests/ReadyToGoTravel.Support.Tests/SupportPersistenceTests.cs
git commit -m "feat(support): add InitialSupportSchema migration with immutability trigger"
```

---

### Task 8: Documentation, full verification and PR

**Files:**
- Modify: `README.md`
- Modify: `docs/README.md`
- Modify: `docs/api/README.md`
- Modify: `docs/domain/README.md`
- Create: `docs/domain/support-tickets-and-guest-access.md`
- Modify: `docs/decisions/review-register.md`
- Modify: `docs/plans/active/PLAN-0002-mvp-delivery.md`
- Modify: `docs/delivery/README.md`
- Create: `docs/delivery/2026-08-09-slice-6-support-tickets-magic-links-outcome.md`
- Modify: `docs/deployment/local-development.md`
- Modify: `docs/architecture/trust-and-data-boundaries.md` (only if any stated boundary changed; otherwise confirm it already matches and leave unmodified)

- [ ] **Step 1: Write the domain documentation and update indexes**

`docs/domain/support-tickets-and-guest-access.md` documents the ticket state model, the message thread, the guest token lifecycle and the attachment scan gate for future readers, cross-linking OI-0012 and ADR-0010. Add it to `docs/domain/README.md`'s list. Update `docs/api/README.md` with the new `/api/v1/support/...` route families. Update `docs/deployment/local-development.md` with the MinIO (and, if enabled locally, ClamAV) compose service and the `dotnet ef database update` command for `SupportDbContext`. Add a review-register row for object-storage/malware-scanning production activation, matching the existing table's shape. Update `docs/plans/active/PLAN-0002-mvp-delivery.md`'s Slice Sequence (`[x] Slice 6`), Current Plan and Change Log sections once the implementation below is complete and verified — not before.

- [ ] **Step 2: Run focused and full verification**

Run:

```bash
dotnet restore ReadyToGoTravel.slnx --locked-mode
dotnet format ReadyToGoTravel.slnx --no-restore --verify-no-changes
dotnet build ReadyToGoTravel.slnx -c Release --no-restore
dotnet test ReadyToGoTravel.slnx -c Release --no-build --no-restore
dotnet ef migrations has-pending-model-changes --project src/ReadyToGoTravel.Support --startup-project src/ReadyToGoTravel.Api --context SupportDbContext
bash scripts/validate-docs.sh
git diff --check
docker build -f src/ReadyToGoTravel.Api/Dockerfile .
docker build -f src/ReadyToGoTravel.Web/Dockerfile .
docker build -f src/ReadyToGoTravel.Worker/Dockerfile .
docker build -f src/ReadyToGoTravel.FlightReconciliation.Worker/Dockerfile .
```

Fix any failure by correcting the implementation (never by weakening a test or skipping a check) and re-run until every command passes cleanly.

- [ ] **Step 3: Review the complete diff against Slice 6 risks**

Inspect: guest-token entropy/hash-only storage, IDOR on every guest/attachment route, storage-key path-traversal safety, fail-closed scan/production defaults, raw-token exposure in responses/logs, staff-role enforcement, cancellation propagation, duplicate side effects in the outbox/scan processors, dead code, and documentation consistency. Reproduce any valid concern with a failing test before fixing it, per the repository's existing review discipline.

- [ ] **Step 4: Write the outcome report and finalize PLAN-0002**

`docs/delivery/2026-08-09-slice-6-support-tickets-magic-links-outcome.md` follows the Slice 5 outcome report's shape exactly: Outcome, ticket/thread behavior, guest magic-link security model, attachment storage/scanning model, staff console/authorization, database/configuration, the exact verification record (including the live PostgreSQL upgrade drill output and precisely what manual/browser verification was and was not performed), and production gates/exclusions. Add it to `docs/delivery/README.md`'s list. Only then mark Slice 6 complete in `docs/plans/active/PLAN-0002-mvp-delivery.md`.

- [ ] **Step 5: Push and open the pull request**

Push `codex/slice-6-support-magic-links` and open a non-draft PR to `dev` describing scope, the guest magic-link and attachment security models, migration name, tests added, and unchanged/new production gates. Do not merge.

## Plan self-review

- Spec coverage: ticket lifecycle, guest magic links (issuance/expiry/rotation/revocation/replay/scope), private attachments (validation/storage/scanning/download authorization), staff console, notifications, persistence/migration, API, Blazor UI, tests and documentation each have a task.
- Placeholder scan: no TBD/TODO or undefined follow-up remains; every step names exact files, types and scenarios rather than deferring detail.
- Type consistency: `IObjectStorage`, `IAttachmentScanner`, `IGuestAccessTokenService`, `ISupportNotificationSender` and `SupportTicketService`/`SupportAttachmentService` signatures introduced in Tasks 1–4 are the exact names consumed by Tasks 5–6.
