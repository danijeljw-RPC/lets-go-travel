---
document_type: legal-evidence-map
title: LiteAPI and Existing Open-Issue Mapping
status: supporting-record
reviewed: 2026-07-29
---

<!-- markdownlint-disable MD013 MD025 -->

# LiteAPI and Existing Open-Issue Mapping

## Purpose

This file maps the existing LiteAPI evidence work into the Australian legal launch pack so the same production evidence is not requested under inconsistent names.

## Mapping

| Existing issue | Australian legal-pack dependency | Launch treatment |
| --- | --- | --- |
| OI-0002 LiteAPI commercial/MOR | Contracting entity, merchant of record, settlement, statement descriptor, margin, refund funding, disputes, chargebacks, tax and booking-failure allocation | Mandatory before payment/booking activation. Determines role and customer terms. |
| OI-0003 Australian carrier capability | Production entitlement and dated Qantas, Jetstar and Virgin Australia lifecycle evidence | Mandatory before exposing a carrier as production-supported. |
| OI-0004 flight servicing | Retrieval, schedule changes, cancellation, exchange, refund and manual support boundary | Customer terms must use the selected partly manual, capability-gated promise. |
| OI-0005 webhook guarantees | Authentication, at-least-once handling, retry, duplication, ordering, replay/retention and environment evidence | Webhooks may be low-latency triggers only; reconciliation remains required. |
| OI-0006 payment/PCI | LiteAPI SDK, browser return, 3-D Secure, Australian methods, AOC and qualified SAQ scope | Mandatory before checkout activation. Supports outsourced card-data handling but does not remove merchant PCI duties. |
| OI-0011 retention validation | LiteAPI licence/DPA, selective raw payloads, canonical evidence and Australian privacy/legal approval | Mandatory before claiming final production retention compliance. |

## Customer-document consequences

### Booking role

The final booking terms must use OI-0002 to identify:

- platform entity;
- LiteAPI/Nuitée entity;
- merchant of record;
- travel supplier;
- payment descriptor;
- refund source;
- support route; and
- tax invoice/receipt issuer.

### Carrier promises

The flight UI and terms must use OI-0003 and OI-0004:

- no static carrier-support claim without production evidence;
- no universal ticketing promise;
- no real-time schedule-change promise;
- no universal self-service exchange/refund;
- direct-airline operational confirmation for day-of-travel information; and
- manual escalation disclosed.

### Booking state

The UI must use the LiteAPI booking result, not payment alone, before showing confirmation.

### Webhooks

OI-0005 supports:

- durable inbox;
- authentication;
- deduplication;
- asynchronous retrieval;
- scheduled reconciliation; and
- no webhook-only state mutation.

This is relevant to ACL accuracy and privacy/security, not merely technical reliability.

### Payments

OI-0006 supports the position that:

- the LiteAPI/provider component collects raw card data;
- readytogo.travel must not build custom card fields;
- readytogo.travel retains payment references only;
- the applicable SAQ/AOC still requires approval;
- mobile payment is deferred; and
- enabled Australian methods must be evidenced, not inferred.

### Retention

OI-0011 supports:

- seven-year canonical financial/booking evidence where legally required;
- 90-day allowlisted successful raw payload retention;
- 12 months after resolution for exceptional payloads;
- 35-day backup rotation;
- no indefinite raw supplier database;
- legal holds; and
- supplier contract/DPA precedence.

## Remaining consolidated legal questions

Australian counsel should answer the following in one advice:

1. Which operating entity contracts with Australian consumers under the `readytogo.travel` brand?
2. Is the entity agent, intermediary, principal or merchant of record for each product?
3. Do the proposed customer terms accurately allocate supplier and platform obligations without breaching the ACL or unfair-contract-term rules?
4. Is any travel-agent licence, registration or permit required in the operating jurisdictions for the exact model?
5. Is ATAS recommended or contractually required?
6. Does any insurance, wallet, remittance, FX, credit or payment activity require an AFSL, authorisation, credit licence, AML/CTF registration or another approval?
7. Does readytogo.travel ever receive customer money, and what legal relationship and insolvency treatment applies?
8. Are the customer-money disclosures accurate?
9. Does the Privacy Act apply to the entity, and are the APP 8 controls and notices sufficient?
10. Does the Children's Online Privacy Code apply at launch or later?
11. Are the minor-traveller authority and unaccompanied-minor rules sufficient?
12. Are the retention classes, periods and trigger dates approved?
13. Are the breach-response and notification procedures sufficient?
14. Which customer terms, notices and consents must be displayed and accepted?
15. What conditions must be completed before production launch?

## Closure use

Attach the completed approval checklist and counsel advice to the controlling launch issue. The controlling issue may then close without reopening each supplier OI, provided every OI's own production gate is satisfied or explicitly excluded from launch scope.
