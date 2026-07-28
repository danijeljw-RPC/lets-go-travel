# Reliability and Supportability

## Failure Assumptions

Assume supplier timeouts, stale offers, price changes, partial failure, pending confirmation, duplicate customer actions, duplicate or missing webhooks, mobile interruption and third-party outages.

## Search and Booking

Search is high-volume, short-lived and replaceable. Booking is durable, financially sensitive and unsafe to repeat without controls. Give them separate telemetry, concurrency and scaling policies.

## Timeout and Retry

Every external operation has an explicit timeout. Retry only when operation semantics and idempotency make it safe. After a high-impact timeout, query current state before repeating the action.

## Durable Messaging

Record business events transactionally with state changes and dispatch them later through an outbox or equivalent. Persist incoming webhooks to an inbox with event identity, authentication result, hash, processing status, attempts and failure reason.

## Observability

Measure request latency/errors, supplier errors, quota use, search-to-prebook and prebook-to-book ratios, confirmation time, pending bookings, payment mismatches, reconciliation age, webhook lag, notification failures, refund age and support-required bookings.

## Support View

Support needs platform and supplier references, current booking/payment/cancellation state, last supplier check, version timeline, meaningful diffs, failed operations, correlation IDs, notification history and permitted next actions with sensitive values masked.

## Recovery Scenarios

Required runbooks eventually cover supplier outage, pending booking, payment success with booking failure, cancellation/refund delay, webhook/reconciliation backlog, compromised supplier key, compromised customer account, Keycloak outage and database restore.
