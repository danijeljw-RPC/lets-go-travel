---
document_type: financial-legal-baseline
title: Client Money, Insolvency and Trust Controls
status: implementation-baseline
reviewed: 2026-07-29
owners:
  - Finance
  - Legal/Compliance
  - Product
---

<!-- markdownlint-disable MD013 MD025 -->

# Client Money, Insolvency and Trust Controls

## Australian travel-industry context

The former travel-agent licensing and Travel Compensation Fund framework ended in 2014. A general statutory travel-agent trust-account or compensation protection must not be assumed.

The reviewed official source confirms that a travel-agent licence is no longer required in Victoria. The exact current position for the operating model must still be confirmed by Australian counsel across applicable jurisdictions.

The product must not imply that customer travel money is government guaranteed or protected by a travel compensation fund.

## Preferred funds flow

The preferred launch model is:

1. customer payment details are entered into the approved LiteAPI/provider payment SDK;
2. the approved merchant of record/payment provider receives the customer payment;
3. readytogo.travel receives only payment and booking references;
4. supplier settlement, refund funding, disputes and chargebacks follow the executed commercial terms; and
5. readytogo.travel receives its approved commission or margin through the agreed settlement process.

This reduces, but does not eliminate, readytogo.travel's customer, insolvency, reconciliation and disclosure obligations.

## Required funds-flow evidence

For every payment route, maintain a diagram that identifies:

- customer;
- readytogo.travel operating entity;
- LiteAPI/Nuitée contracting entity;
- payment processor/acquirer;
- airline/hotel/provider;
- any GDS/NDC/LCC/consolidator;
- bank accounts;
- currencies;
- authorisation and capture;
- legal recipient of funds;
- merchant of record;
- supplier payable;
- platform commission;
- refund source;
- chargeback debtor;
- reserve/offset rights;
- settlement timing; and
- insolvency consequence at each stage.

No production activation is permitted from a high-level API diagram alone.

## If readytogo.travel receives customer funds

Where customer money enters an account controlled by readytogo.travel:

- obtain counsel advice on legal title and whether a trust, agency or debtor-creditor relationship arises;
- use a dedicated segregated client/travel-money account as an operational control unless counsel approves another model;
- do not commingle customer travel money with payroll or general working capital;
- reconcile customer liability, supplier payable and bank balance daily;
- restrict withdrawals to approved supplier settlement, customer refund, earned fee or documented correction;
- dual-authorise material transfers;
- monitor aged unmatched amounts;
- preserve booking-level traceability;
- maintain refund liquidity;
- record chargeback and reserve exposure;
- document bank set-off risk;
- test insolvency treatment; and
- disclose the actual protection accurately.

Operational segregation does not automatically create a legal trust.

## Use of the word trust

Do not call an account a `trust account`, `client trust account` or `protected account` unless:

- a lawyer confirms the trust exists;
- the trust terms and beneficiaries are defined;
- the bank account is correctly titled;
- accounting and withdrawal controls match the trust;
- insolvency treatment has been reviewed;
- customer terms accurately describe it; and
- ongoing compliance is audited.

Use `segregated customer funds account` only where technically true and legally approved.

## Insolvency controls

ASIC states that a company is insolvent when it cannot pay its debts when due, and directors have a duty to prevent insolvent trading.

Finance must maintain:

- rolling cash-flow forecast;
- supplier payable forecast;
- customer refund liability;
- chargeback exposure;
- payment-provider reserve exposure;
- commission clawback exposure;
- unearned fee liability;
- booking failure liability;
- solvency indicators;
- board escalation thresholds; and
- cessation/controlled wind-down procedure.

Customer travel funds and supplier payables must not be treated as unrestricted operating cash merely because settlement occurs later.

## Accounting classification

Finance/accounting advice must determine:

- gross versus net revenue presentation;
- whether customer receipts are revenue, liability or agent funds;
- when platform fees are earned;
- commission recognition;
- supplier payable;
- refunds;
- credits;
- breakage;
- foreign exchange;
- GST;
- chargebacks;
- reserves;
- disputed funds; and
- unclaimed money.

The product data model must preserve the evidence required for the approved accounting treatment.

## Customer disclosures

Before payment, disclose:

- merchant of record;
- who receives funds;
- statement descriptor;
- supplier and platform fees;
- settlement/booking dependency;
- what occurs if payment succeeds but booking fails;
- how refunds are funded and processed;
- chargeback/dispute route;
- whether funds are held by readytogo.travel;
- whether any insolvency protection exists; and
- that accreditation is not a government guarantee where applicable.

Do not use reassuring but unsupported phrases such as:

- `your money is completely safe`;
- `funds are protected`;
- `held in trust`;
- `government backed`;
- `guaranteed refund`; or
- `zero insolvency risk`.

## Protective options for counsel/board decision

The board may consider:

- LiteAPI/provider merchant-of-record model;
- customer payment direct to supplier;
- segregated account;
- legally constituted trust;
- bank guarantee;
- insolvency/default insurance;
- professional indemnity;
- cyber insurance;
- chargeback/refund reserve;
- ATAS accreditation;
- supplier failure insurance;
- staged supplier settlement;
- limits on forward bookings; and
- customer card payment preference over bank transfer.

The pack does not assert that any insurance product covers travel intermediary insolvency; coverage must be verified in the actual policy.

## Failure and wind-down plan

The plan must cover:

- stopping new sales;
- identifying paid/unconfirmed bookings;
- preserving supplier bookings;
- reconciling every customer balance;
- communicating with customers;
- refund and chargeback coordination;
- supplier notification;
- access to booking records;
- operational support;
- director/legal escalation;
- liquidator access;
- data/privacy obligations; and
- status page and complaints.

## Acceptance criteria

- [ ] Funds-flow diagram approved.
- [ ] MOR and legal recipient of funds approved.
- [ ] Customer money does not enter readytogo.travel accounts, or approved controls exist.
- [ ] Trust language is prohibited unless legally established.
- [ ] Daily reconciliation implemented.
- [ ] Refund/chargeback liquidity model approved.
- [ ] Solvency monitoring and board triggers documented.
- [ ] Revenue/liability accounting treatment approved.
- [ ] Customer insolvency disclosures approved.
- [ ] Wind-down plan tested.
