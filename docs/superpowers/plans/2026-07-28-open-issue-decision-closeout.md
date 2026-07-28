<!-- markdownlint-disable MD013 -->

# Open-Issue Decision Closeout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the approved 2026-07-28 product decisions to the ADR, issue, product, architecture, security, operations and planning documentation without inventing answers for deferred or supplier-evidence-dependent questions.

**Architecture:** Close the five settled issues, move the five selected-but-evidence-gated issues to in-review, leave OI-0010/OI-0011 open, and add ADR-0008 for durable flight reconciliation in a dedicated .NET worker container. Synchronise ordinary documentation and PLAN-0001 to these lifecycle records while retaining external production gates.

**Tech Stack:** Markdown, .NET 10 architecture documentation, Blazor SSR, ASP.NET Core Web API, PostgreSQL durable work records, S3-compatible private object storage, Git.

**Execution:** Completed inline on 2026-07-28. The only unresolved items are the external evidence and explicit deferrals retained in PLAN-0001.

## Global Constraints

- Documentation only: do not create application code, schemas, migrations, endpoints, infrastructure or deployment manifests.
- Preserve the standalone consumer boundary; do not introduce tenant, TMC, reseller or enterprise concepts.
- Keep OI-0010 and OI-0011 open and do not select answers for them.
- Do not claim LiteAPI production capability, merchant responsibility, webhook guarantees, PCI status or complete airline servicing without external evidence.
- Combined hotel-plus-flight means a customer journey over independently represented product bookings; it does not establish a regulated package, one price, one contract or one payment.
- Use .NET 10 Blazor SSR and ASP.NET Core Web API; required payment JavaScript remains a narrow interoperability boundary.
- Flight reconciliation is booking/itinerary state, not live operational flight status.
- Ticket UUIDv7 identifiers are not access secrets; guest access uses a separately generated high-entropy bearer token stored only as a hash.
- Ticket attachments are private S3-compatible objects exposed only through authorised, short-lived access.
- Preserve the unrelated untracked `domain-results/` directory.

---

### Task 1: Record the Flight-Reconciliation Architecture Decision

**Files:**

- Create: `docs/adr/accepted/ADR-0008-durable-flight-reconciliation-and-customer-notification.md`
- Modify: `docs/adr/index.md`
- Modify: `docs/architecture/target-architecture.md`
- Modify: `docs/domain/booking-reconciliation-and-version-history.md`

**Interfaces:**

- Consumes: ADR-0004 immutable versions, ADR-0005 operational-status boundary and ADR-0006 workload-specific worker rule.
- Produces: accepted daily/proximity schedule, dedicated worker boundary and durable notification trigger used by issue and plan updates.

- [x] **Step 1: Add the accepted ADR**

Record the explicit product-owner decision with `status: accepted`, `date_proposed: 2026-07-28`, `date_accepted: 2026-07-28`, links to OI-0004/OI-0005/OI-0010 and related ADRs 0004/0005/0006. Specify daily reconciliation outside the final 24 hours, hourly reconciliation during the final 24 hours before each segment, webhook-triggered immediate checks, durable PostgreSQL scheduling, immutable versions, deduplicated notifications and the exclusion of live operational status.

- [x] **Step 2: Update the ADR index**

Change the accepted count from seven to eight and add ADR-0008 with Product, Architecture and Operations decision owners.

- [x] **Step 3: Synchronise architecture and reconciliation guidance**

Add the dedicated private flight-reconciliation worker to the target topology and document the exact schedule, webhook interaction, restart safety, idempotency and notification outbox behaviour in the reconciliation guide.

- [x] **Step 4: Verify the ADR task**

Run:

```bash
rg -n "ADR-0008|once per calendar day|final 24 hours|once per hour|operational flight status" docs/adr docs/architecture docs/domain
git diff --check
```

Expected: ADR-0008 and both schedule tiers appear; operational status remains explicitly separate; `git diff --check` exits zero.

- [x] **Step 5: Commit**

```bash
git add -- docs/adr/accepted/ADR-0008-durable-flight-reconciliation-and-customer-notification.md docs/adr/index.md docs/architecture/target-architecture.md docs/domain/booking-reconciliation-and-version-history.md
git commit -m "docs: accept flight reconciliation worker decision"
```

### Task 2: Resolve and Reclassify the Open Issues

**Files:**

- Move and modify: `docs/issues/open/OI-0001-mvp-product-scope.md` to `docs/issues/closed/OI-0001-mvp-product-scope.md`
- Modify: `docs/issues/open/OI-0002-liteapi-commercial-and-merchant-of-record.md`
- Modify: `docs/issues/open/OI-0003-australian-airline-and-qantas-coverage.md`
- Modify: `docs/issues/closed/OI-0004-flight-servicing-and-schedule-changes.md` (subsequent canonical location)
- Modify: `docs/issues/open/OI-0005-liteapi-webhook-coverage.md`
- Modify: `docs/issues/open/OI-0006-mobile-payment-and-pci-scope.md`
- Move and modify: `docs/issues/open/OI-0007-launch-market-locale-and-currency.md` to `docs/issues/closed/OI-0007-launch-market-locale-and-currency.md`
- Move and modify: `docs/issues/open/OI-0008-saved-traveller-and-passport-data.md` to `docs/issues/closed/OI-0008-saved-traveller-and-passport-data.md`
- Move and modify: `docs/issues/open/OI-0009-web-frontend-technology.md` to `docs/issues/closed/OI-0009-web-frontend-technology.md`
- Move and modify: `docs/issues/open/OI-0012-customer-support-model.md` to `docs/issues/closed/OI-0012-customer-support-model.md`
- Modify: `docs/issues/index.md`

**Interfaces:**

- Consumes: approved disposition in the design spec and ADR-0008.
- Produces: five closed, five in-review and two open issue records referenced by all remaining documentation.

- [x] **Step 1: Close the settled issues**

For OI-0001, OI-0007, OI-0008, OI-0009 and OI-0012, set `status: closed`, `updated: 2026-07-28`, record the selected decision, mark the decision acceptance criteria complete and retain named implementation/go-live constraints. Update their related-document links to use the correct relative depth after moving to `closed/`.

- [x] **Step 2: Put vendor-evidence issues in review**

For OI-0002 through OI-0006, set `status: in-review`, `updated: 2026-07-28`, add the selected direction and owner evidence, and leave external acceptance criteria unchecked. OI-0003 must describe Qantas, Jetstar and Virgin Australia as a portal observation rather than verified production entitlement.

- [x] **Step 3: Rebuild the issue index**

Set counts to Open 2, In review 5 and Closed 5. Keep OI-0010/OI-0011 under Open, list OI-0002 through OI-0006 under In Review, list the five moved records under Closed, and describe the remaining decision/evidence order accurately.

- [x] **Step 4: Verify issue lifecycle state**

Run:

```bash
find docs/issues/open -name 'OI-*.md' -maxdepth 1 | sort
find docs/issues/closed -name 'OI-*.md' -maxdepth 1 | sort
rg -n "^status:" docs/issues/open docs/issues/closed
git diff --check
```

Expected: seven files remain under `open/` with five `in-review` and two `open`; five files exist under `closed/` with `closed`; `git diff --check` exits zero.

- [x] **Step 5: Commit**

```bash
git add -- docs/issues/open docs/issues/closed docs/issues/index.md
git commit -m "docs: resolve product decision issues"
```

### Task 3: Synchronise Product, Payment, Locale and Traveller Policy

**Files:**

- Modify: `docs/product/consumer-mvp-scope.md`
- Modify: `docs/product/capability-roadmap.md`
- Modify: `docs/product/payments-and-pricing.md`
- Modify: `docs/applications/client-strategy.md`
- Modify: `docs/domain/trips-travellers-and-bookings.md`
- Modify: `docs/security/payment-and-pci-scope.md`
- Modify: `docs/security/security-privacy-and-data-ownership.md`
- Modify: `docs/decisions/assumptions.md`
- Modify: `docs/decisions/constraints.md`

**Interfaces:**

- Consumes: closed OI-0001/OI-0007/OI-0008/OI-0009 and in-review OI-0002/OI-0006.
- Produces: canonical launch scope, web/payment boundary, localisation authority and opt-in sensitive traveller policy.

- [x] **Step 1: Replace hotel-first scope and roadmap text**

Describe hotel-only, flight-only and combined hotel-plus-flight journeys as initial product scope while retaining production gates for unsupported flight capabilities. Keep live operational flight status, native mobile applications and Duffel activation deferred.

- [x] **Step 2: Record the initial LiteAPI payment route**

State that the initial direction uses LiteAPI's approved hosted/SDK component and supplier/provider-controlled payment where written terms permit it. Preserve separate merchant, customer-payment, supplier-settlement, booking and refund responsibilities and all OI-0002/OI-0006 evidence gates.

- [x] **Step 3: Record Blazor SSR and localisation**

Set .NET 10 Blazor SSR as the web technology. Store anonymous locale in a secure same-site cookie, authenticated BCP 47 locale in PostgreSQL and UI translations in version-controlled .NET localisation resources with `en-AU` fallback. Keep language independent from currency and preserve supplier/transaction currencies.

- [x] **Step 4: Record explicit traveller opt-in**

Distinguish booking evidence from reusable profile storage. Require granular, unchecked consent before saving date of birth or identity documents, plus protection, masking, audit and deletion gates.

- [x] **Step 5: Verify policy consistency**

Run:

```bash
rg -n "hotel-first|OI-0001|OI-0007|OI-0008|OI-0009" docs/product docs/applications docs/domain docs/security docs/decisions
rg -n "Blazor|en-AU|BCP 47|localisation|opt-in|unchecked|LiteAPI" docs/product docs/applications docs/domain docs/security docs/decisions
git diff --check
```

Expected: no current policy describes a hotel-first launch or points to the moved open issue paths; Blazor, locale and opt-in policy are explicit; `git diff --check` exits zero.

- [x] **Step 6: Commit**

```bash
git add -- docs/product docs/applications/client-strategy.md docs/domain/trips-travellers-and-bookings.md docs/security docs/decisions/assumptions.md docs/decisions/constraints.md
git commit -m "docs: align product and customer data policy"
```

### Task 4: Define the Ticket-Support Operating Model

**Files:**

- Modify: `docs/operations/reliability-and-supportability.md`
- Modify: `docs/architecture/target-architecture.md`
- Modify: `docs/domain/trips-travellers-and-bookings.md`
- Modify: `docs/applications/client-strategy.md`

**Interfaces:**

- Consumes: closed OI-0012 support decision and existing support/documents/notifications module boundaries.
- Produces: authoritative ticket state transitions, guest magic-link protection, private attachment boundary and email-update behaviour.

- [x] **Step 1: Add ticket lifecycle and access rules**

Document `New`, `Waiting on Support`, `Waiting on Customer` and `Closed`, including the last-accepted-author transition rules. Record UUIDv7 as the internal ticket identifier and a separate hashed high-entropy bearer token for guest access.

- [x] **Step 2: Add attachment and notification rules**

Document private S3-compatible objects, authorised expiring download access, safe upload validation/quarantine, immutable thread entries and durable deduplicated email on every accepted update.

- [x] **Step 3: Preserve the only unresolved ticket behaviour**

State that reopening a closed ticket versus creating a linked follow-up ticket remains an implementation-planning choice that must be selected and tested before shipment. Do not reopen OI-0012 because the support channel and lifecycle are settled.

- [x] **Step 4: Verify the support model**

Run:

```bash
rg -n "Waiting on Support|Waiting on Customer|UUIDv7|bearer token|private|signed|email" docs/operations docs/architecture docs/domain docs/applications
rg -n "public (file|object)|UUIDv7.*secret" docs/operations docs/architecture docs/domain docs/applications || true
git diff --check
```

Expected: the full ticket model appears; no permanent-public-object or UUIDv7-as-secret claim appears; `git diff --check` exits zero.

- [x] **Step 5: Commit**

```bash
git add -- docs/operations/reliability-and-supportability.md docs/architecture/target-architecture.md docs/domain/trips-travellers-and-bookings.md docs/applications/client-strategy.md
git commit -m "docs: define ticket support model"
```

### Task 5: Update Planning Readiness and Validate the Documentation Set

**Files:**

- Modify: `docs/decisions/open-questions.md`
- Modify: `docs/plans/active/PLAN-0001-project-planning-readiness.md`
- Modify: `docs/plans/index.md`
- Modify: any Markdown file containing a stale moved-issue path discovered by validation.

**Interfaces:**

- Consumes: all issue, ADR and ordinary-document changes from Tasks 1 through 4.
- Produces: accurate planning status and a link/format-consistent documentation set ready for the remaining external evidence work.

- [x] **Step 1: Update the remaining-question register**

Remove settled product questions from the immediate open list. Keep OI-0010/OI-0011 as unresolved decisions and OI-0002 through OI-0006 as selected directions awaiting external evidence.

- [x] **Step 2: Update PLAN-0001**

Mark OI-0001, OI-0007, OI-0008, OI-0009 and OI-0012 resolved. Describe OI-0002 through OI-0006 as in review, add ADR-0008, add the ticket security and locale implementation-planning inputs, retain OI-0010/OI-0011, update dates and append a 2026-07-28 change-log entry. Do not mark PLAN-0001 complete while P0 supplier/commercial/PCI evidence remains.

- [x] **Step 3: Repair lifecycle links**

Replace every stale `issues/open/` link for moved issues with the corresponding `issues/closed/` path, using correct relative depth and casing.

- [x] **Step 4: Run complete documentation validation**

Run a repository-local shell validation that counts H1 headings and checks every relative Markdown link target, then run:

```bash
git diff --check
rg -n -i "Cinturon|C360|tenant|TMC|reseller" docs
rg -n "issues/open/OI-000(1|7|8|9)|issues/open/OI-0012" docs
git status --short
git diff --stat HEAD~4..HEAD
```

Expected: every Markdown file has one H1, all local links resolve, no moved-issue open path remains, no unrelated enterprise terminology is introduced, `domain-results/` remains the only unrelated untracked path and `git diff --check` exits zero.

- [x] **Step 5: Commit planning and link updates**

```bash
git add -- docs/decisions/open-questions.md docs/plans docs
git commit -m "docs: update planning readiness after decisions"
```

- [x] **Step 6: Verify the final committed range**

Run:

```bash
git status --short
git log --oneline --decorate -6
git diff --check HEAD~5..HEAD
```

Expected: the only unrelated working-tree entry is `?? domain-results/`; the five documentation commits plus the prior design commit are visible; the committed range passes whitespace validation.

## Post-execution Decisions

The executed plan preserved OI-0010/OI-0011 and closed-ticket reply behaviour because they were undecided at that checkpoint. Later product-owner decisions on 2026-07-28 established:

- OI-0010 as a non-blocking post-MVP wishlist item;
- OI-0011 Option A with the exact [Data Retention and Legal Hold](../../security/data-retention-and-legal-hold.md) baseline;
- reopening the same ticket as `Waiting on Support` when a customer replies after closure;
- deferral of manual itinerary items, sharing, loyalty, native offline capability, push and SMS from the initial release.

The canonical issue, product and PLAN-0001 records contain the current state.
