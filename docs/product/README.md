<!-- markdownlint-disable MD013 -->

# Product Documentation

`readytogo.travel` is a standalone consumer product intended to help a customer organise and manage a trip through one account and one consistent experience. It is not a thin supplier frontend: supplier inventory is private fulfilment capability behind the product API.

## Documents

- [Vision and Scope](vision-and-scope.md)
- [Consumer MVP Scope](consumer-mvp-scope.md)
- [Capability Roadmap](capability-roadmap.md)
- [Payments and Pricing](payments-and-pricing.md)

## Product Principle

The organising concept is a trip. A trip may exist before a booking and may eventually combine supplier-managed bookings, manually added travel items, documents, notes, reminders and notifications.

## Current Decision State

Hotel, flight and combined hotel-plus-flight journeys are the accepted MVP product mix under closed [OI-0001](../issues/closed/OI-0001-mvp-product-scope.md). Australia is the operating focus with global access, `en-AU`/AUD defaults and selectable locale under closed [OI-0007](../issues/closed/OI-0007-launch-market-locale-and-currency.md). LiteAPI commercial, carrier, servicing, webhook and payment evidence remains in review under OI-0002 through OI-0006; retention uses an approved baseline while OI-0011 awaits contractual/legal validation.
