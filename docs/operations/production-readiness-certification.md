<!-- markdownlint-disable MD013 -->

# Production-Readiness Certification

## Status

Established by Slice 7 (2026-08-10). This document is a human-readable mirror of the code-verifiable `ReadyToGoTravel.Retention.Readiness.ReadinessCatalog` type; the [Remaining Review Register](../decisions/review-register.md) remains the authoritative source for each gate's disposition and evidence requirement. This document must never claim a gate is verified before the register and the underlying evidence say so — a valid, expected result at MVP completion is "implementation complete, launch blocked by outstanding gates," not "ready."

## How to Read This Table

- **Implementation**: `NotStarted` / `InProgress` / `Complete` — whether the application code for this capability exists in this repository.
- **Verification**: `Unverified` / `LocallyVerified` / `ExternallyVerified` — how the capability has actually been exercised. `LocallyVerified` means a live local/CI drill (for example, a real PostgreSQL container) has exercised it; `ExternallyVerified` means the named approval owner has recorded a real, dated production approval.
- **Activation**: `Disabled` / `Enabled` — whether the capability is turned on in any checked-in configuration. A production capability never self-enables; flipping this to `Enabled` is itself a reviewed, evidenced change.
- **Blocking**: whether this gate must reach `ExternallyVerified` before overall readiness (`Ready` below) can be `true`. A gate that is merely `LocallyVerified`, or has `Complete` implementation, can never satisfy a Blocking gate on its own.

## Readiness Gates

| Gate | Implementation | Verification | Activation | Blocking | Required evidence |
| --- | --- | --- | --- | --- | --- |
| OI-0002 LiteAPI commercial terms and merchant-of-record | Complete | Unverified | Disabled | Yes | Executed merchant, settlement, descriptor, refund, dispute, chargeback, tax and booking-failure allocation. |
| OI-0003 Australian carrier production entitlement | Complete | Unverified | Disabled | Yes | Production entitlement plus dated verify, prebook, book, ticket, retrieve and servicing evidence per enabled carrier. |
| OI-0005 LiteAPI webhook production reliance | Complete | Unverified | Disabled | Yes | Account subscriptions, secrets, retry settings and controlled production delivery/duplicate/missing-event tests. |
| OI-0006 Payment SDK / AOC / PCI scope | Complete | Unverified | Disabled | Yes | Current AOC, responsibility matrix, qualified scope, 3-D Secure and production-domain/payment-method evidence. |
| OI-0011 Retention: LiteAPI DPA and Australian legal/privacy approval | Complete | Unverified | Disabled | Yes | Executed LiteAPI/DPA review, Australian Privacy and Legal/Compliance approval of the record-class schedule, and tested enforcement. |
| Australian market legal and compliance checklist | Complete | Unverified | Disabled | Yes | Operating entity, executed supplier/customer terms, funds flow, and the formal approval record in the [legal-approval checklist](../australian-market-legal-pack/09-legal-approval-checklist.md). |
| Object storage and malware-scanning production activation | Complete | Unverified | Disabled | Yes | Provisioned Australian-region S3-compatible bucket and credentials, a production ClamAV deployment/monitoring path, and exercised quarantine/failure-mode tests. |
| Retention sweep production activation | Complete | Locally Verified | Disabled | Yes | `Retention:Enabled` flipped on in a verified production configuration, plus a completed production retention/legal-hold drill. |
| Legal-hold-officer role provisioning | Complete | Unverified | Disabled | No | Named legal/compliance staff assigned the `legal-hold-officer` Keycloak realm role. |
| PostgreSQL / Keycloak / object-storage backup and restore evidence | Not Started | Unverified | Disabled | Yes | Restore exercises against the stated RPO/RTO objectives, including confirmation that retention tombstones are reapplied after a restore. |
| Diagnostic application log retention | Not Started | Unverified | Disabled | Yes | Confirmation that the log platform enforces a 90-day lifecycle and that logs never contain complete supplier payloads or sensitive traveller/payment data. |

## Overall Readiness

**`Ready = false`.** Nine of the eleven gates above are Blocking, and none has yet reached `ExternallyVerified`. This is the correct, fail-closed result of implementation completeness alone: Slice 7 implements every retention, legal-hold and account-closure control this repository can implement without production supplier, payment, storage or legal/compliance access, and verifies what it can against a live PostgreSQL drill, but it cannot and does not fabricate commercial, legal or infrastructure evidence it does not hold.

## Notes Per Gate

- **Retention sweep production activation** is the strongest gate opened by Slice 7: every concretely-scoped sweep (webhook payload bodies, notification content, abandoned checkout state, support tickets/attachments/audit records, account-closure minimisation) is implemented, legal-hold-aware, and has been verified against a real PostgreSQL 17 container — including a genuine defect this drill found and fixed (see the [Slice 7 outcome report](../delivery/2026-08-10-slice-7-retention-privacy-production-readiness-outcome.md)). It remains Blocking because `Retention:Enabled` defaults `false` in every checked-in configuration and no production drill has run yet, matching the exact precedent already set by `Support:Storage`/`Support:Scanning` in Slice 6.
- **Legal-hold-officer role provisioning** is non-Blocking because the only outstanding item is naming real staff to the role — an operational assignment, not a design or implementation gap, matching the existing precedent for support-agent assignment and supplier escalation contacts.
- **Canonical booking evidence, raw supplier payloads and traveller sensitive-field minimisation** are recorded in the retention policy catalog (period, trigger, action) but have no live sweep implementation, because production supplier integration remains disabled (OI-0002/OI-0003/OI-0006) and no live data of those kinds exists yet to sweep. This is not represented as a separate readiness gate because there is nothing yet to activate; it will need one once production supplier data begins to exist.
- **Backup/restore and log-retention gates** are explicitly infrastructure-owned. This repository's application code cannot self-certify a managed PostgreSQL/object-storage backup rotation or a log platform's lifecycle policy; these rows exist so that fact is visible rather than silently assumed.
