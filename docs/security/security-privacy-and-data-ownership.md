<!-- markdownlint-disable MD013 -->

# Security, Privacy and Data Ownership

## Data Classes

Classify public reference data, internal operational data, personal information, travel-document data, authentication data, payment references, supplier secrets, audit data, support notes and itinerary/location data.

## Data Minimisation

Every stored field needs a purpose, authority, access policy, retention period, deletion behaviour, encryption requirement, offline policy and supplier-disclosure rule. Do not collect passport or identity details merely for possible later use.

Booking-time collection does not opt a traveller into reusable sensitive storage. Saving date of birth, passport or identity-document data for future bookings requires a separate granular control that is unchecked by default, names the categories and purpose and records an affirmative customer action. Activation also requires field-level protection, masking, least-privilege access, audit, key management and deletion behaviour. See closed [OI-0008](../issues/closed/OI-0008-saved-traveller-and-passport-data.md).

## Supplier Disclosure

Record which supplier received which data categories, for what fulfilment purpose, against which booking and when. Supplier, airline, hotel and payment-provider obligations may create independent external records.

Where required, retain the applicable consent or legal basis. Send only the traveller, contact, itinerary and provider-scoped payment reference needed for the requested search, booking or servicing operation.

## Secrets and Encryption

Use TLS, encrypted managed storage, protected backups, scoped secret storage and rotation. Keep supplier keys, database credentials, identity client credentials and encryption/signing keys out of source control and routine logs.

## Logging and Audit

Do not log tokens, passwords, supplier keys, full passport numbers, full payment data, CVV or complete supplier payloads by default. Application logs are diagnostic; booking/audit history is durable, structured, access-controlled evidence.

## Privacy Lifecycle

Planning must address access, correction, export, account closure, deletion, consent withdrawal, active bookings, refunds, disputes, legally retained records, cross-border disclosure and backup retention. OI-0011 remains open for the supplier-payload and booking-evidence retention schedule.

Keycloak and the application need an explicit authority/synchronisation rule for overlapping fields such as email, display name, phone and locale. The authenticated BCP 47 locale preference belongs to the PostgreSQL customer profile; anonymous locale belongs to a secure same-site cookie. Removing identity access does not silently rewrite or erase retained booking evidence.

## Mobile Security

Mobile planning covers secure token storage, device compromise, screenshots, offline booking data, biometric convenience versus account authentication, push-notification privacy, deep links, rooted/jailbroken devices, remote session revocation and cached identity documents. Sensitive itinerary detail is not exposed on a locked screen without deliberate customer choice.

## Security Events

Audit security-relevant login/recovery and MFA changes, account linking, data export, sensitive traveller access, new devices, booking/cancellation/refund actions, support impersonation, administrative pricing changes, secret rotation and webhook-authentication failures.
