# Identity and Access

## Identity ownership

Keycloak should be the platform's identity provider and source of authentication state.

LiteAPI should not be treated as the platform's customer identity system.

The customer's account relationship is with the platform. Supplier APIs receive only the data required to search, prebook, book, service, or cancel a reservation.

## Client authentication

The web UI, Android app, and iOS app should use standard OAuth 2.0 and OpenID Connect flows appropriate to public clients.

The final flow selection must account for:

- browser security;
- native-app redirect handling;
- Proof Key for Code Exchange;
- refresh-token rotation;
- token storage;
- session revocation;
- logout;
- account recovery;
- device compromise;
- multiple concurrent devices.

No client secret should be embedded in a browser or mobile application.

## API access

Clients send bearer access tokens to the Web API.

The API should validate:

- issuer;
- audience;
- signature;
- expiry;
- not-before time;
- required scopes or permissions;
- token type;
- any required authentication context.

The API should derive the caller's stable identity from a non-reassignable subject identifier rather than relying on mutable email addresses.

## Platform user record

The application database should maintain a platform user record linked to the Keycloak subject.

The platform record may contain:

- internal user ID;
- Keycloak subject ID;
- status;
- profile completion state;
- preferred locale;
- preferred currency;
- notification preferences;
- consent records;
- created and updated timestamps.

Authentication credentials remain in Keycloak.

## Customer and traveller distinction

The account holder and the people travelling are not always the same.

The model should distinguish:

- **Customer account**: the authenticated owner of trips and bookings.
- **Traveller profile**: a person whose details may be used in a booking.

A customer may maintain several travellers, including:

- themselves;
- partner;
- children;
- relatives;
- friends.

Authorisation must ensure one customer cannot retrieve or use another customer's traveller records.

## Roles and capabilities

Initial consumer roles may remain small:

- Customer;
- Support;
- Administrator.

Roles alone may be too broad for privileged operations. Fine-grained policies or capabilities should be considered for:

- reading a customer's booking;
- changing booking state;
- initiating cancellation;
- viewing sensitive traveller data;
- resending documents;
- issuing manual credits;
- accessing supplier payloads;
- impersonation or assisted support;
- changing pricing rules;
- viewing audit history.

## Scopes

OAuth scopes may be used to communicate intended API capabilities, especially if partner or third-party clients are added later.

Potential scope areas include:

- profile;
- travellers;
- trips;
- search;
- bookings;
- documents;
- notifications.

Scopes do not replace resource-level ownership checks.

## Sensitive profile data

Saved passport, date-of-birth, loyalty, and identity-document details require additional controls.

Before storing them, determine:

- whether they are genuinely required;
- encryption approach;
- field-level access policy;
- masking;
- retention period;
- deletion process;
- support access;
- audit requirements;
- mobile offline behaviour;
- backup exposure;
- breach impact.

The safest initial option may be to collect some details only during booking rather than permanently save them.

## Account lifecycle

The design must cover:

- registration;
- email verification;
- social sign-in;
- duplicate-account detection;
- account linking;
- password reset;
- MFA recovery;
- account suspension;
- deletion request;
- data export;
- active-booking retention after account deletion;
- legal retention;
- token revocation;
- support-assisted recovery.

## Administrative access

Support and administrator access should use separate privileged roles and stronger controls than consumer access.

Consider:

- mandatory MFA;
- shorter sessions;
- device or network restrictions;
- detailed audit;
- approval for high-impact actions;
- masking by default;
- no routine access to full passport or payment-related data;
- break-glass accounts stored and reviewed separately.
