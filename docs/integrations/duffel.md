<!-- markdownlint-disable MD013 -->

# Duffel Integration

## Status

Planned supplier adapter, disabled by default for the initial launch. Duffel inventory is not exposed in customer search and cannot be booked in production until the activation gates below are satisfied.

## Integration Boundary

Duffel will be implemented behind the same platform-owned supplier, payment and settlement contracts as other providers. Its credentials and provider objects remain server-side. A runtime provider configuration controls activation so enabling Duffel later does not require a new public API or a redesign of trips, travellers, bookings or payments.

Development, sandbox and internal discovery may exercise Duffel independently of customer-facing activation. Search capability alone is not sufficient to enable the provider: any offer shown to a customer must have an approved and operational booking, payment, refund and support route.

## Initial Decision

Duffel is not enabled out of the box. The initial product does not assume that it has the working capital required to pre-fund Duffel Balance while waiting for customer payments collected through Stripe to settle.

Duffel's customer-card route could remove that pre-funding requirement, but production use requires Duffel approval and must be proven for the relevant product, supplier and market. Duffel Payments is not an initial dependency because its Payment Intents documentation stated on 2026-07-28 that it was not accepting new customers.

## Activation Gates

Duffel customer search and booking may be enabled together only when all applicable gates are satisfied:

- Production account access and commercial terms are approved.
- A supported settlement route is confirmed for the offers that will be exposed.
- Either Duffel customer-card payment is approved for production or a funded Duffel Balance operating model is approved.
- Any Duffel Balance model has an agreed working-capital buffer, top-up thresholds, lead-time monitoring and low-balance controls.
- Customer collection, supplier settlement, booking failure, cancellation, refund, dispute and reconciliation procedures are tested end to end.
- Merchant-of-record, statement descriptor, markup, fees, tax and customer-support responsibilities are documented.
- Monitoring can prevent customer-visible offers when their required payment or settlement route is unavailable.

## Official References Reviewed

- [Paying with customer cards](https://duffel.com/docs/guides/paying-with-customer-cards)
- [Collecting and making payments](https://duffel.com/guides/collecting-payments-from-your-customers)
- [Payment Intents](https://duffel.com/docs/api/payment-intents)

## Related Documents

- [ADR-0007](../adr/accepted/ADR-0007-payment-data-and-pci-scope-minimisation.md)
- [Payments and Pricing](../product/payments-and-pricing.md)
- [Supplier Integration Principles](supplier-integration-principles.md)
