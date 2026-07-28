<!-- markdownlint-disable MD013 -->

# Supplier Integration Principles

## Capability Model

Provider capability is explicit by supplier, account, environment, product and market. The platform must not pretend all suppliers support the same searches, ancillaries, changes, cancellations, refunds, payments or events.

Capabilities include accommodation content/availability/booking, flight shopping/booking, retrieval, cancellation, payment-session creation, webhook verification and document retrieval. Product details such as refundable rates, multi-room/child rules, seats, baggage, frequent-flyer numbers, exchanges, schedule changes, currencies, pay-now/pay-later and hosted checkout remain independently discoverable rather than implied by a provider name.

## Mapping

Adapters map supplier responses into platform-owned models while retaining original identifiers, statuses, currency, terms, timestamps and permitted evidence. Unknown fields do not silently corrupt normalised state.

Search results are provisional. Offers retain expiry, revalidation requirements, currency, occupancy, room/fare conditions and the terms needed to detect price, availability, tax/fee or cancellation-policy changes. Prebook is a distinct confirmation stage before final payment and booking.

## Credentials

API keys and confidential credentials stay in protected server configuration, differ by environment, support rotation, never appear in clients or logs, and are not stored in booking records.

## Failure Handling

Every external call has an operation-specific timeout. High-impact timeouts require state recovery before retry because a timeout does not prove the supplier did nothing. Supplier errors map to a stable platform error model.

## Webhooks

Webhook processing assumes duplicates, delay, reordering, missing delivery, authentication failure and schema change. Persist an inbox record before processing where practical, deduplicate by event identity, and reconcile rather than blindly overwrite booking state.

## Provider Activation

Provider integration and customer-facing activation are separate. Each adapter is enabled by environment and capability only after its production access, commercial terms, booking route, payment and settlement route, servicing, refund, reconciliation and support obligations are proven.

Customer search must not expose an offer whose required booking or settlement route is disabled. Sandbox or internal discovery can remain available while a provider is disabled for customers. Duffel follows this policy and is disabled by default for the initial launch.

## Exit Strategy

Adding or replacing a supplier must not replace customer accounts, trips, travellers, platform IDs, notification preferences or support history. Supplier-specific metadata remains isolated rather than being hidden through an artificial universal model.
