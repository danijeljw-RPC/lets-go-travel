---
document_type: legal-launch-decision
title: Australian Market Launch Gate Decision
status: closed-as-design-decision
reviewed: 2026-07-29
owners:
  - Legal/Compliance
  - Product
---

<!-- markdownlint-disable MD013 MD025 -->

# Australian Market Launch Gate Decision

## Selected direction

The Australian launch uses a conservative travel-intermediary model with capability-gated supplier products and explicit production approvals.

The platform may be built before legal approval, but production transactions cannot be enabled until all mandatory evidence is approved.

## Fixed product decisions

### Business identity and role

- Every customer journey identifies the contracting operating entity by full legal name, ABN, contact details and registered or principal business address.
- Every product states whether the operating entity acts as agent/intermediary, principal, reseller or merchant of record.
- Supplier identity and incorporated supplier terms are disclosed before purchase where they affect the customer.
- The platform never uses ambiguous wording that suggests the platform itself is the airline, hotel or travel operator when it is not.

### Price and sale

- Australian point-of-sale prices default to Australian dollars unless the customer intentionally selects or is clearly shown another charging currency.
- The earliest actionable offer shows the minimum total price that can actually be paid, including taxes and unavoidable or pre-selected fees that can be quantified.
- Optional extras are not pre-selected by default.
- Card surcharges are applied only where legally permitted, supported by current acceptance-cost evidence and displayed in accordance with the minimum-total-price rules.
- The platform does not send a confirmation email or show a confirmed state until supplier confirmation exists.
- Price or availability changes before confirmation require renewed customer acceptance.

### Refunds, cancellations and changes

- Customer terms distinguish platform fees, supplier penalties and refundable supplier amounts.
- Australian Consumer Law rights are not excluded, restricted or misrepresented.
- Supplier rules do not automatically override non-excludable rights.
- Unsupported exchanges, refunds, cancellations or servicing actions are described as request-based and subject to supplier confirmation, not guaranteed self-service actions.
- Refund status distinguishes requested, supplier-approved, initiated, settled and failed.

### Privacy and data

- The platform implements the Australian Privacy Principles as the baseline unless written advice approves a narrower scope.
- Personal information is collected only where reasonably necessary for booking, payment, servicing, legal, security or approved operational purposes.
- Sensitive information, including health or accessibility information, is collected only where necessary and with appropriate consent or another documented legal basis.
- Overseas disclosures are identified, contractually controlled and recorded.
- Australian primary hosting is preferred for Australian customer data, but cross-border compliance is based on APP 8 and contractual controls rather than an unsupported claim that all data must remain in Australia.
- Raw supplier data follows the approved selective-retention model rather than indefinite retention.

### Travel-selling and licensing

- No general Australian travel-agent licence is assumed to be required, but each product and operating jurisdiction is reviewed for special licensing or permit triggers.
- ATAS accreditation is treated as voluntary unless a supplier, investor, customer contract or commercial strategy makes it mandatory.
- Travel insurance is not offered, recommended, arranged or bundled until an approved AFSL/authorised-representative or other lawful model is recorded.
- Direct airline ticket issuance is not represented unless the relevant IATA, consolidator or supplier ticketing authority exists.
- Stored value, customer wallets, remittance, foreign exchange, credit and buy-now-pay-later functionality are outside launch scope until separately reviewed.

### Client money and insolvency

- The preferred payment path sends customer funds directly to the approved merchant of record or payment provider.
- Where the operating entity receives travel funds, funds flow, legal title, supplier liability, refund funding and insolvency treatment must be documented.
- Operational segregation is required where customer travel funds are temporarily held.
- The word `trust` is not used unless counsel confirms a legally constituted trust arrangement and the account is operated accordingly.
- Customer money is never represented as protected against insolvency unless an enforceable protection actually exists.

### Minors

- The purchasing account holder must be at least 18.
- A purchaser booking for a traveller under 18 must confirm authority to do so and provide required guardian/emergency details.
- Child-only and unaccompanied-minor bookings are disabled unless the exact supplier, carrier, route, age, documentation and servicing capability has been approved.
- The platform does not determine family-law rights or authorise international travel contrary to parental responsibility or court orders.
- Child privacy and age-appropriate notice requirements are reviewed before the Children's Online Privacy Code is registered by 10 December 2026.

### Breach response

- A formal breach-response plan, incident register and decision log are mandatory.
- Suspected eligible data breaches are assessed expeditiously, with reasonable steps taken to complete the assessment within 30 days where the NDB scheme applies.
- Eligible breaches are notified to the OAIC and affected individuals as soon as practicable.
- Supplier and subprocessor contracts must require rapid security-incident notice and cooperation.
- Payment incidents, passport/document exposure and detailed itinerary exposure receive high-severity assessment.

## Explicitly prohibited launch claims

The platform must not claim:

- all prices are final where local property or government charges may still apply;
- a booking is confirmed before supplier confirmation;
- all Australian carriers, hotels or markets are supported;
- all cancellations, exchanges or refunds are automated;
- customer funds are held on trust or protected against insolvency without a valid legal structure;
- all data is stored only in Australia unless technically and contractually true;
- the platform is PCI compliant merely because LiteAPI's SDK is used;
- the platform has real-time flight operational data where it only has supplier booking retrieval;
- a minor may travel without required guardian, passport, visa, airline or court documentation; or
- final Australian legal compliance has been approved before the checklist is signed.

## Launch blockers

Production remains blocked until:

- the exact Australian operating and contracting entity is approved;
- OI-0002 merchant-of-record and commercial terms are approved;
- the customer funds-flow and insolvency treatment are approved;
- the Australian privacy assessment and cross-border schedule are approved;
- the provider AOC and PCI scope are approved;
- final customer terms, privacy policy and collection notices are approved;
- travel-insurance functionality is either disabled or lawfully authorised;
- minor-traveller limitations are implemented;
- the breach-response plan is exercised;
- customer support and complaint processes are operational; and
- the approval record in [09-legal-approval-checklist.md](09-legal-approval-checklist.md) is complete.

## Re-review triggers

Re-review is mandatory for:

- a new merchant of record;
- readytogo.travel receiving or controlling customer travel funds;
- customer wallet, stored value, credit or foreign exchange;
- travel insurance or another financial product;
- a new supplier country or data-hosting country;
- direct ticket issuance;
- a package where the operating entity becomes principal for multiple travel services;
- child-directed features or direct minor accounts;
- material AI or automated decisions using personal information;
- new health, passport, biometric or identity-document processing;
- a material change to refund or customer-money protection;
- acquisition or insolvency risk affecting customer funds; or
- material amendments to the ACL, Privacy Act or Children's Online Privacy Code.
