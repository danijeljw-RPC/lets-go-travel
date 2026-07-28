<!-- markdownlint-disable MD013 -->

# Reliability and Supportability

## Failure Assumptions

Assume supplier timeouts, stale offers, price changes, partial failure, pending confirmation, duplicate customer actions, duplicate or missing webhooks, mobile interruption and third-party outages.

## Search and Booking

Search is high-volume, short-lived and replaceable. Booking is durable, financially sensitive and unsafe to repeat without controls. Give them separate telemetry, concurrency and scaling policies.

## Timeout and Retry

Every external operation has an explicit timeout. Retry only when operation semantics and idempotency make it safe. After a high-impact timeout, query current state before repeating the action.

Timeouts are operation-specific for reference lookup, search, prebook, payment-session creation, booking, retrieval and cancellation. Retry policy considers supplier guarantees, received response, request key and current booking/payment state; booking and payment are never retried blindly.

## Circuit Breaking and Degradation

During supplier failure, stop overwhelming the dependency, return stable platform errors, preserve customer context, avoid presenting stale bookable prices as current, keep existing trips/documents available where possible, expose a useful retry path and alert operations.

## Durable Messaging

Record business events transactionally with state changes and dispatch them later through an outbox or equivalent. Persist incoming webhooks to an inbox with event identity, authentication result, hash, processing status, attempts and failure reason.

Durable events include booking/change/cancellation/refund outcomes, notifications, document generation and reconciliation requests. The webhook inbox also records receipt time, booking correlation and replay state.

## Observability

Measure request latency/errors, supplier errors, quota use, search-to-prebook and prebook-to-book ratios, confirmation time, pending bookings, payment mismatches, reconciliation age, webhook lag, notification failures, refund age and support-required bookings.

## Support View

Support needs platform and supplier references, current booking/payment/cancellation state, last supplier check, version timeline, meaningful diffs, failed operations, correlation IDs, notification history and permitted next actions with sensitive values masked.

## Ticket Support

The initial customer-support channel is the first-party asynchronous ticket model accepted in closed [OI-0012](../issues/closed/OI-0012-customer-support-model.md). Authenticated customers and guests can create tickets containing name, email, optional booking/customer reference, a required category, an initial message and permitted attachments. For an authenticated customer, the form uses the authoritative account email and does not allow it to be edited.

Every ticket starts as `New`. An accepted customer reply changes `New` or `Waiting on Customer` to `Waiting on Support`. An accepted support response changes `New` or `Waiting on Support` to `Waiting on Customer`. An authorised support user explicitly sets `Closed`. An accepted customer reply to a closed ticket reopens the same thread as `Waiting on Support`, preserves the closure event and queues the ordinary support notification.

Each ticket has an internal UUIDv7 identifier and a separate cryptographically random bearer token for guest access. Only a one-way hash of the bearer token is stored. The UUIDv7 is not an access secret. The emailed magic link can be revoked or rotated, grants access only to its ticket and returns no ticket existence or customer information when it is invalid, expired or revoked.

Ticket messages form an immutable thread. Every accepted customer or support update persists before it queues a durable, retryable and deduplicated email notification to the ticket email address. Email failure remains visible to support and does not roll back the thread entry. Messages must not place sensitive attachments, passport data or unnecessary booking details in email.

Attachments are private objects in an S3-compatible store. Thread entries retain an object reference and safe display metadata rather than a permanently public URL. After ticket authorisation, the platform returns short-lived signed access or streams the object. Upload handling enforces allowlisted types, size/count limits, non-executable content disposition and malware scanning or quarantine before download. A failed attachment does not discard an otherwise accepted text message and can be retried safely.

Published service hours, urgent-travel criteria, supplier escalation contacts and SLAs remain production-readiness configuration informed by OI-0002 and OI-0004. A later third-party live-chat adapter may append to or create tickets through a controlled support contract, but live chat is not part of the initial product and cannot replace ticket history.

## Recovery Scenarios

Required runbooks eventually cover supplier outage, pending booking, payment success with booking failure, cancellation/refund delay, webhook/reconciliation backlog, compromised supplier key, compromised customer account, Keycloak outage and database restore.

Disaster-recovery planning covers PostgreSQL restoration, object storage, Keycloak data/configuration, secrets, provider-key rotation, notification replay, post-outage reconciliation, recovery objectives and tested recovery exercises. After an outage, target bookings whose external state may have changed rather than blindly replaying every operation.

Expiry and recovery follow [Data Retention and Legal Hold](../security/data-retention-and-legal-hold.md). Restore procedures reapply deletion tombstones so expired personal data does not return to active use. Legal-hold evidence uses a separate protected repository when it must outlive the ordinary 35-day backup window.

## Feature Control and Environments

Auditable, reversible feature controls gate supplier rollout, flight access, payment-flow changes, mobile beta, reconciliation strategies, AI features and pricing rules. Financial and booking behaviour changes require especially controlled rollout and rollback.

Local development, automated test, supplier sandbox, staging/UAT and production remain separated. Production supplier keys, customer data and payment configuration are prohibited from lower environments.
