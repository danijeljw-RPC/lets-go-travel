# Decisions, Assumptions, and Open Questions

## Current direction to formalise later

These are strong current directions, not yet repository-formal decisions:

- Use .NET 10.
- Use ASP.NET Core Web API as the public application boundary.
- Use PostgreSQL as the application database.
- Use Keycloak for customer authentication and token issuance.
- Let web, Android, and iOS clients call the platform API.
- Do not let clients call LiteAPI directly.
- Keep LiteAPI credentials server-side.
- Own customer profiles, trips, travellers, preferences, and platform booking records.
- Treat LiteAPI as a private supplier.
- Use platform-owned identifiers.
- Keep current booking state normalised.
- retain immutable canonical booking versions;
- use hashes to detect meaningful version changes;
- store structured metadata, flags, and `DiffJson`;
- use background processing for reconciliation and notifications;
- implement layered rate limiting;
- use idempotency for financial and reservation effects;
- avoid raw cardholder data touching the platform backend.

## Working assumptions requiring validation

- LiteAPI's user-payment flow will be suitable for the web product.
- A suitable LiteAPI-supported mobile payment path will exist.
- The commercial arrangement will permit the intended markup or commission model.
- LiteAPI flight access will be available to the platform.
- LiteAPI's booking retrieval will provide enough detail for useful reconciliation.
- Supplier payload retention will be permitted.
- The first version can be implemented as a modular monolith.
- Search traffic can be controlled within supplier look-to-book obligations.
- PostgreSQL JSONB will be used for snapshots and differences.
- Keycloak will support the selected consumer sign-in methods and mobile flow.

## Critical LiteAPI questions

### Commercial

- What production agreement applies?
- Is the platform paid through net rates, commissionable rates, cashback, or another model?
- Can the platform set markup?
- Are there markup limits or parity restrictions?
- Who is merchant of record?
- Who owns chargeback responsibility?
- How and when are commissions or margin settled?
- How are platform-funded and supplier-funded discounts represented?
- What refund and cancellation fees apply?
- What look-to-book or request limits apply?
- What data may be retained, and for how long?

### Hotels

- Which markets and properties are available?
- What rate types are provided?
- Are taxes and fees complete and display-ready?
- What multi-room and child-occupancy rules apply?
- Which booking changes can be serviced through the API?
- How are pay-at-property bookings represented?
- Are property images and content licensed for caching and display?
- Are hotel relocations or supplier changes reported?

### Flights

- Is flight access enabled by default or separately approved?
- Is Qantas domestic inventory available?
- Is Qantas international inventory available?
- Are Jetstar and Virgin Australia available?
- What content comes from GDS, NDC, or other sources?
- Are branded fares supported?
- Are seats, baggage, meals, and other ancillaries supported?
- Can frequent-flyer numbers be submitted?
- Are tickets and ticket numbers returned?
- Can voluntary changes be quoted and completed?
- Can involuntary schedule changes be retrieved?
- Is there a schedule-change webhook?
- Does booking retrieval return the latest changed itinerary?
- Can changes be accepted or rejected?
- Are cancellations and refunds automated?
- What happens when carrier servicing must be handled manually?
- Is live gate, terminal, delay, aircraft, diversion, or cancellation data included?

### Payments

- Which payment integration is recommended for a .NET web frontend?
- Which payment integration is supported for Android and iOS?
- Is full redirect available?
- Is embedded payment supported?
- Who owns the Stripe relationship?
- What payment methods are available in Australia?
- How are 3-D Secure and step-up authentication handled?
- What happens when payment succeeds but booking fails?
- How are refunds correlated?
- What webhook security is provided?
- What PCI documentation and Attestation of Compliance can LiteAPI provide?

### Webhooks and retrieval

- What events are available for hotels and flights?
- Are events signed?
- What are retry and ordering guarantees?
- Is there a unique event identifier?
- How long are events retained?
- Can events be replayed?
- Which booking fields can change after confirmation?
- Are retrieved bookings source-current or only the original confirmed record?

## Product questions

- Is the first release hotels only, or hotels and flights?
- Is trip planning included in the MVP?
- Are manual trip items included?
- Will customers save traveller profiles?
- Will passport details be stored?
- Is trip sharing included?
- Which notification channels launch first?
- Is offline access required for the first mobile release?
- Will support be email, chat, or in-app ticketing?
- Is a loyalty model part of the product?
- Which countries launch first?
- Which currencies and languages launch first?

## Architecture questions

- Blazor Web App, another web frontend, or separate frontend architecture?
- Modular monolith boundaries?
- Background job technology?
- Message broker from day one or transactional outbox plus database polling?
- Cache technology and timing?
- Object storage provider?
- API versioning convention?
- Identifier strategy?
- Canonical money model?
- Canonical date-time and timezone model?
- Booking state machine?
- Supplier capability model?
- Reconciliation frequency policy?
- Raw payload retention?
- Diff schema and canonicalisation versioning?
- Support tooling?
- Administrative authentication boundary?
- Deployment platform?
- Secret store?
- Monitoring platform?

## Security and legal questions

- Applicable Australian Privacy Act obligations?
- Consumer Data Right relevance, if any?
- PCI DSS scope and SAQ type?
- Merchant-of-record and acquiring obligations?
- Australian Consumer Law pricing presentation?
- Chargeback and refund policy?
- Travel-agent licensing or registration implications?
- Insolvency protection or trust-account implications?
- Cross-border data transfer?
- Data residency?
- Passport-data retention?
- Minor traveller and parental consent?
- Breach response?
- Terms of service and privacy policy?
- Supplier terms that must be passed through to customers?

## Decisions that should not be made silently

- storing passports by default;
- becoming merchant of record;
- promising live flight updates;
- claiming Qantas support;
- choosing polling intervals;
- selecting arbitrary rate limits;
- exposing PNR lookup publicly;
- letting discounts create negative margin;
- storing all raw supplier payloads indefinitely;
- using an embedded mobile web view for payment;
- making email the identity key;
- allowing support impersonation;
- combining booking state and live operational flight status.
