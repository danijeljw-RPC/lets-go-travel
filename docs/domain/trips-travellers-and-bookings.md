<!-- markdownlint-disable MD013 -->

# Trips, Travellers and Bookings

## Trip

A trip is the customer-facing aggregate for travel planning and management. It may exist before any supplier booking and may include supplier-managed bookings, manually added items, notes, documents, reminders and notifications.

Likely trip attributes include platform ID, owning customer, title, destinations, start/end dates, timezone context, status, travellers and timestamps. Sharing is deferred and must not be assumed in initial authorisation.

## Customer and Traveller

The customer owns the account, trip and booking. A traveller is a person participating in travel. A customer may maintain travellers for themselves, family or friends under closed [OI-0008](../issues/closed/OI-0008-saved-traveller-and-passport-data.md).

Booking-time traveller details are snapshotted because later profile changes must not rewrite what was supplied for an existing booking.

Date of birth, passport and identity-document data is entered per booking by default. Saving any of those categories for future bookings requires a separate unchecked, affirmative and revocable opt-in. General account registration, booking acceptance or saving a low-risk traveller profile cannot imply that consent. Reusable sensitive storage and booking-evidence retention are distinct purposes; OI-0011 continues to govern the unresolved evidence-retention schedule.

## Booking

A booking is the platform's durable representation of an external reservation. It has a platform ID, customer and trip ownership, product category, supplier mappings, external references, current platform and supplier status, price records, cancellation/refund state, current normalised travel detail, reconciliation state and version history.

## Product-specific Detail

Accommodation and flights share lifecycle concepts but should not be forced into one universal detail model. Accommodation needs property, room, occupancy, stay, meal and cancellation concepts. Flights need journey, leg, segment, airport, local time, carrier, cabin, fare, baggage, seat, PNR and ticket concepts.

## Manual Items

Manually entered flights, hotels, activities, rail, restaurants, notes and documents must be clearly distinguished from supplier-managed bookings because the platform cannot necessarily reconcile, cancel or service them.

## Trip Timeline

Customer-relevant events such as trip creation, booking/payment confirmation, an approaching check-in, a supplier change, cancellation/refund completion, document addition and future traveller invitations come from durable domain events or records rather than diagnostic logs.

## Support Ticket

A support ticket is owned by the support module and may refer to a customer, trip or booking without becoming part of the booking aggregate. It records an internal UUIDv7 identifier, customer or guest contact details, optional reference, category, current state, immutable message thread, private attachment references, notification history and audit metadata.

Tickets begin as `New`. The latest accepted customer message produces `Waiting on Support`; the latest accepted support response produces `Waiting on Customer`; an authorised support action produces `Closed`. Guest access uses a separate high-entropy bearer token stored only as a hash, not the UUIDv7 identifier. Attachments remain private S3-compatible objects and require ticket authorisation before short-lived access is issued.

## Sharing

Future sharing may introduce owner, editor, viewer and traveller capabilities. Sharing never implicitly grants access to passport, payment or account data and must be added through an explicit authorisation design rather than inferred from trip participation.

## Retention

Removing a trip from the customer view does not automatically delete confirmed bookings, financial evidence, support records or legally retained data. Retention and anonymisation rules remain an open privacy/commercial decision.

The product distinguishes hiding/archiving, removing an item from a trip, deleting a draft, deleting a preference, closing an account, retaining required booking evidence and anonymising eligible records.
