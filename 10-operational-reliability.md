# Operational Reliability

## Reliability principle

Travel operations are externally dependent and financially sensitive.

The platform should assume:

- supplier timeouts;
- stale offers;
- price changes;
- partial failure;
- delayed confirmation;
- duplicate requests;
- duplicate webhooks;
- mobile-network interruption;
- customer retries;
- third-party outages;
- inconsistent external state.

## Search versus booking workloads

Search and booking have different characteristics.

Search is:

- high volume;
- cacheable for short periods;
- replaceable;
- tolerant of partial supplier failure;
- heavily rate-limited by supplier economics.

Booking is:

- lower volume;
- durable;
- financially sensitive;
- idempotent;
- audit-heavy;
- not safely repeatable without controls.

They should have separate monitoring and scaling assumptions.

## Timeouts

Every external call requires an explicit timeout.

Timeouts should differ by operation:

- reference lookup;
- search;
- prebook;
- payment-session creation;
- booking confirmation;
- booking retrieval;
- cancellation.

A timeout does not prove that the supplier did not perform the operation. Recovery must query current state before repeating high-impact operations.

## Retries

Retries should be selective.

Safe retry depends on:

- operation idempotency;
- supplier guarantees;
- received response;
- timeout type;
- request key;
- current booking state.

Blind retries are dangerous for booking and payment operations.

## Circuit breaking and degradation

When a supplier is unavailable, the platform should:

- stop overwhelming it;
- return a stable platform error;
- preserve customer context;
- avoid showing stale bookable prices as current;
- allow access to existing trips and documents;
- surface a useful retry path;
- alert operations.

## Outbox and durable messaging

Events that must survive process failure should be recorded transactionally with the application change and dispatched later.

Likely uses include:

- booking confirmed;
- booking changed;
- cancellation accepted;
- refund completed;
- notification requested;
- document generation requested;
- reconciliation requested.

## Webhook inbox

Incoming webhooks should be persisted before processing where practical.

The inbox should support:

- authentication result;
- supplier event ID;
- deduplication;
- received timestamp;
- payload hash;
- processing status;
- retry count;
- failure reason;
- correlation with booking;
- replay.

## Observability

Operational telemetry should include:

- request rate;
- latency;
- error rate;
- supplier error category;
- rate-limit usage;
- cache effectiveness;
- search-to-prebook ratio;
- prebook-to-book ratio;
- booking confirmation time;
- pending bookings;
- payment-booking mismatches;
- reconciliation age;
- webhook lag;
- notification failures;
- refund age;
- support-required bookings.

## Audit versus logs

Application logs are not the booking audit trail.

Booking audit records should be durable, structured, queryable, access-controlled, and retained according to policy.

Logs can expire much sooner and are optimised for diagnostics.

## Supportability

Support staff should be able to see:

- platform booking ID;
- customer-facing references;
- supplier references;
- current status;
- last supplier check;
- payment state;
- cancellation terms;
- version timeline;
- meaningful diffs;
- failed operations;
- correlation IDs;
- notification history;
- permitted next actions.

Sensitive data should be masked by default.

## Disaster recovery

Planning should cover:

- PostgreSQL backup and restoration;
- object-storage recovery;
- Keycloak database and configuration recovery;
- secret recovery;
- supplier key rotation;
- notification replay;
- reconciliation after outage;
- recovery-point and recovery-time objectives;
- recovery testing.

## Data consistency after outage

After a platform outage, the system should identify bookings that may have changed externally and prioritise reconciliation.

After a supplier outage, pending bookings and cancellations require targeted recovery rather than a generic full-system retry.

## Feature flags

Feature flags are useful for:

- supplier rollout;
- flight access;
- payment flow changes;
- mobile beta;
- reconciliation strategies;
- AI features;
- new pricing rules.

Financial and booking behaviour changes should be auditable and safely reversible.

## Environments

At minimum, separate:

- local development;
- automated test;
- supplier sandbox;
- staging or UAT;
- production.

Production supplier keys, customer data, and payment configuration must not be used in lower environments.

## Runbooks

Eventually required runbooks include:

- supplier outage;
- booking pending;
- payment succeeded but booking failed;
- cancellation pending;
- refund delayed;
- webhook backlog;
- reconciliation backlog;
- compromised API key;
- compromised customer account;
- Keycloak outage;
- database restore;
- incorrect pricing rule.
