# Product Vision and Scope

## Product idea

Build a consumer travel platform where the customer can organise and manage an entire trip through one account and one experience.

The platform should not feel like a thin LiteAPI frontend. LiteAPI is an initial private supplier behind the product. The long-term value is the platform's ownership of:

- the customer relationship;
- identity and profile;
- saved travellers;
- trips;
- booking history;
- pricing presentation;
- notifications;
- travel documents;
- support context;
- planning and itinerary features;
- future artificial-intelligence assistance;
- a consistent web and mobile experience.

## Product centre

The primary product object should be a **Trip**, not a supplier booking type.

A trip can contain:

- flights;
- accommodation;
- ground transport;
- activities;
- restaurant plans;
- notes;
- documents;
- reminders;
- notifications;
- expenses;
- shared travellers;
- future recommendations and generated itineraries.

This avoids fragmenting the experience into unrelated flight, hotel, and activity silos.

## Consumer focus

This is a consumer product.

Initial design should therefore optimise for:

- simple onboarding;
- fast destination and date search;
- transparent pricing;
- trustworthy checkout;
- clear cancellation conditions;
- accessible support;
- useful pre-trip reminders;
- resilient in-trip access;
- strong privacy controls;
- a mobile-ready API;
- straightforward self-service account management.

## Product promise

A useful framing is:

> Everything about my trip lives here.

Booking inventory is necessary, but inventory alone is not the differentiator. The product becomes more valuable after booking by helping the customer prepare, respond to changes, navigate, retrieve documents, and understand the trip.

## Initial capabilities

A sensible first product boundary includes:

- account creation and sign-in;
- profile and preferences;
- saved travellers;
- destination and accommodation search;
- flight search, subject to supplier access and coverage;
- price and availability confirmation before payment;
- supplier-hosted or supplier-controlled secure payment entry;
- booking confirmation;
- trip creation and booking attachment;
- current booking details;
- booking history;
- cancellation requests where supported;
- email and in-app notifications;
- booking reconciliation;
- customer support references and traceability;
- responsive web experience.

## Later capabilities

Later releases may include:

- Android and iOS applications;
- push notifications;
- offline trip packs;
- document storage;
- itinerary sharing;
- activity and attraction inventory;
- rail and transfers;
- travel insurance;
- loyalty and rewards;
- AI-assisted trip planning;
- live disruption information from a dedicated operational-data provider;
- collaborative trips;
- expense and receipt tools;
- partner integrations.

## Non-goals for the first planning phase

The first planning phase should not assume:

- every supplier capability will be available through LiteAPI;
- all airline servicing can be automated;
- one supplier will remain sufficient;
- the platform should become merchant of record;
- the mobile app should contain business rules;
- supplier response models should become the public API;
- all travel products need one universal relational structure;
- operational flight tracking and booking management are the same capability.

## Success criteria

The architecture should allow the platform to:

- add or replace suppliers without redesigning the clients;
- keep supplier credentials private;
- control the customer identity and data model;
- expose one consistent API to web and mobile;
- identify and record supplier booking changes;
- prevent duplicate booking and payment operations;
- explain price composition and discount funding;
- support strong audit and support workflows;
- scale expensive search traffic independently from durable booking operations;
- evolve without forcing immediate client upgrades.
