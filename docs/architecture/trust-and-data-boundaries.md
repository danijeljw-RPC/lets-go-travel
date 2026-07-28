# Trust and Data Boundaries

## Identity Boundary

Keycloak owns authentication credentials, authentication sessions, recovery and token issuance. PostgreSQL links a platform customer to an immutable Keycloak subject identifier and stores product profile data. Email is not the primary immutable identity key.

## Product Data Boundary

PostgreSQL owns customer profile, saved travellers, trips, preferences, consents, platform booking records, price records, reconciliation state, booking versions, notifications, support context and platform audit data.

## Supplier Boundary

Suppliers receive only the traveller, contact, payment reference and itinerary data required for a requested fulfilment operation. Supplier records remain external fulfilment records; the platform stores internal mappings and the evidence necessary to serve the customer.

## Client Boundary

Clients hold short-lived authentication material and approved cached trip data. They do not hold supplier secrets, calculate authoritative totals, determine final booking state or perform reconciliation.

## Administrative Boundary

Support and administrative access requires separate privileged capabilities, stronger authentication, masked sensitive data and detailed audit. A booking reference alone never grants access.

## Network Trust Boundaries

Every internet, identity-provider, database, supplier, notification and object-storage boundary requires authentication, authorisation where applicable, encryption, timeouts, failure handling, correlation and redaction.
