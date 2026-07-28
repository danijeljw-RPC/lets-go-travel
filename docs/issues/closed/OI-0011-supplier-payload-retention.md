---
document_type: issue-answer
issue_id: OI-0011
title: Supplier Payload Retention Validation and Closure
status: closed
created: 2026-07-27
updated: 2026-07-28
decision_owners:
  - Data
  - Privacy
  - Legal/Compliance
related_adrs:
  - ADR-0004
related_plans:
  - PLAN-0001
related_docs:
  - docs/domain/booking-reconciliation-and-version-history.md
  - docs/security/data-retention-and-legal-hold.md
  - docs/integrations/liteapi-nuitee-connect.md
blocked_by: []
production_gates:
  - Executed LiteAPI commercial terms and DPA reviewed
  - Australian Privacy and Legal/Compliance approval recorded
  - Retention, deletion, backup and legal-hold controls verified
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0011 Answer — Supplier Payload Retention Validation and Closure

## Original requirement

| Review | Evidence or approval required | Owner | Effect |
| --- | --- | --- | --- |
| OI-0011 retention validation | LiteAPI retention/licensing terms and Australian legal/privacy approval of the baseline schedule, record classification and trigger dates. | Data, Privacy, Legal/Compliance | Does not block implementation of the stricter defaults; blocks claiming final production compliance. |

## Executive answer

**Option A is confirmed: retain durable supplier-neutral booking and financial evidence, while retaining allowlisted raw supplier payloads only for shorter, purpose-limited periods.**

The selected baseline is consistent with:

- LiteAPI's public licence restrictions;
- LiteAPI's public data-minimisation and retention statements;
- LiteAPI's stated DPA deletion model;
- section 286 of the Australian `Corporations Act 2001`;
- Australian Taxation Office business record-keeping guidance; and
- APP 11 guidance from the Office of the Australian Information Commissioner.

The public LiteAPI material does not grant an unrestricted right to permanently store or build databases from LiteAPI content. It grants a limited licence to use LiteAPI data to deliver travel services and prohibits storing, copying or creating databases outside that permitted scope.

The public LiteAPI privacy documentation states that data is retained only as long as necessary to provide contracted services, satisfy legal/accounting/regulatory duties, and support security, fraud prevention and dispute resolution. It also states that personal data is deleted or anonymised when no longer required.

This supports the project's selective-retention approach and rejects indefinite raw-payload storage.

OI-0011 is closed as a data-governance and architecture decision. Closing the issue does **not** represent:

- executed-contract approval;
- external legal advice;
- a final Australian privacy compliance opinion; or
- permission to claim production compliance before the production gates in this document are approved.

The executed LiteAPI commercial terms and DPA remain authoritative if they impose a shorter period, narrower purpose, deletion requirement or other restriction.

## Closure decision

The following position is adopted:

1. Supplier-neutral canonical booking, financial, settlement and dispute evidence is retained according to the approved legal and business record schedule.
2. Raw LiteAPI requests and responses are not permanent business records by default.
3. Successful raw payloads are retained only where allowlisted and only for a short diagnostic and reconciliation window.
4. Exceptional raw payloads may be retained longer where needed for an incident, ambiguous operation, mapping failure, refund, chargeback, supplier dispute or legal matter.
5. Search inventory, bulk hotel content, images, reviews, transient rates and other supplier content are not retained as a permanent internal dataset.
6. Personal information is removed, destroyed or de-identified when no longer needed for a permitted purpose and no legal retention obligation or legal hold applies.
7. A matter-specific legal hold suspends deletion only for records within the defined scope of the hold.
8. Backups expire on their normal rotation and are not treated as an indefinite exception to deletion.
9. The executed LiteAPI contract and DPA can narrow the permitted scope or require earlier deletion.
10. Production compliance may be claimed only after Data, Privacy and Legal/Compliance record formal approval and the controls have been verified.

## Supplier terms and licensing findings

### Limited permitted-use licence

LiteAPI's public Terms of Service grant a limited, non-exclusive, non-transferable and revocable licence to use Nuitee Connect to deliver travel services to end users.

The permitted examples include:

- browsing properties;
- displaying rates and availability; and
- enabling bookings through the customer's travel platform.

The public terms prohibit uses outside that permitted scope, including:

- redistributing or reselling Nuitee Connect inventory or data;
- mapping Nuitee Connect data to third-party datasets;
- using the data for competitive analysis, benchmarking or derivative works;
- storing, copying or creating databases from Nuitee Connect data outside the permitted scope;
- training or enriching third-party datasets or machine-learning models;
- scraping, harvesting or bulk-downloading inventory; and
- sublicensing or otherwise exploiting Nuitee Connect data.

LiteAPI also states that its content, APIs, databases, hotel information, property identifiers, rates and related materials remain the intellectual property of Nuitée Travel Limited.

### Retention consequence

The public terms do not prohibit every temporary or evidentiary copy required to operate an authorised travel service. However, they do not provide an unrestricted licence to retain all returned supplier content indefinitely.

The defensible interpretation for implementation is:

- retain only data necessary to deliver, evidence, reconcile, service and defend the travel transaction;
- retain full raw payloads only for the allowlisted purposes and periods in this document;
- do not create a permanent mirror of LiteAPI's inventory or content database;
- do not retain unrelated search results or content merely because the API returned them;
- do not reuse retained payloads for unrelated analytics, benchmarking, mapping, model training or product enrichment; and
- obtain executed contractual confirmation where a retention use is material or ambiguous.

This is an implementation interpretation, not a substitute for contractual legal advice.

## LiteAPI privacy and deletion findings

LiteAPI's public Data Protection and Privacy documentation states that Nuitee Connect:

- processes only data required for API functionality;
- avoids unnecessary personal-data storage;
- limits internal access on a need-to-know basis;
- retains data only as long as necessary to provide contracted services, meet legal/accounting/regulatory obligations, and support operational security, fraud prevention and dispute resolution; and
- deletes or anonymises personal data when it is no longer required.

LiteAPI also states that customers are responsible for ensuring that personal data sent to LiteAPI is lawfully collected and shared.

LiteAPI's public DPA summary states that:

- personal data is retained only as long as necessary to provide services and meet legal or operational obligations;
- upon termination, Nuitee Connect deletes or anonymises personal data in accordance with the DPA unless retention is required by law;
- compliance information can be made available subject to reasonable scope and confidentiality; and
- the formal DPA is available on request and forms part of the contractual documentation.

### Retention consequence

readytogo.travel must not infer LiteAPI's internal retention periods from its own schedule.

The production review must separately establish:

- what LiteAPI retains;
- how long LiteAPI retains each category;
- which party is controller, processor or independent controller for each processing activity;
- what deletion or return occurs on termination;
- how data-subject deletion requests are supported;
- what subprocessors hold the data;
- whether legal holds or supplier/accounting duties override deletion;
- whether readytogo.travel may retain specific supplier payload fields; and
- whether LiteAPI requires shorter deletion than the project baseline.

## Australian legal and privacy baseline

### Company financial records

Section 286 of the `Corporations Act 2001` requires a company to keep written financial records that correctly record and explain its transactions and financial position and performance and would enable true and fair financial statements to be prepared and audited.

The section requires those financial records to be retained for seven years after the transactions covered by the records are completed.

This requirement applies to qualifying financial records. It does not impose a seven-year blanket on:

- all personal information;
- every supplier API response;
- all search results;
- all hotel or airline content;
- all operational logs; or
- fields that are unnecessary to explain the transaction.

### Tax records

The Australian Taxation Office states that most business records must generally be retained for five years. The exact start point depends on the record and commonly runs from when the record was prepared or obtained, or when the relevant transaction was completed.

Some categories require longer retention or have special trigger rules.

Because the approved seven-year financial-evidence schedule is longer than the ordinary five-year tax baseline, it provides a conservative default for records that are genuinely part of the canonical financial transaction record.

### Personal information

APP 11 requires an APP entity to take reasonable steps to protect personal information it holds.

APP 11 also requires reasonable steps to destroy or de-identify personal information once it is no longer needed for a purpose for which it may be used or disclosed, unless:

- it is contained in a Commonwealth record; or
- an Australian law or court/tribunal order requires retention.

OAIC guidance states that this obligation can extend to archived and backup copies and expects organisations to have technical and organisational systems to identify and destroy or de-identify information that is no longer needed.

### Legal consequence

A longer retention period should be applied only where supported by:

- a specific legal record-keeping obligation;
- a continuing permitted business purpose;
- a contractual requirement;
- a dispute or incident need;
- a regulator/court requirement; or
- a scoped legal hold.

Retention must not be extended merely because storage is inexpensive or the data may hypothetically be useful.

## Approved baseline schedule

| Record class | Baseline retention | Trigger date | Purpose and scope | End-of-life action |
| --- | --- | --- | --- | --- |
| Canonical booking transaction record | Seven years | The later of booking completion, final travel completion, final supplier settlement, final customer settlement, final refund/chargeback resolution or closure of a financial adjustment affecting the transaction | Evidence of the service purchased, parties, price, taxes, fees, cancellation terms, confirmations, changes and final financial outcome | Delete fields not required for the retained legal/financial purpose; destroy the remaining record after expiry unless held |
| Canonical accounting and settlement evidence | Seven years | Completion of the transaction covered by the financial record; use the later financial-completion date where multiple settlement, refund or chargeback events form one transaction history | Financial reporting, audit, reconciliation, payout, commission, refund, dispute and tax evidence | Destroy after expiry unless another law or hold applies |
| Canonical itinerary and meaningful booking versions | Seven years where part of the booked transaction record | Final completion of the related transaction, using the same conservative completion trigger as the canonical booking record | Explain what was sold, confirmed, changed and ultimately fulfilled or refunded | Remove unnecessary personal and volatile supplier content before or during canonicalisation |
| Successful allowlisted raw supplier payload | 90 days | Successful completion and reconciliation of the related supplier operation | Short-term mapping diagnosis, reconciliation, support and regression analysis | Automatically delete; do not promote to long-term evidence without reclassification |
| Raw webhook payload successfully processed | 90 days | Successful processing and reconciliation of the event | Inbox deduplication, delivery evidence, parser diagnosis and reconciliation trace | Delete raw body after expiry; retain non-sensitive canonical audit metadata if required |
| Ambiguous operation or uncertain supplier outcome payload | 12 months after resolution | Date the ambiguity is conclusively resolved and the final supplier/customer/financial state is reconciled | Evidence for timeouts, unknown outcomes, duplicate risk and supplier investigation | Delete after expiry unless reclassified as financial/dispute evidence or held |
| Mapping or canonicalisation failure payload | 12 months after resolution | Date the mapping defect is fixed and affected data is reconciled | Defect diagnosis, regression proof and correction evidence | Delete after expiry; retain de-identified test fixtures only where licensing and privacy permit |
| Security incident payload evidence | 12 months after incident closure, or longer where the incident policy or law requires | Formal incident closure date | Investigation and control validation | Delete or de-identify unless legal, insurance, regulator or hold requirements apply |
| Refund, chargeback, complaint or supplier dispute payload | 12 months after final resolution, unless incorporated into the seven-year financial evidence record | Date all financial, supplier and customer consequences are final | Support and dispute defence | Extract required canonical financial evidence; delete unnecessary raw payload after expiry |
| General non-booking support ticket | Two years | Ticket closure date | Customer service quality, complaint history and operational support | Delete or de-identify unless linked to a booking, financial record, dispute, incident or legal hold |
| Booking-related support ticket | Follow the longest applicable booking, financial, dispute or legal-hold classification | Final resolution of the matter, with the financial transaction trigger applied to retained financial evidence | Explain booking servicing and customer outcome | Retain only material evidence; remove conversational duplication and unnecessary personal information |
| Ordinary operational backup | 35 days | Backup creation date | Disaster recovery | Expire automatically; deleted production data disappears as backup generations rotate |
| Legal-hold copy | Until written release by an authorised Legal/Compliance owner | Hold issue date; no automatic expiry while active | Preserve records relevant to an actual or reasonably anticipated dispute, litigation, investigation, audit, subpoena or regulator/court requirement | Resume the ordinary schedule from the applicable trigger when the hold is released; delete promptly where the ordinary period has already expired |
| De-identified aggregate metric | According to the analytics policy, potentially longer where genuinely de-identified and contractually permitted | Creation of the de-identified dataset | Product, reliability and business measurement | Periodically reassess re-identification risk, necessity and supplier licensing restrictions |

## Trigger-date rules

### Transaction completion

For the seven-year canonical financial schedule, use a conservative transaction-completion date.

The trigger is the latest material date required to explain the completed transaction, including where applicable:

- service or travel completion;
- booking cancellation;
- supplier settlement;
- commission payout;
- final customer payment;
- final refund;
- chargeback or dispute outcome;
- final tax adjustment; or
- final accounting correction.

This prevents premature deletion where a booking is created in one year but fulfilled, refunded or disputed later.

It does not permit unrelated payload fields to inherit the seven-year period.

### Successful raw payload

The 90-day clock begins only after:

- the supplier operation has reached a known outcome;
- the corresponding canonical record has been created or updated;
- reconciliation has succeeded; and
- no incident, dispute, mapping failure or legal hold requires reclassification.

### Exceptional raw payload

The 12-month clock begins after resolution, not initial receipt.

Resolution requires:

- a final known supplier outcome;
- final booking and financial reconciliation;
- correction of any mapping or processing defect;
- closure of the related incident, complaint or dispute; and
- confirmation that no legal hold applies.

### Support ticket

The two-year clock for a general support ticket starts at closure.

A ticket does not qualify for the general schedule if it contains evidence needed for:

- the booked transaction;
- a payment/refund/chargeback;
- a complaint with unresolved legal exposure;
- a security/privacy incident;
- a supplier dispute; or
- a legal hold.

Material evidence must be classified into the relevant controlled record before the general ticket is deleted.

### Backup

The 35-day period starts when each backup is created.

Backups are:

- access-restricted;
- encrypted;
- used only for disaster recovery;
- not restored merely to recover data deleted under the retention policy; and
- permitted to expire through normal generation rotation.

Where a restore occurs, deletion workflows must be reapplied before the restored environment is returned to normal operation.

## Record classification

### Canonical booking evidence

The canonical record may retain the minimum information necessary to establish:

- internal booking identifier;
- supplier and provider identifiers;
- supplier booking identifier;
- provider confirmation or PNR;
- product type;
- property, carrier or service identity;
- itinerary or stay details as purchased;
- room, rate, fare, cabin or service description necessary to explain the purchase;
- cancellation and refund conditions accepted at purchase;
- traveller or guest identity required to establish the booking;
- booking status and meaningful version history;
- price, currency, taxes, fees, commission and settlement;
- payment and refund references that do not contain prohibited payment data;
- customer consent or acceptance evidence;
- material servicing and support outcomes; and
- final completion, cancellation, refund or dispute outcome.

The canonical record must not become a permanent unfiltered copy of the supplier response.

### Raw payload allowlist

Raw supplier payload storage is permitted only for approved operation classes, such as:

- booking creation;
- booking retrieval;
- cancellation;
- refund;
- amendment or servicing;
- reconciliation;
- webhook delivery;
- ambiguous timeout or unknown outcome;
- mapping failure;
- supplier dispute; and
- security or privacy incident investigation.

Raw payload storage is not automatically permitted for:

- every search;
- all rate or fare results;
- bulk hotel content;
- bulk airline content;
- property images;
- reviews;
- full content feeds;
- large destination datasets;
- supplier inventory mirroring;
- competitive analysis;
- cross-supplier mapping datasets;
- model training; or
- indefinite analytics archives.

### Prohibited or specially protected data

Raw payload filters and classifiers must identify and prevent unnecessary retention of:

- payment card numbers and security codes;
- authentication secrets and API keys;
- identity-document images or unrestricted document numbers;
- unnecessary date-of-birth data;
- unnecessary contact details;
- free-text special requests containing sensitive information;
- accessibility, health, dietary or religious information not required for the retained purpose;
- supplier-internal credentials or tokens;
- complete content/images where a reference is sufficient; and
- data relating to unbooked travellers or discarded search candidates.

## Technical controls

### Storage separation

Store canonical records separately from raw evidence.

Raw evidence must use:

- a dedicated protected store;
- encryption at rest and in transit;
- strict role-based access;
- environment separation;
- immutable receipt metadata;
- automatic expiry;
- audited reads;
- no default user-interface exposure; and
- no unrestricted analytics access.

### Canonicalisation

Canonicalisation must:

- allowlist fields;
- normalise supplier-specific representations;
- preserve source references and evidence hashes;
- avoid copying transient supplier content without purpose;
- record meaningful immutable versions under ADR-0004;
- separate financial evidence from operational diagnostics; and
- support deletion of raw payloads without breaking the canonical booking history.

### Deletion

Deletion jobs must:

- run automatically;
- calculate expiry from the correct record-specific trigger;
- respect legal holds;
- delete all active copies;
- remove search/index/cache copies;
- create a non-sensitive deletion receipt;
- report failures;
- retry safely; and
- produce compliance metrics.

### Deletion receipt

A deletion receipt may retain:

- internal record identifier;
- record class;
- environment;
- supplier;
- original trigger date;
- scheduled deletion date;
- actual deletion date;
- deletion-job identifier;
- legal-hold check result; and
- success/failure status.

It must not preserve the deleted payload or unnecessary personal information.

### Legal hold

A legal hold must contain:

- matter identifier;
- authorised owner;
- legal basis or reason;
- issue date;
- scope criteria;
- affected record classes;
- affected customers, bookings and people where appropriate;
- review date;
- release authority; and
- release date.

A general concern, possible future usefulness or ordinary support request is not a legal hold.

### Access

Access to raw payload evidence must be restricted to approved roles for:

- supplier integration;
- incident response;
- reconciliation;
- privacy/security investigation;
- legal/compliance;
- authorised support escalation; and
- controlled engineering diagnosis.

Access must be time-limited where practical and audited.

## Production supplier-contract review

Before claiming final production compliance, review the executed LiteAPI commercial agreement and DPA for:

- permitted storage and caching;
- booking-record retention rights;
- raw request/response retention;
- hotel and flight content licensing;
- property, room, rate, airline, fare and image restrictions;
- prohibition on building derivative or mapping datasets;
- use of data for analytics and model training;
- retention after booking completion;
- retention after agreement termination;
- deletion and return obligations;
- data-subject rights assistance;
- processor/controller roles;
- subprocessors;
- international transfers;
- security incident evidence;
- audit rights;
- dispute and chargeback evidence;
- supplier-required retention;
- legal-hold handling; and
- priority between the executed terms and public website documentation.

Where the executed agreement is stricter than this schedule, the stricter supplier restriction applies unless Australian law requires retention.

Where Australian law requires a record but the supplier contract appears to prohibit retention, Legal/Compliance must define the minimum lawful canonical record and obtain supplier clarification rather than retaining the complete raw payload by default.

## Australian legal/privacy approval record

This document provides the proposed baseline for approval. It is not itself external legal advice.

The approving reviewers should confirm:

- that the seven-year classification is limited to qualifying financial and transaction evidence;
- that the five-year tax baseline and special cases have been considered;
- that raw payload periods are supported by an ongoing permitted purpose;
- that the trigger dates are reasonable and auditable;
- that personal information is minimised within retained canonical records;
- that backup handling meets APP 11 expectations;
- that legal holds are scoped and authorised;
- that cross-border processing and subprocessors are addressed;
- that traveller/guest notices accurately describe retention;
- that access and deletion requests can be fulfilled; and
- that the executed LiteAPI agreement does not require a different result.

### Approval table

| Role | Required approval | Name | Date | Decision/reference |
| --- | --- | --- | --- | --- |
| Data | Record classification, trigger implementation and deletion controls |  |  |  |
| Privacy | APP compliance, notices, data-subject handling and minimisation |  |  |  |
| Legal/Compliance | Australian record-keeping, legal hold and supplier-contract interpretation |  |  |  |
| Security | Protection, access, audit, backup and destruction controls |  |  |  |

Production compliance must not be declared until the required approvals are populated or linked to an equivalent controlled approval record.

## Production implementation evidence

The following evidence must exist before final production approval:

- retention rules implemented by record class;
- unit tests for trigger calculations;
- integration tests for automatic expiry;
- evidence that legal hold overrides deletion;
- evidence that release of a hold resumes the correct schedule;
- raw-payload allowlist enforcement;
- sensitive-field filtering;
- 35-day backup rotation evidence;
- documented restored-backup deletion procedure;
- deletion receipts;
- failed-deletion alerting;
- role/access review;
- raw-store read audit logs;
- customer ownership and record-isolation tests;
- encryption configuration;
- privacy access/deletion workflow;
- supplier-contract approval; and
- formal owner approval.

## Issue closure and production gate

### Why OI-0011 can close

The open product and architecture question has been answered:

- Option A is selected.
- Record classes are defined.
- Retention periods are defined.
- Trigger dates are defined.
- LiteAPI's public licence and privacy position has been recorded.
- Australian statutory and privacy baselines have been recorded.
- The implementation and deletion controls are defined.
- Unresolved evidence has been converted into explicit production approvals rather than an undefined data-model decision.

Keeping the architecture issue open would not change the implementation direction.

### What remains blocked

The system must not claim final production retention compliance until:

1. the executed LiteAPI agreement and DPA are reviewed;
2. Australian Privacy and Legal/Compliance approval is recorded;
3. the implemented controls are verified; and
4. customer-facing privacy and retention disclosures are approved.

This is a production-readiness gate, not an unresolved product architecture decision.

## Acceptance criteria

- [x] Retention schedule exists by record type.
- [x] Raw-payload allowlist and protection are defined.
- [x] Backup, deletion and legal-hold behaviour are defined.
- [x] ADR-0004 and the canonical-history approach are preserved.
- [x] LiteAPI's public retention, minimisation and licensing terms are recorded.
- [x] Australian company, tax and privacy baselines are recorded from primary sources.
- [x] Field classification and trigger dates are defined.
- [x] Executed-contract review requirements are explicit.
- [x] Formal legal/privacy approval is preserved as a production gate rather than assumed.
- [x] Implementation may proceed using the stricter defaults.
- [x] Final production compliance may not be claimed until the approval record and control evidence are complete.

## Closure statement

**OI-0011 is closed with Option A selected.**

readytogo.travel will retain supplier-neutral canonical booking and financial evidence for the approved legal/business period, while retaining allowlisted raw supplier payloads for shorter diagnostic, reconciliation, dispute or incident periods.

LiteAPI content will not be retained as a permanent replicated supplier database, used outside the permitted travel-service purpose, or reused for unrelated mapping, benchmarking, dataset enrichment or model training.

The executed LiteAPI agreement and DPA may impose stricter limits and remain a production authority. Australian Privacy and Legal/Compliance approval and implementation evidence remain mandatory before final production compliance is claimed.

The issue should be reopened if:

- LiteAPI's executed terms prohibit a required retention use;
- the DPA imposes materially different deletion or termination requirements;
- Australian legal/privacy review rejects a classification, period or trigger;
- the product begins retaining new categories of sensitive or licensed data;
- a new supplier requires a materially different schedule;
- a regulator or court requirement changes; or
- implementation cannot enforce the approved deletion and legal-hold controls.

## References

### LiteAPI / Nuitée

- [LiteAPI Terms of Service](https://liteapi.travel/terms/)
- [Data Protection and Privacy](https://docs.liteapi.travel/docs/data-protection-privacy)
- [Data Processing Agreement](https://docs.liteapi.travel/docs/data-processing-agreement-dpa-europe)
- [LiteAPI Privacy Policy](https://liteapi.travel/privacy/)
- [Security, Privacy and Compliance Overview](https://docs.liteapi.travel/docs/security-privacy-compliance-overview)

### Australia

- [Corporations Act 2001 — section 286](https://www.legislation.gov.au/C2004A00818/latest/text)
- [ATO — Overview of record-keeping rules for business](https://www.ato.gov.au/businesses-and-organisations/preparing-lodging-and-paying/record-keeping-for-business/overview-of-record-keeping-rules-for-business)
- [OAIC — APP 11 Security of personal information](https://www.oaic.gov.au/privacy/australian-privacy-principles/australian-privacy-principles-guidelines/chapter-11-app-11-security-of-personal-information)
- [OAIC — Guide to securing personal information](https://www.oaic.gov.au/privacy/privacy-guidance-for-organisations-and-government-agencies/handling-personal-information/guide-to-securing-personal-information)

## Limitations

This answer is based on:

- the OI-0011 issue and approved project schedule supplied on 28 July 2026;
- public LiteAPI/Nuitée documentation available on 28 July 2026; and
- public Australian legislation and regulator guidance available on 28 July 2026.

The executed LiteAPI commercial agreement, formal DPA, supplier-specific schedules, Australian legal advice and completed technical control evidence were not supplied for independent review.

This document is a data-governance and architecture decision record. It is not legal advice, a privacy impact assessment, an audit opinion or a contractual interpretation binding on LiteAPI.
