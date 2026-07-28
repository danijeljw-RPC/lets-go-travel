<!-- markdownlint-disable MD013 -->

# Payment and PCI Scope

## Position

ADR-0007 requires payment-provider- or supplier-controlled card entry and prohibits raw card data from entering the platform backend. Approved hosted pages, hosted fields, Elements or provider SDK components transmit card data directly to the responsible payment or travel provider. Do not claim a particular SAQ or compliance state until the final web/mobile integration, merchant arrangement, scripts, callbacks, webhooks and operational controls are reviewed.

The initial selected direction uses LiteAPI's officially supported hosted JavaScript, hosted payment experience or platform SDK for each supported client. A hosted system-browser route is the fallback when an approved embedded component is unavailable. Custom card forms and unsupported embedded web views remain prohibited.

## Prohibited Storage

The platform must not store full PAN, CVV, sensitive authentication data or supplier/payment secrets in booking records or logs.

## Permitted Operational References

Subject to the selected agreement, store provider name, merchant of record, customer-payment reference, supplier-settlement reference, state, amount, currency, timestamps, masked descriptor, refund reference and dispute reference. Every token remains opaque and scoped to the provider that created it.

## Decision Dependency

[OI-0002](../issues/open/OI-0002-liteapi-commercial-and-merchant-of-record.md) and [OI-0006](../issues/open/OI-0006-mobile-payment-and-pci-scope.md) must be resolved before activating a production payment route or claiming a PCI compliance outcome.
