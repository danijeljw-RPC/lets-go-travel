<!-- markdownlint-disable MD013 -->

# Security, Privacy and Data Ownership

## Data Classes

Classify public reference data, internal operational data, personal information, travel-document data, authentication data, payment references, supplier secrets, audit data, support notes and itinerary/location data.

## Data Minimisation

Every stored field needs a purpose, authority, access policy, retention period, deletion behaviour, encryption requirement, offline policy and supplier-disclosure rule. Do not collect passport or identity details merely for possible later use.

## Supplier Disclosure

Record which supplier received which data categories, for what fulfilment purpose, against which booking and when. Supplier, airline, hotel and payment-provider obligations may create independent external records.

## Secrets and Encryption

Use TLS, encrypted managed storage, protected backups, scoped secret storage and rotation. Keep supplier keys, database credentials, identity client credentials and encryption/signing keys out of source control and routine logs.

## Logging and Audit

Do not log tokens, passwords, supplier keys, full passport numbers, full payment data, CVV or complete supplier payloads by default. Application logs are diagnostic; booking/audit history is durable, structured, access-controlled evidence.

## Privacy Lifecycle

Planning must address access, correction, export, account closure, deletion, consent withdrawal, active bookings, refunds, disputes, legally retained records, cross-border disclosure and backup retention. [OI-0008](../issues/open/OI-0008-saved-traveller-and-passport-data.md) tracks the highest-risk product choice.
