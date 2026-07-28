---
adr_id: ADR-0007
title: Payment Data and PCI Scope Minimisation
status: accepted
date_proposed: 2026-07-27
date_accepted: 2026-07-28
date_rejected: null
date_superseded: null
superseded_by: null
supersedes: []
decision_owners:
  - Finance
  - Security
  - Compliance
related_issues:
  - OI-0002
  - OI-0006
related_adrs:
  - ADR-0002
  - ADR-0006
related_plans:
  - PLAN-0001
related_docs:
  - docs/product/payments-and-pricing.md
  - docs/security/payment-and-pci-scope.md
  - docs/integrations/duffel.md
---

# ADR-0007 — Payment Data and PCI Scope Minimisation

## Status

Accepted on 2026-07-28. Production activation of each payment route remains gated by commercial, legal, security and PCI evidence. Duffel is not enabled for the initial launch.

## Context

The platform must support suppliers whose customer-payment and supplier-settlement models differ without coupling checkout, booking or refund behaviour to one provider SDK. LiteAPI can collect customer payment as merchant of record or accept settlement through an account card, wallet or credit line. Duffel can pass an approved customer card to the travel supplier, deduct bookings from Duffel Balance or, where contractually available, collect customer payment through Duffel Payments. A platform-owned payment provider such as Stripe creates another route in which `readytogo.travel` collects the customer funds and settles the supplier separately.

Customer payment collection, supplier settlement and merchant-of-record responsibility are related but distinct. A JavaScript component does not establish who owns disputes, refunds, tax, consumer obligations or booking-failure remediation.

## Decision Drivers

- Keep raw card data out of the platform backend.
- Support secure web and later mobile checkout.
- Keep customer payment, supplier settlement and booking outcomes independent and reconcilable.
- Permit supplier/provider merchant-of-record and platform merchant-of-record routes.
- Avoid making LiteAPI, Duffel or Stripe objects the platform payment contract.
- Select the route per final offer because capability can vary by supplier, product, rate, account, market and payment method.
- Establish responsibilities through evidence rather than assumption.

## Options Considered

### Option A — Supplier/provider merchant-of-record only

Always use a supplier or supplier payment entity to collect the customer charge through an approved hosted or embedded component. This minimises platform financial operations but cannot provide a consistent route when a supplier lacks an approved direct-customer payment model.

### Option B — Platform merchant-of-record only

Always collect customer funds through the platform's payment provider, then settle LiteAPI, Duffel or another supplier through an account card, wallet, balance, credit line or other approved mechanism. This provides a consistent branded checkout but makes the platform responsible for payment operations, disputes, refunds, cash flow and separate supplier settlement.

### Option C — Capability-driven hybrid with supplier/provider preference

Support both merchant models behind platform-owned contracts. Prefer a supplier/provider merchant-of-record route for the initial product when it is commercially available and legally clear. Enable the platform-owned collection route only when required for bundling, one branded customer charge or a supplier without an acceptable direct-payment route.

### Option D — Platform-handled raw card data

Build custom card entry that sends full PAN, CVV or sensitive authentication data through the platform backend.

## Recommendation

Choose Option C. It preserves a universal platform model without pretending that all suppliers share a payment rail. Keep Option A as the preferred operational route where evidence supports it, allow Option B through an explicitly approved route, and reject Option D.

## Decision

Option C is accepted.

`IPaymentService` is the application-facing orchestrator. It coordinates two independent provider contracts:

- `ICustomerPaymentProvider` prepares customer checkout, reconciles payment state and performs customer refunds through LiteAPI/Nuitee, Stripe, Duffel Payments if contracted, or another approved collector.
- `ISupplierSettlementProvider` supplies the payment instruction needed to confirm or service a booking, such as a LiteAPI transaction, account card, wallet or credit line, or a Duffel customer-card reference or balance payment.

The supplier adapter produces a `PaymentPlan` for each final offer. The plan records merchant of record, customer-payment provider, supplier-settlement provider, required customer action, authorisation/capture capability, customer and supplier amounts/currencies, fees, margin, refund owner and route, and whether multiple charges may occur. Provider tokens and references remain opaque, provider-scoped values and cannot be reused across adapters.

The initial policy prefers supplier/provider-controlled customer payment when written terms establish the merchant, statement descriptor, commission or margin, refund, dispute and booking-failure responsibilities. A platform-owned Stripe route may collect the customer total when required, using an approved Stripe-hosted or embedded component while raw card data goes directly to Stripe. The platform then settles LiteAPI through an approved account card, wallet or credit line, or Duffel through an approved balance or other settlement method.

Duffel Payments is not assumed to be available: its Payment Intents documentation stated on 2026-07-28 that it was not accepting new customers. Duffel customer-card support also requires approval and charges the traveller through the travel supplier rather than creating a universal Duffel-managed charge.

## Initial Supplier Activation Policy

The platform architecture accommodates a Duffel adapter, but Duffel is disabled by default for the initial launch. Customer-facing search results must not include Duffel inventory while the corresponding booking route is disabled; the platform must not advertise offers that it cannot reliably sell. Sandbox, development and internal capability discovery may use the adapter without enabling it for customers.

Duffel may be enabled for customer search and booking only after one of these settlement routes is operationally approved:

- Duffel approves production use of customer-card payment for the required products, suppliers and markets; or
- the business approves and funds a Duffel Balance operating model with sufficient working capital, top-up lead-time controls and refund/reconciliation procedures.

Using the platform's Stripe account to collect customer funds does not remove the second route's working-capital requirement. Stripe settlement and Duffel Balance funding are separate financial legs, so the required supplier funds may need to be available before Stripe pays out the customer charge. Duffel Payments must not be used as an enablement assumption unless live access is confirmed in writing.

Customer payment, supplier settlement, booking and refund state machines remain separate. When the platform is merchant of record, a supplier refund and customer refund are separate financial legs that must be reconciled. Authorise-then-book-then-capture may be used only when the customer payment method supports delayed capture; otherwise the payment plan defines capture-first compensation and refund behaviour.

No platform endpoint, worker, log or database record may receive or persist full PAN, CVV or sensitive authentication data. Checkout must use approved hosted pages, hosted fields, Elements or provider SDK components that transmit card data directly to the responsible PCI-compliant provider. This decision does not claim a particular SAQ or compliance status.

## Evidence Reviewed

The following official documentation was reviewed on 2026-07-28. Documentation establishes available integration shapes, not account enablement or contractual responsibility.

- [LiteAPI payment methods](https://docs.liteapi.travel/docs/implementing-payment)
- [LiteAPI user payment](https://docs.liteapi.travel/docs/user-payment)
- [LiteAPI revenue management and merchant-of-record models](https://docs.liteapi.travel/docs/revenue-management-and-commission)
- [LiteAPI account credit card](https://docs.liteapi.travel/docs/account-credit-card), [wallet](https://docs.liteapi.travel/docs/account-wallet) and [credit line](https://docs.liteapi.travel/docs/credit-line)
- [Duffel customer-card payment](https://duffel.com/docs/guides/paying-with-customer-cards) and [Card API](https://duffel.com/docs/api/v2/card/create-card)
- [Duffel Balance and customer collection](https://duffel.com/guides/collecting-payments-from-your-customers)
- [Duffel Payment Intents](https://duffel.com/docs/api/payment-intents)
- [Stripe Checkout Sessions](https://docs.stripe.com/payments/checkout-sessions) and [manual capture](https://docs.stripe.com/payments/place-a-hold-on-a-payment-method)

## Consequences

### Positive

- Minimises sensitive payment-data exposure.
- Avoids coupling booking logic to one payment component or merchant model.
- Allows a low-overhead supplier/provider merchant route and a consistent platform-owned fallback.
- Makes customer collection, supplier settlement, booking and refunds independently auditable.
- Supports LiteAPI, Duffel and later providers through capability-specific adapters.
- Allows Duffel to be activated later without changing the public API or core payment contracts.

### Negative

- Checkout UI and statement descriptors can differ between payment plans.
- Supplier/provider merchant routes may produce separate charges or require a separate platform service fee.
- The platform-owned route creates cash-flow, dispute, refund and settlement obligations.
- The abstraction requires more state and reconciliation than a single provider-specific checkout.
- Duffel inventory is unavailable to launch customers until its settlement gate is satisfied.

### Risks

The hybrid model can hide material provider differences if adapters collapse unsupported capabilities into a false common denominator. A customer charge can succeed while a booking fails, or a supplier booking can succeed while customer capture fails. Supplier refunds may return to a balance or account before the platform refunds the customer. These risks require explicit payment plans, idempotency, durable state, reconciliation, compensation workflows and operational ownership.

## Dependencies

[OI-0002](../../issues/open/OI-0002-liteapi-commercial-and-merchant-of-record.md) must establish the written commercial and merchant responsibilities for every enabled route. [OI-0006](../../issues/open/OI-0006-mobile-payment-and-pci-scope.md) must validate the actual web/mobile components and qualified PCI scope before production activation. Provider account enablement, supported payment methods, refund operations, settlement funding and legal/compliance approval remain go-live gates. The specific Duffel activation evidence is recorded in the [Duffel integration profile](../../integrations/duffel.md).

## Related Documents

- [Payments and Pricing](../../product/payments-and-pricing.md)
- [Payment and PCI Scope](../../security/payment-and-pci-scope.md)
- [Duffel Integration](../../integrations/duffel.md)
