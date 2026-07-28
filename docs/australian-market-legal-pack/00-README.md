---
document_type: legal-pack-index
title: Australian Market Legal and Compliance Launch Pack
status: baseline-approved
jurisdiction: Australia
reviewed: 2026-07-29
owners:
  - Legal/Compliance
  - Product
contributors:
  - Finance
  - Privacy
  - Security
  - Data
  - Operations
  - Supplier Integration
production_status: blocked-pending-approval
---

<!-- markdownlint-disable MD013 MD025 -->

# Australian Market Legal and Compliance Launch Pack

## Original requirement

| Review | Evidence or approval required | Owner | Effect |
| --- | --- | --- | --- |
| Australian market legal pack | Consumer-law/pricing, privacy, travel-selling/licensing, insolvency/trust, cross-border/data-residency, minor-traveller and breach-response advice. | Legal/Compliance, Product | Blocks production launch and final customer terms, not application scaffolding. |

## Executive decision

The Australian market baseline is now defined.

Application scaffolding, provider-neutral integration, sandbox implementation and stricter compliance controls may proceed against this pack. Production launch, payment activation and publication of final customer terms remain blocked until the approvals in [09-legal-approval-checklist.md](09-legal-approval-checklist.md) are completed.

The approved minimum position is:

- display the minimum total price prominently and early, including taxes and unavoidable or pre-selected fees;
- do not confirm a booking until the supplier has actually confirmed it;
- preserve Australian Consumer Law rights and avoid unfair standard-form terms;
- assume the Privacy Act and Australian Privacy Principles apply unless Australian privacy counsel records a narrower conclusion;
- use Australian primary hosting as a policy preference, while treating overseas supplier and infrastructure disclosures under APP 8 rather than claiming that Australian law generally prohibits overseas hosting;
- operate as an expressly disclosed intermediary or agent except where an approved product-specific contract makes the operating entity the principal or merchant of record;
- do not sell or arrange travel insurance without an approved Australian financial-services authorisation model;
- prefer payment flows where the approved merchant of record receives customer funds directly;
- do not describe an ordinary operating account as a trust account;
- require an adult purchaser and guardian authority for bookings involving travellers under 18;
- capability-gate unaccompanied-minor travel by carrier, property, provider and route;
- maintain a documented data-breach response plan, complete suspected eligible-data-breach assessments expeditiously and within the statutory 30-day maximum where applicable, and notify the OAIC and affected individuals as soon as practicable when required; and
- complete a specific review of the Children's Online Privacy Code before its required registration date of 10 December 2026 and before any child-directed functionality launches.

## Closure position

This pack closes the undefined product and architecture question because the launch baseline, prohibited behaviours, required controls, customer-document schedule and approval evidence are now specified.

Closure does not mean that external legal advice has been received or that production compliance may be claimed. The remaining work is a controlled launch approval, not an unresolved design choice.

## Pack contents

| File | Purpose |
| --- | --- |
| [01-launch-gate-decision.md](01-launch-gate-decision.md) | Closure statement, fixed decisions and launch blockers. |
| [02-consumer-law-and-pricing.md](02-consumer-law-and-pricing.md) | ACL, price display, booking confirmation, refunds, unfair terms and complaints. |
| [03-privacy-cross-border-and-data-residency.md](03-privacy-cross-border-and-data-residency.md) | Privacy Act baseline, APP 8, residency, notices, sensitive data and children. |
| [04-travel-selling-licensing-and-financial-services.md](04-travel-selling-licensing-and-financial-services.md) | Travel-agent licensing position, ATAS, ticketing, travel insurance and adjacent licensing triggers. |
| [05-client-money-insolvency-and-trust.md](05-client-money-insolvency-and-trust.md) | Funds flow, insolvency, segregation, trust language and prudential controls. |
| [06-minor-travellers.md](06-minor-travellers.md) | Adult purchaser, guardian authority, unaccompanied minors, privacy and documents. |
| [07-data-breach-response.md](07-data-breach-response.md) | Incident handling, NDB assessment, notification and supplier coordination. |
| [08-customer-terms-and-notices.md](08-customer-terms-and-notices.md) | Required customer documents and clause schedule. |
| [09-legal-approval-checklist.md](09-legal-approval-checklist.md) | Evidence and sign-off required before launch. |
| [10-source-register.md](10-source-register.md) | Primary sources and supplier references used by the pack. |
| [11-liteapi-and-existing-oi-mapping.md](11-liteapi-and-existing-oi-mapping.md) | Mapping to the existing LiteAPI OI evidence and production gates. |

## How to use this pack

1. Product and Architecture implement the fixed baseline.
2. Finance, Privacy, Security, Data, Operations and Supplier Integration attach their evidence to the checklist.
3. Australian counsel reviews the exact entity, funds flow, customer terms, privacy model and product scope.
4. Legal/Compliance records approval, conditions or required changes.
5. Product enables only the products and capabilities covered by the approval.
6. Any material change to merchant of record, payment flow, insurance, credit, stored value, supplier role, data location, minors flow or customer promise triggers re-review.

## Not legal advice

This pack is a research-backed product, architecture and approval brief. It is not a legal opinion, tax advice, financial-services advice, privacy impact assessment or representation that the business is compliant. Australian counsel must approve the final operating model and customer documents.
