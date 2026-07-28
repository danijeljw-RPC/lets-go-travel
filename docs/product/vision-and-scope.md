<!-- markdownlint-disable MD013 -->

# Product Vision and Scope

## Vision

Give consumers one trusted place to search, book and manage the important parts of a trip, while keeping supplier complexity behind a stable product experience.

## Product Promise

Everything about my trip lives here.

## Product Value

Inventory enables a transaction, but the durable product value is the customer relationship, trip organisation, traveller profiles, booking history, notifications, documents, support context and consistent web/mobile experience.

The primary customer-facing object is a trip rather than a supplier booking type. A trip can organise accommodation, flights, ground transport, activities, restaurant plans, notes, documents, reminders, notifications, expenses and travellers without forcing those concepts into supplier-specific silos.

## Target Customer

The initial target is an individual consumer organising travel for themselves and possibly other travellers such as family or friends. A customer owns the account, trips and bookings. A traveller is a person participating in travel and may not be the customer.

The experience should optimise for simple onboarding, fast destination/date search, transparent pricing, trustworthy checkout, clear cancellation conditions, accessible support, useful pre-trip reminders, resilient in-trip access, privacy controls, self-service account management and a mobile-ready API.

## Initial Capability Direction

- Account creation, sign-in and recovery.
- Customer profile and preferences.
- Trip creation and management.
- Saved travellers with sensitive reusable fields off by default and protected by granular opt-in.
- Hotel, flight and combined hotel-plus-flight search and offer selection through approved supplier capabilities.
- Price and availability confirmation before payment.
- Secure supplier/payment-provider-controlled card entry.
- Booking confirmation and current booking details.
- Booking history and reconciliation.
- Cancellation and refund status where supported.
- Email and in-app notifications, including versioned flight-itinerary changes.
- First-party asynchronous ticket support for customers and guests.
- Responsive .NET 10 Blazor SSR experience using the platform API.

## Explicit Non-goals

- Direct client access to supplier APIs or credentials.
- Supplier response models as the public product contract.
- Business rules or authoritative pricing in web/mobile clients.
- A promise that one supplier supports all travel products or servicing needs.
- A promise of live operational flight status without a dedicated verified provider.
- Storing raw cardholder data in the platform backend.

## Success Measures for Planning

Planning can proceed when the product can state its first sellable scope, verified supplier capabilities, payment and merchant-of-record responsibilities, customer/traveller data policy, booking lifecycle, reconciliation behaviour, support path and launch market.

The resulting architecture must also allow suppliers to be added or replaced without redesigning clients, prevent duplicate financial and reservation operations, explain price composition and discount funding, support auditable customer-support workflows, scale search separately from durable booking work and evolve without forcing immediate mobile-client upgrades.
