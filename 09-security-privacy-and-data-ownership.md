# Security, Privacy, and Data Ownership

## Data ownership position

The platform should control its customer relationship and maintain the canonical application record for:

- account linkage;
- customer profile;
- saved travellers;
- trips;
- preferences;
- consents;
- notifications;
- platform booking history;
- support interactions;
- platform pricing decisions.

LiteAPI receives only information required to perform supplier operations.

This does not mean the platform exclusively owns all booking data. Supplier, hotel, airline, payment provider, and regulatory obligations may create separate records and rights.

## Data classification

At minimum, classify:

- public reference data;
- internal operational data;
- personal information;
- sensitive travel-document data;
- authentication data;
- payment references;
- cardholder data;
- supplier secrets;
- audit data;
- support notes;
- location and itinerary data.

Travel itinerary data can be highly sensitive because it reveals where a person will be and when.

## Data minimisation

Before storing a field, establish:

- business purpose;
- source;
- users and services allowed to access it;
- retention period;
- deletion behaviour;
- encryption requirement;
- whether it needs to be available offline;
- whether it must be shared with a supplier.

Do not collect passport or identity details merely because the model might need them later.

## Keycloak data boundary

Keycloak should store identity and authentication information necessary for sign-in.

The application database should store product profile and domain data.

A deliberate boundary is required for overlapping fields such as:

- email;
- display name;
- phone;
- locale.

The design should state which system is authoritative and how updates propagate.

## Supplier disclosure

The privacy model must explain that booking data is disclosed to suppliers necessary to fulfil travel services.

The platform should record:

- supplier;
- purpose;
- data categories sent;
- booking reference;
- timestamp;
- consent or legal basis where required.

## Secrets

Secrets include:

- LiteAPI API keys;
- Keycloak client credentials for confidential clients;
- database credentials;
- signing and encryption keys;
- email provider keys;
- push-notification credentials;
- object-storage credentials.

Secrets should be held in a managed secret store, rotated, scoped, and excluded from logs.

## Encryption

Plan for:

- TLS for all network communication;
- encrypted storage;
- encryption of particularly sensitive fields;
- key rotation;
- backup encryption;
- protected object storage;
- device storage encryption and secure keychain use;
- separation between encryption keys and application data.

## Authorisation

Every resource request must enforce ownership or granted access.

Examples:

- a customer can access only their trips;
- a shared traveller cannot automatically access the owner's account;
- support access is explicit and audited;
- booking references alone do not grant access;
- PNR plus surname should not become an unprotected public lookup unless intentionally designed and risk-assessed.

## Logging

Logs should not contain:

- access tokens;
- refresh tokens;
- LiteAPI keys;
- full passport numbers;
- full payment details;
- card verification values;
- complete supplier payloads by default;
- customer passwords;
- unnecessary personal information.

Structured redaction and secure diagnostic access are required.

## Privacy rights

The product needs processes for:

- access request;
- correction;
- export;
- account closure;
- deletion;
- consent withdrawal;
- notification preference changes;
- retained booking and financial records;
- third-party disclosure information.

Australian Privacy Principles should be considered, along with obligations in other markets where the product is offered.

## Account deletion with active travel

Account deletion is complicated when the customer has:

- upcoming bookings;
- pending refunds;
- open disputes;
- support cases;
- legally retained transaction records.

The product needs a clear policy separating account access removal from deletion or anonymisation of retained records.

## Mobile security

Future mobile design must address:

- secure token storage;
- device compromise;
- screenshots;
- offline booking data;
- biometric convenience versus account authentication;
- push-notification content;
- deep links;
- rooted or jailbroken devices;
- remote session revocation;
- cached passport and document data.

Push notifications should avoid exposing sensitive itinerary details on a locked screen unless the customer explicitly enables them.

## Security events

Security-relevant events should include:

- login and recovery;
- MFA changes;
- account linking;
- profile-data export;
- sensitive traveller-data access;
- new device;
- booking creation;
- cancellation;
- refund;
- support impersonation;
- administrative pricing changes;
- secret rotation;
- webhook authentication failures.
