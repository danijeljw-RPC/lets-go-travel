# Mobile and Web Experience

## Shared backend

The web UI and future mobile applications should use the same platform API and domain rules.

The clients differ in presentation and device capability, not in authoritative business behaviour.

## Web application

The initial web experience should establish:

- account onboarding;
- destination search;
- date and traveller selection;
- search results;
- filters;
- accommodation or flight detail;
- prebook review;
- secure payment;
- confirmation;
- trip view;
- booking detail;
- cancellation flow;
- account and traveller management;
- support access.

Responsive design should anticipate later mobile patterns without pretending a browser is identical to a native app.

## Mobile applications

The Android and iOS apps should:

- use Keycloak-compatible native authentication;
- call the Web API directly;
- store tokens securely;
- receive push notifications;
- cache approved trip data;
- open secure payment flows;
- support deep links;
- display offline trip essentials;
- respect minimum API and app versions.

They should not embed LiteAPI credentials or authoritative pricing logic.

## Payment on mobile

The final payment method must be validated against LiteAPI's supported integration paths.

Potential patterns may include:

- system browser redirect;
- secure hosted checkout;
- approved embedded provider component;
- web view only if explicitly supported and compliant.

The design must handle return-to-app, abandoned payment, duplicate callback, and payment success without booking confirmation.

## Offline trip pack

A later mobile capability should make essential travel data available without connectivity.

Candidates include:

- booking references;
- flight and hotel summary;
- local departure and arrival times;
- airport and hotel addresses;
- cancellation contact details;
- vouchers and selected documents;
- emergency support details.

Highly sensitive documents should require deliberate offline-storage policy.

## Notifications

Notification channels may include:

- in-app;
- email;
- push;
- later SMS for critical events.

The notification model should support:

- preference;
- urgency;
- deduplication;
- retry;
- language;
- timezone;
- quiet hours where appropriate;
- lock-screen privacy;
- acknowledgement;
- links to the affected booking.

## Timezones

Travel data must preserve local timezone context.

The customer needs to see departure and arrival in the relevant local time.

The model should retain:

- local date-time;
- timezone identifier;
- UTC instant where determinable;
- airport or property timezone;
- original supplier representation if needed.

A blanket "store only UTC" rule is insufficient for future scheduled travel because local civil time and timezone rules matter.

## Locale and currency

The platform should support:

- preferred locale;
- preferred display currency;
- supplier original currency;
- charged currency;
- local date and number formatting;
- language fallback.

Displayed currency conversions must be clearly distinguished from the amount actually charged.

## Accessibility

Accessibility should be planned from the first web experience.

Important areas include:

- date pickers;
- traveller selectors;
- filter controls;
- price breakdown;
- cancellation terms;
- payment errors;
- status changes;
- colour-independent warnings;
- screen-reader labelling;
- keyboard operation;
- readable travel documents.

## Customer trust

The UI should clearly distinguish:

- current price from estimated price;
- refundable from non-refundable;
- pay now from pay later;
- platform fee from supplier charge;
- pending from confirmed;
- cancellation requested from cancelled;
- refund initiated from refunded;
- booking data from live flight status;
- supplier-managed booking from manually added trip item.
