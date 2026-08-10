<!-- markdownlint-disable MD013 -->

# Retention and Legal Hold

This page documents the Slice 7 retention and legal-hold model implemented against the approved [Data Retention and Legal Hold](../security/data-retention-and-legal-hold.md) baseline and closed [OI-0011](../issues/closed/OI-0011-supplier-payload-retention.md). See the [Slice 7 design](../superpowers/specs/2026-08-10-slice-7-retention-privacy-production-readiness-design.md), [implementation plan](../superpowers/plans/2026-08-10-slice-7-retention-privacy-production-readiness.md) and [outcome report](../delivery/2026-08-10-slice-7-retention-privacy-production-readiness-outcome.md) for the full record.

## Record-Class Lifecycle Model

Every retained record class is modelled explicitly rather than as an ad hoc delete query: `RetentionRecordClass` is a closed enum of the twelve classes in the approved schedule, and `RetentionPolicyCatalog` pairs each with its documented period, unit (days or years), trigger description, policy version and action (`Delete`, `DeIdentify`, or `PolicyOnlyNoLiveSweep` where no live sweep is implemented). `RetentionPolicyCatalog.CalculateExpiry`/`IsExpired` is the single source of truth for expiry math — every sweep calls this rather than computing a manual cutoff, so a policy's period-unit can never be silently miscalculated in one sweep and not another.

Sweeps are stateless and live-recomputed: each cycle re-derives candidacy fresh from a stable historical trigger timestamp already stored on the owning record (ticket closure, webhook completion, checkout last-activity), rather than from a separate pre-populated work queue. This makes every sweep naturally idempotent and means reapplying retention tombstones after a restore is automatic once the worker resumes running, with no separate reconciliation step needed.

## Module Ownership

A new `ReadyToGoTravel.Retention` module owns only what is genuinely cross-module: legal holds and their scopes, the retention policy catalog, centralised deletion receipts, and the production-readiness catalog. It has no reference to Booking, Support or Consumer; those modules reference Retention and implement their own sweep against their own schema, calling the shared `ILegalHoldGuard` and `IRetentionReceiptRecorder` contracts.

| Record class | Owner | Action |
| --- | --- | --- |
| Webhook payload bodies (90d) | Booking | Delete |
| Notification rendered content (90d) | Booking | Delete |
| Abandoned checkout state (30d) | Booking | Delete |
| Support attachments (90d from ticket closure) | Support | Delete |
| General support tickets (2y from closure) | Support | Delete |
| Booking-related support tickets (7y from closure) | Support | Delete |
| Security audit records (2y) | Support | Delete |
| Customer account closure (90d from closure) | Consumer | De-identify |
| Canonical booking evidence (7y) | — | Policy-and-calculator only |
| Successful/exceptional raw supplier payloads (90d/12mo) | — | Policy-and-calculator only |
| Traveller sensitive-field minimisation (90d from travel completion) | — | Policy-and-calculator only |

The last three rows have no live sweep because production supplier integration remains disabled and no live data of those kinds exists yet to sweep — they are still recorded in the policy catalog so the schedule stays complete and traceable.

## Legal Hold

A `LegalHold` cannot be created with zero scope entries — the model has no "any hold exists, suspend everything" shortcut. Each `LegalHoldScope` names exactly one record class and exactly one subject (a customer, a component booking, or a support ticket), enforced both in code and by a PostgreSQL `CHECK` constraint. Protecting several subjects means several scope rows on the same hold, so matching stays an unambiguous per-record-class equality check.

Every sweep checks `ILegalHoldGuard` twice: `ExcludeHeldAsync` pre-filters a whole batch before any destructive action begins, and `IsHeldAsync` rechecks each individual item immediately before its own delete or redact statement, closing the race window between batch selection and execution. Both checks write an audit event. A hold's release is idempotent and, once released, the affected records resume their ordinary lifecycle on the next sweep cycle.

Legal holds are administered by a named `legal-hold-officer` through a staff-only API (`/api/v1/retention/legal-holds`), gated by a Keycloak realm role and never reachable by an ordinary customer token — mirroring the Slice 6 `support-agent` console pattern exactly.

## Deletion Receipts and Audit Trail

Every sweep batch, successful or not, produces a `RetentionDeletionReceipt`: record class, policy version, action, success/failure counts, completion time, and a short non-sensitive failure summary. It never contains the deleted content itself. `LegalHoldAuditEvent` (hold opened, hold released, guard check found a hold) is append-only, enforced by both an EF change-tracker guard and a PostgreSQL trigger — legal-hold history is exactly as tamper-evident as canonical booking version history.

## Account Closure

`Customer.Close` (Slice 7) is customer-initiated, idempotent, and immediately denies further customer-scoped booking-context access. It never deletes or rewrites confirmed-booking, financial, refund, dispute or legal-hold evidence. A later minimisation sweep only proceeds once every module's `IConsumerRetentionEvidencePort` implementation confirms the closed customer has no remaining protected evidence; only non-essential preference fields (locale, display currency) are reset, while the stable customer identifier, subject and closure timestamp are preserved so already-retained evidence continues to make sense.
