# OI-0002 — LiteAPI Commercial and Merchant-of-Record Evidence Review

**Status:** Open — public documentation provides preliminary evidence but does not satisfy production approval
**Vendor:** LiteAPI / Nuitee Connect
**Contracting entity identified in public terms:** Nuitée Travel Limited
**Reviewed:** 28 July 2026
**Decision owners:** Product, Finance, Legal/Compliance

## Purpose

This document records the public LiteAPI/Nuitee Connect evidence relevant to the following open issue:

| Review | Evidence or approval required | Owner | Effect |
| --- | --- | --- | --- |
| OI-0002 LiteAPI commercial/MOR | Executed terms covering merchant, settlement, statement descriptor, commission/margin, refunds, disputes, chargebacks, tax and booking-failure ownership. | Product, Finance, Legal/Compliance | Blocks production payment and booking activation, not provider-neutral implementation. |

The objective is to distinguish:

1. what LiteAPI publicly documents;
2. what can be used as preliminary vendor evidence; and
3. what still requires executed commercial terms or written confirmation from LiteAPI.

## Executive conclusion

LiteAPI's public documentation establishes that two merchant-of-record models are available:

1. **Nuitee Connect as merchant of record** when the application uses the Nuitee Connect payment SDK; or
2. **the integrating organisation as merchant of record** when it implements its own customer payment layer and pays LiteAPI using an account credit card, wallet or another agreed account-level payment arrangement.

The public documentation also describes:

- configurable commission through the `margin` and `additionalMarkup` fields;
- weekly commission payouts after the guest completes the stay and checks out;
- cancellation-policy-driven refunds and cancellation charges;
- release of a payment hold after one to two business days when payment succeeds but the booking is not finalised; and
- customer responsibility for PCI DSS obligations according to the selected integration model.

This is useful architectural and commercial evidence, but it is **not sufficient to close OI-0002**.

The reviewed public material does not provide an account-specific, legally binding allocation of:

- customer card-statement descriptor;
- settlement account, settlement currency and reconciliation mechanics;
- reserves, deductions, negative balances or payout adjustments;
- dispute and chargeback handling;
- chargeback fees and financial-loss ownership;
- GST, VAT, sales-tax, accommodation-tax and invoicing responsibilities;
- refund funding and processing-fee treatment;
- customer support and remediation ownership;
- supplier failure, unsuccessful confirmation or post-payment booking-failure losses; or
- the priority between public documentation and negotiated commercial terms.

Production payment collection and live booking activation should therefore remain blocked until the selected model is covered by executed terms or explicit written vendor confirmation.

## Public evidence assessment

| Topic | Public LiteAPI evidence | Assessment | Remaining production evidence |
| --- | --- | --- | --- |
| Merchant of record — LiteAPI/Nuitee | The Revenue Management and Commission guide states that Nuitee Connect can act as merchant of record when its payment SDK is used and handles payment transactions on the application's behalf. | Publicly documented. | Confirm the exact contracting entity, supported countries/currencies, card-statement descriptor, payment processor, settlement mechanics, refund funding, dispute handling and chargeback allocation. |
| Merchant of record — integrating organisation | The same guide states that the integrating organisation may be merchant of record, use net rates with `margin: 0`, apply its own revenue model and manage the payment process. The Account Credit Card guide says the application implements its own customer-facing payment layer. | Publicly documented. | Confirm contractual permission for the intended pricing model, rate-parity or supplier-price restrictions, permitted service fees, tax treatment and responsibility for customer claims. |
| Payment methods | LiteAPI documents User Payment, Account Credit Card, Account Wallet and Credit Line options. Credit Line requires a separate contract and normally a credit review or deposit. | Publicly documented at a functional level. | Confirm which methods are approved for the production account and the commercial conditions attached to each method. |
| Commission and margin | LiteAPI documents `margin` and `additionalMarkup`; `margin: 0` returns a net rate, while a non-zero margin adds commission to the selling price. | Publicly documented operational behaviour. | Confirm permitted margin ranges, currency treatment, rounding, supplier restrictions, exclusions, reversals, cancellation effects and reporting. |
| Commission payout | LiteAPI states that commissions on confirmed bookings are paid weekly. It defines confirmation for payout as the guest completing the stay and checking out, after which commission is included in the next weekly payout. | Payout cadence is publicly described. | Confirm payout destination, currency, minimum threshold, remittance advice, failed payout handling, offsets, withholding, reconciliation and right to amend prior commissions. |
| Refunds and cancellations | LiteAPI documents refundable and non-refundable rates. Cancellation policies determine whether a full refund, partial refund or charge applies. Its FAQ states that the applicable refund is credited to the card used for the booking upon cancellation. | Public operational behaviour is documented. | Confirm who funds the refund, whether payment-processing fees are returned, refund timing guarantees, manual refund processes, partial refund support and liability for incorrect supplier policies. |
| Payment succeeds but booking is not finalised | The User Payment guide states that, for a lost or non-finalised booking, the payment hold remains for one to two business days before release. | One specific failure mode is publicly documented. | Confirm whether the transaction is an authorisation or captured payment in each flow, who communicates with the customer, escalation procedures, compensation exposure and ownership where the hold is not released correctly. |
| Failed supplier confirmation after payment | No complete contractual allocation was identified in the reviewed public pages. | Not resolved by public documentation. | Obtain written allocation of refund, rebooking, price difference, support, compensation and financial-loss ownership. |
| Card-statement descriptor | No statement-descriptor commitment or configuration rule was identified in the reviewed public pages. | Not resolved by public documentation. | Obtain the exact default descriptor, configurable fields, regional variations and whether the descriptor identifies Nuitée, LiteAPI, the platform or another payment entity. |
| Settlement and reconciliation | Public documentation describes weekly commission payout and account funding methods, but does not provide complete settlement and reconciliation terms. | Partially documented. | Obtain settlement schedules, currencies, bank-account requirements, reports, transaction identifiers, reserves, offsets, negative balances and correction procedures. |
| Disputes and chargebacks | No allocation of dispute intake, evidence submission, deadlines, fees or loss ownership was identified in the reviewed public pages. | Not resolved by public documentation. | Obtain the full dispute and chargeback operating model for each merchant-of-record option. |
| Taxes | API responses can expose rate-level taxes and fees, but the reviewed public documentation does not allocate legal responsibility for calculating, collecting, invoicing, remitting or reporting taxes. | Pricing data is documented; tax ownership is not. | Confirm GST, VAT, sales tax, accommodation tax, withholding, tax invoices/receipts, place-of-supply rules and treatment of the platform's own service fees or margin. |
| PCI DSS | LiteAPI states that raw card data is handled by PCI-compliant third-party providers and that customers remain responsible for their own PCI DSS obligations based on the integration model. | Public compliance boundary is partially documented. | Obtain the applicable Attestation of Compliance or other evidence under NDA and confirm the application's exact SAQ scope. |
| Public Terms of Service | The public terms identify Nuitée Travel Limited and describe Nuitee Connect as SaaS/PaaS for hotel content, pricing and booking. They contain general availability, liability, indemnity and Irish governing-law terms. | Useful entity and baseline legal evidence. | The public terms do not contain the detailed account-specific payment and commercial allocation required by OI-0002. Executed terms are still required. |

## Detailed findings

### 1. Merchant-of-record models

LiteAPI's Revenue Management and Commission guide expressly describes both available models.

Under the **Nuitee Connect merchant-of-record model**, the application uses the Nuitee Connect payment SDK and Nuitee Connect handles payment transactions on the application's behalf.

Under the **integrating-organisation merchant-of-record model**, the organisation works from net rates, ordinarily using `margin: 0`, implements its own payment layer and applies its own commission or bundling model.

This public evidence is sufficient to support an architecture that keeps payment and booking-provider boundaries configurable. It is not sufficient to select or activate a production model without commercial approval.

Sources:

- [Revenue Management and Commission](https://docs.liteapi.travel/docs/revenue-management-and-commission)
- [Implementing a Payment Method](https://docs.liteapi.travel/docs/implementing-payment)
- [User Payment — Nuitee SDK](https://docs.liteapi.travel/docs/user-payment)
- [Account Credit Card](https://docs.liteapi.travel/docs/account-credit-card)

### 2. Commission, margin and payout

LiteAPI documents a configurable `margin` percentage and an `additionalMarkup` percentage.

The guide states that:

- `margin: 0` returns a net rate;
- a non-zero `margin` adds commission;
- the seller can control the margin;
- commissions are paid weekly; and
- commission becomes locked for payout after the guest completes the stay and checks out.

The public documentation does not define all accounting and settlement conditions. Executed terms should cover reversals, cancellations, no-shows, fraud, supplier adjustments, currency conversion, rounding, payout thresholds, withheld amounts and reconciliation evidence.

Source:

- [Revenue Management and Commission](https://docs.liteapi.travel/docs/revenue-management-and-commission)

### 3. Refund and cancellation behaviour

LiteAPI documents that a booking may be:

- non-refundable;
- fully refundable before a policy deadline;
- partially refundable; or
- subject to cancellation charges after a policy deadline.

Its FAQ states that the applicable refund is credited to the credit card used for the booking upon cancellation.

This explains API behaviour but does not answer the legal and financial questions needed for production, including who funds refunds, whether payment fees are recoverable, how long refunds may take, who handles exceptions and who bears losses caused by inaccurate supplier cancellation data.

Sources:

- [Canceling a Booking](https://docs.liteapi.travel/docs/canceling-a-booking)
- [Cancel a Booking API](https://docs.liteapi.travel/reference/put_bookings-bookingid)
- [LiteAPI FAQ — Payment Questions](https://docs.liteapi.travel/docs/faq)

### 4. Payment-success and booking-failure handling

The User Payment guide requires the application to preserve the `transactionId` and `prebookId` between payment and final booking.

It states that when a booking is lost or not finalised, the payment hold remains for one to two business days before release.

This is relevant evidence for one booking-failure state, but it does not allocate:

- customer communication;
- operational escalation;
- refund or release failures;
- rebooking and fare/rate differences;
- goodwill compensation;
- support ownership;
- losses from duplicate or mismatched transactions; or
- liability when the supplier cannot confirm after funds have been captured.

Source:

- [User Payment — Nuitee SDK](https://docs.liteapi.travel/docs/user-payment)

### 5. Payment compliance boundary

LiteAPI states that it does not store, process or transmit raw card data and relies on PCI-compliant third-party payment providers through a proxy or redirect model. It also states that customers remain responsible for their own PCI DSS obligations according to their integration model.

This supports the need to assess the exact payment design rather than assuming that use of LiteAPI removes all PCI DSS obligations.

Source:

- [Regulatory Compliance](https://docs.liteapi.travel/docs/regulatory-compliance)

### 6. Public Terms of Service

LiteAPI's public Terms of Service:

- identify the service owner as Nuitée Travel Limited;
- identify the company as incorporated and registered in Ireland;
- describe Nuitee Connect as a SaaS/PaaS service;
- permit use of the service to deliver travel services to end users;
- disclaim uninterrupted availability; and
- include general liability, indemnity, governing-law and jurisdiction provisions.

The reviewed public terms do not contain the detailed merchant, settlement, descriptor, dispute, chargeback, tax, refund-funding or booking-failure allocation required for OI-0002.

The public Terms of Service must therefore be treated as baseline public legal material, not as a substitute for the required executed commercial agreement.

Source:

- [LiteAPI / Nuitee Connect Terms of Service](https://liteapi.travel/terms/)

## Required vendor evidence before production activation

Product, Finance and Legal/Compliance should obtain the following for the selected production model.

### Contract and entity

- Executed agreement with the exact Nuitée contracting entity.
- Order form, commercial schedule or account-specific addendum.
- Confirmation of document priority where the agreement, public terms and developer documentation conflict.
- Supported legal entities, markets, currencies and customer countries.

### Merchant of record and customer-facing identity

- Explicit merchant-of-record designation for every payment path.
- Exact customer card-statement descriptor.
- Descriptor configuration and regional variations.
- Identity displayed on payment pages, receipts, invoices, refund notifications and dispute records.
- Responsibility for customer terms and privacy notices.

### Funds flow and settlement

- Customer-to-merchant funds-flow diagram.
- LiteAPI/supplier payment flow.
- Settlement and payout timing.
- Settlement currencies and foreign-exchange treatment.
- Bank-account and payout-provider requirements.
- Reserve, rolling reserve, withholding and negative-balance rights.
- Reconciliation reports and stable transaction identifiers.
- Treatment of cancellations, no-shows, amendments and supplier adjustments.

### Commission and margin

- Permitted `margin` and `additionalMarkup` use.
- Rate-parity, minimum-selling-price or supplier restrictions.
- Calculation and rounding rules.
- Tax treatment of commission, markup and service fees.
- Timing of commission recognition.
- Reversal and clawback conditions.
- Payout thresholds and failed-payout procedures.

### Refunds, disputes and chargebacks

- Party responsible for approving and executing refunds.
- Party funding refunds.
- Treatment of payment-processing fees.
- Refund service levels.
- Dispute notification and response process.
- Evidence-submission responsibilities.
- Chargeback fees.
- Chargeback-loss allocation.
- Fraud-screening and fraud-loss allocation.
- Rights to debit reserves, wallets, cards or future commissions.

### Tax and invoicing

- GST, VAT, sales-tax and accommodation-tax responsibility.
- Place-of-supply treatment.
- Tax treatment of platform service fees and margins.
- Party issuing the customer receipt or tax invoice.
- Supplier invoice or voucher documentation.
- Withholding-tax handling.
- Required legal disclosures by country.

### Booking failure and customer remediation

- Ownership where payment succeeds but booking confirmation fails.
- Ownership where the supplier rejects or later cancels the booking.
- Rebooking and price-difference responsibility.
- Refund timing and payment-release escalation.
- Duplicate booking or duplicate payment handling.
- Customer support hand-off and service levels.
- After-hours and in-stay support.
- Goodwill, compensation and consequential-loss rules.
- Incident reporting and reconciliation obligations.

## Questions to send LiteAPI

1. Which legal entity will contract with us for production use?
2. Can Nuitée Connect act as merchant of record for customers in all intended launch countries and currencies?
3. What exact descriptor appears on the customer's card statement when the Nuitee Connect payment SDK is used?
4. Can the statement descriptor be configured to include our brand?
5. Who receives and manages payment disputes under the Nuitee merchant-of-record model?
6. Who pays chargeback fees and bears the final chargeback loss?
7. Who funds customer refunds, and are payment-processing fees refundable?
8. What happens financially and operationally when payment succeeds but the hotel booking cannot be confirmed?
9. Who pays any price difference if the customer must be rebooked?
10. What settlement and commission reports are available for reconciliation?
11. Which currencies are used for customer payment, supplier payment and commission payout?
12. Can LiteAPI offset refunds, disputes or other losses against commissions or future payouts?
13. Which party calculates, collects, invoices, remits and reports GST, VAT, sales tax and accommodation taxes?
14. Who issues the customer receipt or tax invoice?
15. Are there rate-parity, minimum-selling-price or supplier restrictions on our margin and service fees?
16. What customer-support obligations remain with us when Nuitée Connect is merchant of record?
17. What executed agreement, order form, payment schedule and data-processing documents apply to our account?
18. Which document prevails if public developer documentation conflicts with our executed commercial terms?

## Recommended OI-0002 entry

| Review | Evidence or approval required | Owner | Effect |
| --- | --- | --- | --- |
| OI-0002 LiteAPI commercial and merchant-of-record model | LiteAPI public documentation confirms that Nuitée Connect can act as merchant of record when its payment SDK is used, while the integrating organisation can act as merchant of record when it implements its own customer-payment layer and pays LiteAPI through an account-level payment method. Before production activation, obtain executed LiteAPI/Nuitée commercial terms or written vendor confirmation covering the selected merchant-of-record model, contracting entity, customer statement descriptor, payment settlement and reconciliation, commission/margin calculation and payout, refunds and cancellation charges, disputes, chargebacks and associated fees, GST/VAT and other tax responsibilities, PCI DSS boundaries, and financial/customer-service ownership where payment succeeds but the hotel booking fails or cannot be confirmed. | Product, Finance, Legal/Compliance | Blocks production payment collection and live booking activation. Does not block provider-neutral API integration, sandbox implementation or implementation support for both merchant-of-record models. |

## Recommended decision status

**Keep OI-0002 open.**

Public LiteAPI documentation may be attached to the issue as **preliminary vendor evidence**, but closure should require one of the following:

1. executed commercial terms containing the required allocation;
2. an executed commercial schedule plus incorporated payment/refund/dispute terms; or
3. written confirmation from an authorised LiteAPI/Nuitée representative that is accepted by Product, Finance and Legal/Compliance.

## Architecture impact

OI-0002 should not block provider-neutral implementation.

The implementation should avoid hard-coding either merchant-of-record model and should keep the following concerns behind explicit provider and configuration boundaries:

- payment collection;
- merchant-of-record designation;
- customer statement and receipt identity;
- margin and service-fee calculation;
- supplier payable amount;
- commission receivable;
- settlement and reconciliation;
- cancellation and refund orchestration;
- dispute and chargeback case handling;
- tax classification and invoice ownership;
- payment-authorised-but-booking-unconfirmed handling; and
- customer support ownership.

Production feature flags should prevent live payment and booking activation until the commercial model has been approved and recorded.

## Reference register

| Reference | Relevance |
| --- | --- |
| [Revenue Management and Commission](https://docs.liteapi.travel/docs/revenue-management-and-commission) | Merchant-of-record alternatives, margin behaviour and weekly commission payout. |
| [Implementing a Payment Method](https://docs.liteapi.travel/docs/implementing-payment) | Payment options and the distinction between Nuitee-managed and organisation-managed payment. |
| [User Payment — Nuitee SDK](https://docs.liteapi.travel/docs/user-payment) | Nuitee payment SDK flow and payment-hold release for a non-finalised booking. |
| [Account Credit Card](https://docs.liteapi.travel/docs/account-credit-card) | Organisation-as-merchant-of-record flow and responsibility for its own payment layer. |
| [Account Wallet](https://docs.liteapi.travel/docs/account-wallet) | Pre-funded account payment model. |
| [Credit Line Payments](https://docs.liteapi.travel/docs/credit-line) | Contract-dependent credit-line payment model. |
| [Canceling a Booking](https://docs.liteapi.travel/docs/canceling-a-booking) | Refundability and cancellation-policy behaviour. |
| [Cancel a Booking API](https://docs.liteapi.travel/reference/put_bookings-bookingid) | API cancellation outcome: full refund, partial refund or charges depending on policy. |
| [LiteAPI FAQ](https://docs.liteapi.travel/docs/faq) | Account funding, invoice charging and refund-to-card statements. |
| [Regulatory Compliance](https://docs.liteapi.travel/docs/regulatory-compliance) | PCI DSS boundary and customer responsibility based on integration model. |
| [API Pricing and Usage Costs](https://docs.liteapi.travel/reference/api-pricing-usage-costs) | Public pricing applies unless superseded by a commercial agreement. |
| [LiteAPI / Nuitee Connect Terms of Service](https://liteapi.travel/terms/) | Service owner, baseline licence, liability, governing law and jurisdiction. |

## Limitations

This review is based on publicly accessible LiteAPI/Nuitee Connect material available on 28 July 2026.

It is not legal, tax or accounting advice. Public documentation may change and may not represent negotiated account-specific terms. Statements that a topic was not identified mean that it was not found in the reviewed public pages; they do not prove that LiteAPI has no private policy, support article, dashboard configuration or contractual term addressing the topic.
