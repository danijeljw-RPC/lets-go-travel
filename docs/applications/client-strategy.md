<!-- markdownlint-disable MD013 -->

# Client Strategy

## Shared Rules

Clients present platform data, manage local UI state, securely hold tokens, submit idempotency keys, follow retry guidance and cache only approved trip data. They do not calculate authoritative totals, decide discount eligibility, determine final booking state or reconcile suppliers.

## Web First

The web experience is a .NET 10 Blazor Web App using server-side rendering and interactive server components only where the customer journey needs them. It establishes onboarding, locale selection, hotel/flight/combined search, results, detail, prebook review, payment, confirmation, trips, booking detail, cancellation state, account/traveller management and ticket-support access. ASP.NET Core Web API remains the supplier-neutral application boundary.

Necessary LiteAPI hosted payment JavaScript is isolated behind a narrow browser-interoperability boundary. Browser code never receives supplier API credentials and does not own authoritative pricing, payment, booking or reconciliation state.

## Mobile Later

Native or cross-platform mobile clients need secure token storage, deep links, payment return handling, push notifications, approved offline data and minimum supported app/API versions. The payment path remains unresolved in [OI-0006](../issues/open/OI-0006-mobile-payment-and-pci-scope.md).

Mobile payment may use a system-browser redirect, hosted checkout or an approved provider component. An embedded web view is permitted only when explicitly supported and reviewed. Every approach handles return-to-app, abandonment, duplicate callbacks and payment success without booking confirmation.

An approved offline trip pack may contain booking references, itinerary summaries in local time, airport/property addresses, cancellation contacts, vouchers, selected documents and emergency support details. Sensitive documents require a deliberate offline-storage policy.

## Notifications

In-app, email, push and any later critical SMS notifications support preferences, urgency, deduplication, retry, language, timezone, quiet hours where appropriate, lock-screen privacy, acknowledgement and a link to the affected booking.

## Ticket Support

The web client provides first-party asynchronous ticket creation and thread access for authenticated customers and guests. The form collects name, email, optional booking/customer reference, required category, message and permitted attachments. An authenticated customer's account email is prepopulated and read-only.

Guest ticket links use a UUIDv7 route identifier plus a separate high-entropy secret token; the UUIDv7 alone never authorises access. Attachments are not public URLs. The client obtains authorised, short-lived download access after the platform validates the account owner or magic-link token. Every accepted update is persisted before an email is queued, and the UI shows notification or attachment failure without losing the thread message.

## Time, Locale and Currency

Preserve local date/time and timezone identifiers for travel schedules, plus UTC instants where determinable. Retain supplier original, transaction and charged currency separately from display conversion.

The initial locale/currency decision is closed in [OI-0007](../issues/closed/OI-0007-launch-market-locale-and-currency.md). Anonymous visitors store a selected locale in a secure same-site cookie. Authenticated customers store their preferred BCP 47 locale in PostgreSQL, and the account preference becomes authoritative on sign-in. UI text uses version-controlled .NET localisation resources with `en-AU` fallback; the database does not act as the static translation catalogue. Language selection never overwrites supplier, transaction, charged, settlement or refund currency.

## Customer Trust

Clearly distinguish estimated/current price, refundable/non-refundable, pending/confirmed, cancellation requested/cancelled, refund initiated/refunded, booking data/live flight status and supplier-managed/manual trip items.

Also distinguish pay-now/pay-later, platform fees/supplier charges and display currency/the amount actually charged.

## Accessibility

Accessibility starts with the first web experience. Date pickers, traveller selectors, filters, price breakdowns, cancellation terms, payment errors and status changes require keyboard operation, screen-reader labels, colour-independent warnings and readable travel documents.
