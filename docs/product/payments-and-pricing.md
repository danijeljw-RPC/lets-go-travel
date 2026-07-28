<!-- markdownlint-disable MD013 -->

# Payments and Pricing

## Status

Accepted architecture under [ADR-0007](../adr/accepted/ADR-0007-payment-data-and-pci-scope-minimisation.md). Production routes remain gated by [OI-0002](../issues/open/OI-0002-liteapi-commercial-and-merchant-of-record.md) and [OI-0006](../issues/open/OI-0006-mobile-payment-and-pci-scope.md).

## Payment Boundary

Use a capability-driven payment plan for each final offer. Prefer supplier/provider-controlled customer payment where the commercial and merchant responsibilities are clear. Permit platform-owned collection through an approved provider such as Stripe when bundling, one branded charge or supplier capability requires it. In every route, raw card number, CVV and sensitive authentication data bypass the platform backend.

## Provider Contracts

`IPaymentService` orchestrates customer collection and supplier settlement without making provider objects the platform contract. `ICustomerPaymentProvider` owns checkout and customer refund actions. `ISupplierSettlementProvider` supplies the transaction, account card, wallet, credit line, customer-card reference, balance or other instruction needed for the booking supplier.

## Initial Provider Activation

Duffel will be integrated as a later provider but disabled by default for the initial launch. Its offers are not exposed in customer search until the related production booking and settlement route is enabled. Activation requires either approved Duffel customer-card payment or an approved Duffel Balance operating model with sufficient working capital and tested refund and reconciliation controls. Collecting customer funds through Stripe does not pre-fund Duffel Balance or remove settlement timing risk.

## Separate State Machines

Payment success is not booking confirmation, and supplier settlement is not customer collection. The design represents customer action required, authorisation, capture, booking pending, booking confirmed, booking failed after payment, supplier refund pending, supplier refund received, customer refund pending, customer refunded and disputed states without conflating them.

## Pricing Record

The platform should retain supplier amount, taxes, supplier fees, platform markup or fee, each discount and funding source, final customer total, transaction currency, displayed currency if different, rounding and the customer-approved terms at prebook and confirmation.

Each discount records its rule or campaign, funding party, gross amount, currency, eligibility reason, application order, stacking behaviour, expiry, approval/override context and effect on platform margin. Shared funding records each contribution separately.

Price snapshots preserve the customer-approved components, currency, terms, time and source references at prebook, payment initiation, booking confirmation, cancellation quotation and refund confirmation; search presentation is also snapshotted where evidence requirements justify it.

## Commercial Questions

Merchant of record, chargebacks, refund responsibility, tax, settlement, commission, markup limits, parity restrictions and discount funding are commercial decisions. They cannot be inferred from the presence of a payment SDK.

## Guardrails

- Authoritative pricing and discount calculation stays server-side.
- Discounts record funding source and margin impact.
- A configurable margin floor prevents unintended negative margin and unintended stacking of coupons, loyalty credits, campaigns, referrals and manual adjustments unless an authorised loss-leading promotion explicitly permits it.
- Cancellation acceptance and refund completion remain distinct.
- Provider references remain opaque and scoped to the provider that created them.
- A supplier refund and customer refund are reconciled as separate legs when the platform is merchant of record.
- Supplier original, customer display, customer charge, platform revenue, settlement and refund amounts remain distinguishable. Display conversion never overwrites transaction, settlement or refund amounts.
