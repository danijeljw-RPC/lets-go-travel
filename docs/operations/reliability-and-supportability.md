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

## Recovery Scenarios

Required runbooks eventually cover supplier outage, pending booking, payment success with booking failure, cancellation/refund delay, webhook/reconciliation backlog, compromised supplier key, compromised customer account, Keycloak outage and database restore.

Disaster-recovery planning covers PostgreSQL restoration, object storage, Keycloak data/configuration, secrets, provider-key rotation, notification replay, post-outage reconciliation, recovery objectives and tested recovery exercises. After an outage, target bookings whose external state may have changed rather than blindly replaying every operation.

## Feature Control and Environments

Auditable, reversible feature controls gate supplier rollout, flight access, payment-flow changes, mobile beta, reconciliation strategies, AI features and pricing rules. Financial and booking behaviour changes require especially controlled rollout and rollback.

Local development, automated test, supplier sandbox, staging/UAT and production remain separated. Production supplier keys, customer data and payment configuration are prohibited from lower environments.
