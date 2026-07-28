<!-- markdownlint-disable MD013 -->

# Constraints

## Product Constraints

- The product is consumer-focused and must not inherit unrelated enterprise structures.
- Supplier capability and commercial availability are external dependencies.
- Customer-facing claims must reflect verified account and market capability.
- Hotel, flight and combined hotel-plus-flight journeys are product scope, but unsupported supplier offers must be suppressed rather than advertised.
- Australia is the operating focus without geo-blocking customers from other countries.

## Security Constraints

- Supplier credentials remain server-side.
- Raw card number, CVV and sensitive authentication data do not pass through or persist in the platform backend.
- Resource ownership is enforced by platform identity, not by booking reference alone.
- Sensitive traveller data is minimised and protected.
- Reusable date-of-birth and identity-document storage requires separate, granular opt-in and is off by default.
- A ticket UUIDv7 is an identifier, not an access secret; guest access requires a separate protected bearer token.

## Data Constraints

- Authentication state, product/customer data and supplier fulfilment data have distinct authorities.
- Current booking state and immutable booking history remain distinct.
- Original transaction/supplier currency is retained independently of display conversion.
- Travel times preserve local timezone context.
- Raw supplier payload retention is disabled by default and uses the approved short-lived allowlist.
- Legal holds suspend deletion only for explicitly scoped records and do not create blanket retention.

## Delivery Constraints

- No implementation plan should assume unresolved ADRs are accepted.
- Flight-selling plans must preserve account-level carrier, fare, ticketing and servicing evidence gates even though flights are accepted product scope.
- No mobile checkout plan should assume a supported payment integration or PCI scope.
- Static UI translations belong in version-controlled .NET localisation resources; PostgreSQL stores authenticated locale preference rather than acting as the translation catalogue.
