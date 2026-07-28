# Booking Lifecycle

## Status

Proposed domain direction. Exact transitions require supplier sandbox evidence and commercial rules.

## Platform States

| State | Meaning |
| --- | --- |
| Draft | Customer intent exists without a selected supplier offer. |
| OfferSelected | A provisional offer has been chosen. |
| PrebookPending | Price and availability confirmation is in progress. |
| PriceChanged | Supplier returned different price or terms requiring customer acceptance. |
| PaymentPending | Payment needs customer or provider action. |
| BookingPending | Supplier outcome is not final. |
| Confirmed | Supplier fulfilment is confirmed and required references are retained. |
| ChangePending | A servicing or supplier change is unresolved. |
| CancellationPending | Cancellation was requested but is not final. |
| Cancelled | Supplier cancellation is final. |
| RefundPending | Refund is expected but not completed. |
| Refunded | Refund completion is evidenced. |
| Failed | The operation failed with a known final outcome. |
| RequiresSupport | Automated recovery cannot safely determine or complete the next action. |

## Mapping Rule

Supplier states map to platform states without discarding the original supplier value. Unknown values fail safely and create operational visibility rather than being treated as confirmed or unchanged.

## Payment Mismatches

Payment and booking use separate state. Payment success with booking failure or uncertainty must enter a recoverable state with supplier lookup, retry rules, customer messaging and support escalation.

## Idempotency

Prebook where appropriate, payment-session creation, booking confirmation, cancellation, refund and notification event creation require defined idempotency ownership, fingerprint, lifetime, repeat response and conflict behaviour.
