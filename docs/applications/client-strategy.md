# Client Strategy

## Shared Rules

Clients present platform data, manage local UI state, securely hold tokens, submit idempotency keys, follow retry guidance and cache only approved trip data. They do not calculate authoritative totals, decide discount eligibility, determine final booking state or reconcile suppliers.

## Web First

The web experience establishes onboarding, search, results, detail, prebook review, payment, confirmation, trips, booking detail, cancellation state, account/traveller management and support access.

## Mobile Later

Native or cross-platform mobile clients need secure token storage, deep links, payment return handling, push notifications, approved offline data and minimum supported app/API versions. The payment path remains unresolved in [OI-0006](../issues/open/OI-0006-mobile-payment-and-pci-scope.md).

## Time, Locale and Currency

Preserve local date/time and timezone identifiers for travel schedules, plus UTC instants where determinable. Retain supplier original, transaction and charged currency separately from display conversion. The initial locale/currency decision is [OI-0007](../issues/open/OI-0007-launch-market-locale-and-currency.md).

## Customer Trust

Clearly distinguish estimated/current price, refundable/non-refundable, pending/confirmed, cancellation requested/cancelled, refund initiated/refunded, booking data/live flight status and supplier-managed/manual trip items.
