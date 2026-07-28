# Payments, Pricing, Discounts, and PCI DSS

## Preferred initial payment direction

The preferred initial approach is for raw cardholder data to be collected by a supplier-controlled or payment-provider-controlled component rather than by the platform Web API.

Official LiteAPI/Nuitee Connect documentation describes a user-payment flow using its payment SDK and Stripe Elements.

The intended separation is:

- the platform backend initiates the relevant prebook or payment session;
- the client presents the approved secure payment component;
- card details are sent to the payment provider through that component;
- the platform receives non-card payment references and status;
- the platform backend completes or confirms the booking through LiteAPI;
- the platform does not store card number or card verification value.

## PCI DSS caution

Using a hosted field, embedded payment element, or supplier SDK can reduce PCI DSS scope, but it does not automatically establish the final Self-Assessment Questionnaire category or compliance result.

The final scope depends on:

- whether the platform hosts the surrounding payment page;
- whether scripts on the page can affect payment security;
- whether payment is embedded or fully redirected;
- mobile integration method;
- server-side payment calls;
- webhooks;
- operational procedures;
- vulnerability management;
- third-party service-provider responsibilities;
- the selected merchant-of-record arrangement.

A qualified PCI specialist or acquiring/payment partner should confirm the final scope before production.

## Merchant of record

The planning phase must determine who is merchant of record for each product and payment method.

This affects:

- card statement descriptor;
- chargebacks;
- refunds;
- consumer-law obligations;
- tax;
- fraud responsibility;
- settlement;
- customer support;
- payment disputes;
- PCI responsibilities;
- booking failure after payment;
- insolvency and trust obligations where applicable.

Do not infer merchant-of-record status only from the technical payment flow.

## Payment and booking are separate states

A successful payment interaction does not necessarily mean a confirmed booking.

The system must model states such as:

- payment session created;
- customer action required;
- payment authorised;
- payment captured;
- booking pending;
- booking confirmed;
- booking failed after payment;
- refund pending;
- refund completed;
- payment disputed.

Recovery behaviour is required for every mismatch between payment and booking state.

## Pricing composition

The platform should retain a structured pricing breakdown.

Potential components include:

- supplier net amount;
- supplier taxes;
- supplier fees;
- platform markup;
- platform service fee;
- supplier-funded promotion;
- platform-funded discount;
- partner-funded discount;
- loyalty credit;
- payment surcharge where lawful and permitted;
- final customer total;
- currency;
- rounding adjustment.

The customer-facing presentation must be clear and comply with applicable Australian consumer and pricing law.

## Markup

LiteAPI documentation describes commissionable or net-rate models with margin control, but the production rules must be confirmed contractually.

The platform must verify:

- whether the rate is net or commissionable;
- whether markup is allowed;
- maximum or minimum constraints;
- parity or advertising restrictions;
- tax treatment;
- settlement timing;
- chargeback allocation;
- refund treatment;
- currency conversion treatment.

Markup rules belong in the backend pricing domain, not in the client.

## Discounts

Every discount should have an explicit funding source.

Examples:

- supplier-funded;
- platform-funded;
- partner-funded;
- shared;
- loyalty-funded;
- promotional budget.

For each discount, retain:

- rule or campaign ID;
- funding party;
- gross amount;
- currency;
- eligibility reason;
- application order;
- stacking behaviour;
- expiry;
- approval or override context;
- effect on platform margin.

A platform-funded discount normally reduces platform revenue or margin. A supplier-funded discount normally changes the supplier-side rate or contribution. Shared funding should record each contribution separately.

## Margin protection

The pricing engine should support a configurable floor.

It should prevent a booking from falling below permitted margin or cost unless an authorised loss-leading promotion explicitly allows it.

It should also prevent unintended stacking of:

- coupon;
- loyalty credit;
- campaign discount;
- referral credit;
- manual support adjustment.

## Price snapshots

The platform should retain the customer-approved price and terms at important points:

- search result presentation where needed;
- prebook;
- payment initiation;
- booking confirmation;
- cancellation quotation;
- refund confirmation.

Each snapshot should include currency, components, timestamps, and source references.

## Currency

Always retain the supplier's original currency and amounts.

Displayed conversion should not overwrite:

- supplier amount;
- charged amount;
- settlement amount;
- refund amount.

The system may need separate amounts for:

- supplier transaction;
- customer display;
- customer charge;
- platform revenue;
- settlement;
- accounting.

## Refunds and cancellations

The system must distinguish:

- cancellation eligibility;
- cancellation request;
- supplier acceptance;
- calculated penalty;
- refund initiation;
- refund completion;
- payment-provider outcome;
- platform support action.

Never show a refund as complete merely because a cancellation request was accepted.

## Payment data retention

The platform should store only the payment data required for operations, such as:

- payment provider;
- payment or transaction reference;
- status;
- amount;
- currency;
- timestamps;
- masked card descriptor if provided and permitted;
- refund references;
- chargeback references.

It should not store:

- full card number;
- card verification value;
- sensitive authentication data;
- supplier secret keys in booking records or logs.
