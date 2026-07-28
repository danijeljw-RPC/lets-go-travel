<!-- markdownlint-disable MD013 -->

# Data Retention and Legal Hold

## Status

Approved project baseline on 2026-07-28 under the Option A direction in [OI-0011](../issues/open/OI-0011-supplier-payload-retention.md). Production activation remains subject to LiteAPI contractual terms and Australian legal/privacy review. A stricter applicable law, court/tribunal order, provider contract or matter-specific legal hold overrides the ordinary schedule only for the affected records.

## Principles

- Retain supplier-neutral canonical evidence rather than complete supplier payloads wherever possible.
- Retain the minimum fields needed to explain the transaction, booking, change, refund, dispute or support outcome.
- Never retain full PAN, CVV or sensitive authentication data.
- Do not copy full passport or identity-document values into canonical history, logs or support records.
- Give every retained record class an owner, purpose, authority, access rule, start event, expiry action and legal-hold behaviour.
- Destruction applies to primary data, replicas, archives, object versions and backups through their documented lifecycle.
- A legal hold suspends deletion only for records within its explicit scope; it does not create a blanket seven-year retention rule.

## Default Retention Schedule

The periods below are project policy, not a claim that Australian law requires every category for that period.

| Record class | Default period | Start event | Expiry action |
| --- | --- | --- | --- |
| Company financial records | 7 years | Completion of the transaction represented by the record | Destroy non-required copies; retain only the protected accounting evidence required by law. |
| Canonical booking, price, payment, refund and immutable version evidence | 7 years | Later of final travel completion or final booking, cancellation, refund, chargeback or dispute resolution | Destroy or irreversibly de-identify personal fields that are not required in the retained financial/evidentiary record. |
| Booking-related support ticket thread evidence | 7 years | Later of ticket closure or final linked booking/payment dispute resolution | Destroy unnecessary personal content; retain only the minimum protected thread evidence. Attachments follow their separate schedule. |
| General account/support tickets with no booking or financial relationship | 2 years | Ticket closure | Destroy the ticket, token material and attachments unless a legal hold applies. |
| Support attachments | 90 days | Ticket closure | Destroy unless an authorised reviewer promotes the attachment to required booking/dispute evidence or a legal hold. |
| Successful raw supplier request/response payloads on the approved allowlist | 90 days | Receipt | Destroy the payload; keep only canonical state, hashes, references and permitted operational metadata. |
| Raw payloads for ambiguous operations, mapping failures, incidents or disputes | 12 months | Resolution of the operation, incident or dispute | Destroy unless a documented legal hold or stricter legal requirement remains active. |
| Webhook payload bodies | 90 days | Receipt | Destroy the body; retain event identity, authentication result, hash, correlation and processing outcome for 2 years. |
| Customer-notification rendered content | 90 days | Final delivery attempt | Destroy rendered content; retain recipient reference, template/version, locale, status and timestamps for 2 years. |
| Diagnostic application logs | 90 days | Log event | Delete through the log-platform lifecycle. Logs must not contain complete supplier payloads or sensitive traveller/payment data. |
| Security and privileged-access audit records | 2 years | Audit event | Destroy unless linked to an active investigation, incident or legal hold. |
| Search results, abandoned offers and unconfirmed checkout state | 30 days | Last customer or system activity | Destroy unless needed for an ambiguous payment/booking investigation. |

The seven-year company-financial-record baseline follows section 286 of the [Corporations Act 2001](https://www.legislation.gov.au/C2004A00818/latest/text). General Australian business tax records are commonly subject to a five-year rule, with exceptions, under [ATO Taxation Ruling TR 96/7](https://www.ato.gov.au/law/view/document?LocID=%22TXR%2FTR967%2FNAT%2FATO%2F00001%22). The project uses seven years for the combined financial/canonical evidence class so one protected record set satisfies the longer baseline without preserving every raw supplier payload.

Where the Australian Privacy Principles apply, personal information that is no longer needed must be destroyed or de-identified unless an Australian law or court/tribunal order requires retention. The [OAIC APP 11 guidance](https://www.oaic.gov.au/privacy/australian-privacy-principles/australian-privacy-principles-guidelines/chapter-11-app-11-security-of-personal-information) also requires reasonable treatment of archived and backup copies.

## Raw Supplier Payload Allowlist

Raw payload retention is disabled by default. It may be enabled only for:

- a supplier operation with an ambiguous outcome;
- a mapping/canonicalisation failure that cannot be diagnosed from redacted metadata;
- an active security or reliability incident;
- a customer dispute, chargeback, refund or supplier escalation requiring source evidence;
- a contractually permitted conformance fixture that has been irreversibly de-identified.

Every retained payload records purpose, booking/operation reference, supplier, classification, creation time, expiry time, access policy, encryption/key reference and legal-hold identifier when applicable. Access is least-privilege, audited and unavailable to routine customer-support roles unless specifically authorised for the case.

Payloads containing full card data, CVV, authentication secrets or unneeded identity-document values are prohibited. If a supplier response unavoidably contains sensitive traveller values, ingestion must redact or isolate them before ordinary payload retention. The shorter expiry applies unless a documented law or legal hold requires the specific fields.

## Legal Hold

A legal hold is a matter-specific suspension of ordinary destruction. It may be opened for an actual or reasonably anticipated dispute, litigation, regulatory investigation, audit, subpoena, court/tribunal order, material chargeback, refund claim or security incident.

Each hold records:

- hold ID and matter reference;
- reason and legal/compliance authority;
- authorised owner;
- affected customers, bookings, tickets, record classes and date range;
- start time and next review date;
- access restrictions and preservation method;
- release authority, release time and release reason.

The legal/compliance owner reviews an active hold at least every 90 days. Records outside its scope continue through normal deletion. Held records are immutable or integrity-protected, access-controlled and audited. A hold does not require recovery of data that was lawfully destroyed before the hold began.

When the owner releases a hold, each record resumes its ordinary lifecycle. Records already past their expiry are destroyed within 30 days of release unless another hold or retention requirement applies.

## Backup and Deletion Behaviour

Primary stores run expiry processing at least daily. Ordinary encrypted database and object-store backup copies expire within 35 days. Deletion tombstones or equivalent suppression records are reapplied after a restore so expired personal data does not silently return to active use.

If a legal hold needs evidence beyond the ordinary backup window, the affected records are exported to a separate encrypted hold repository with matter-scoped access and integrity metadata. Routine backups are not retained indefinitely as a substitute for a legal-hold process.

Deletion produces an audit receipt containing record class, policy version, count, deletion/de-identification method, completion time and failure status without copying the deleted personal content. Repeated failures alert operations and remain visible until resolved.

## Account Closure and Sensitive Traveller Data

Account closure removes access and schedules eligible customer/profile data for deletion, but it does not rewrite confirmed-booking, financial, refund, dispute or held evidence. Retained evidence is minimised and access-restricted.

Reusable date-of-birth, passport and identity-document storage follows closed [OI-0008](../issues/closed/OI-0008-saved-traveller-and-passport-data.md). It is separate from booking evidence, remains off by default and is removed when consent is withdrawn or the account lifecycle requires deletion, subject only to a documented legal requirement or hold. Full identity-document values do not enter the seven-year canonical evidence set by default.

Within 90 days after final travel completion, a minimisation job removes or irreversibly masks date-of-birth, contact and direct traveller-identification fields that are not required to explain the retained transaction, booking or dispute. Stable internal traveller/booking identifiers preserve evidence relationships without retaining unnecessary document data.

## Remaining Production Evidence

- LiteAPI contractual payload-retention and content-licensing terms.
- Australian legal/privacy confirmation of record classification and trigger dates.
- Confirmation that payment, tax and supplier-settlement records captured by the platform are sufficient without retaining complete provider payloads.
- Verification that selected PostgreSQL, object-storage, backup and log services can enforce the schedule and legal-hold controls.
