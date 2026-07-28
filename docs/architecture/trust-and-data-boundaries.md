<!-- markdownlint-disable MD013 -->

# Trust and Data Boundaries

## Identity Boundary

Keycloak owns authentication credentials, authentication sessions, recovery and token issuance. PostgreSQL links a platform customer to an immutable Keycloak subject identifier and stores product profile data. Email is not the primary immutable identity key.

## Product Data Boundary

PostgreSQL owns customer profile, saved travellers, trips, preferences, consents, platform booking records, price records, reconciliation state, booking versions, notifications, support tickets, immutable ticket threads, guest-token hashes and platform audit data. Static UI translations remain version-controlled .NET localisation resources rather than database content.

## Supplier Boundary

Suppliers receive only the traveller, contact, payment reference and itinerary data required for a requested fulfilment operation. Supplier records remain external fulfilment records; the platform stores internal mappings and the evidence necessary to serve the customer.

## Client Boundary

Clients hold short-lived authentication material and approved cached trip data. They do not hold supplier secrets, calculate authoritative totals, determine final booking state or perform reconciliation.

## Administrative Boundary

Support and administrative access requires separate privileged capabilities, stronger authentication, masked sensitive data and detailed audit. A booking reference, ticket UUIDv7 or object key alone never grants access. Guest ticket access requires the separate high-entropy magic-link token, and private attachments require ticket authorisation before short-lived access is issued.

## Network Trust Boundaries

Every internet, identity-provider, database, supplier, notification and object-storage boundary requires authentication, authorisation where applicable, encryption, timeouts, failure handling, correlation and redaction.
