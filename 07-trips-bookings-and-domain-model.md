# Trips, Bookings, and Domain Model

## Trip as the customer aggregate

The customer-facing organising concept is a Trip.

A trip should be able to exist before any booking is made.

Potential trip attributes include:

- owner;
- title;
- destination or destinations;
- start and end dates;
- timezone context;
- travellers;
- status;
- notes;
- preferences;
- sharing settings;
- created and updated timestamps.

A trip may contain supplier bookings and manually added items.

## Booking as a platform record

A Booking is the platform's durable representation of an external reservation.

It should not simply be a stored LiteAPI response.

Common booking attributes may include:

- platform booking ID;
- trip ID;
- owning customer ID;
- product category;
- supplier;
- supplier booking ID;
- external confirmation references;
- current status;
- booked timestamp;
- original currency;
- customer total;
- supplier total where appropriate;
- current cancellation state;
- current reconciliation state;
- last checked timestamp.

## Product-specific detail

Flights and accommodation share some lifecycle concepts but have different domain detail.

The model should avoid forcing them into one oversized universal table.

Accommodation may require:

- property;
- room;
- occupancy;
- check-in and check-out;
- meal plan;
- cancellation rules;
- guest assignments;
- property contact details.

Flights may require:

- journeys;
- legs;
- segments;
- marketing and operating carrier;
- flight number;
- departure and arrival local date-time;
- airport;
- terminal;
- booking class;
- fare brand;
- baggage;
- seats;
- traveller ticketing details;
- PNR and ticket references.

Shared booking workflow does not require identical storage structures.

## Customer and travellers

A booking belongs to a customer account, while booking participants are travellers.

Traveller data should be snapshotted into the booking where necessary because a saved traveller profile can change later.

For example, changing a profile phone number should not rewrite the historical details submitted with a booking.

## Current normalised state

The current booking state should be stored in queryable relational structures used by:

- trip display;
- booking details;
- notifications;
- support;
- upcoming-trip queries;
- cancellation eligibility;
- customer documents.

This is separate from immutable version history.

## Supplier references

Supplier references should be isolated from public identifiers.

The platform may need to retain:

- LiteAPI booking ID;
- airline PNR;
- hotel confirmation number;
- supplier order ID;
- payment transaction ID;
- provider-specific traveller ID;
- supplier environment;
- supplier account.

Not every reference should be exposed to the customer.

## Booking lifecycle

The platform needs its own lifecycle mapping.

Potential states include:

- Draft;
- OfferSelected;
- PrebookPending;
- PriceChanged;
- PaymentPending;
- BookingPending;
- Confirmed;
- PartiallyConfirmed;
- ChangePending;
- CancellationPending;
- Cancelled;
- RefundPending;
- Refunded;
- Failed;
- RequiresSupport.

Supplier states should map into platform states without losing the original value.

## Trip timeline

A trip timeline can present customer-relevant events such as:

- trip created;
- booking confirmed;
- payment completed;
- hotel check-in approaching;
- flight schedule changed;
- cancellation confirmed;
- refund completed;
- document added;
- traveller invited.

The timeline should be generated from durable domain events or records, not only application logs.

## Manually added items

Customers may want to include reservations made elsewhere.

The domain should leave room for:

- manually added flights;
- manually added hotels;
- restaurant bookings;
- activities;
- rail;
- notes;
- addresses;
- confirmation PDFs.

Manual items should be clearly distinguished from supplier-managed bookings because the platform may not be able to reconcile or service them.

## Sharing

Future trip sharing introduces resource-level authorisation.

Potential roles include:

- owner;
- editor;
- viewer;
- traveller.

Sharing should not automatically grant access to sensitive passport, payment, or account data.

## Deletion and retention

Deleting a trip does not necessarily mean deleting all booking records.

Confirmed bookings, financial records, support history, and legal records may require retention.

The product should distinguish:

- hide or archive;
- remove from trip;
- delete draft;
- delete personal preference;
- close account;
- retain booking evidence;
- anonymise where permitted.
