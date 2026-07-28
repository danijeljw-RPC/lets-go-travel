<!-- markdownlint-disable MD013 -->

# Constraints

## Product Constraints

- The product is consumer-focused and must not inherit unrelated enterprise structures.
- Supplier capability and commercial availability are external dependencies.
- Customer-facing claims must reflect verified account and market capability.

## Security Constraints

- Supplier credentials remain server-side.
- Raw card number, CVV and sensitive authentication data do not pass through or persist in the platform backend.
- Resource ownership is enforced by platform identity, not by booking reference alone.
- Sensitive traveller data is minimised and protected.

## Data Constraints

- Authentication state, product/customer data and supplier fulfilment data have distinct authorities.
- Current booking state and immutable booking history remain distinct.
- Original transaction/supplier currency is retained independently of display conversion.
- Travel times preserve local timezone context.

## Delivery Constraints

- No implementation plan should assume unresolved ADRs are accepted.
- No flight-selling plan should assume Australian carrier coverage or full servicing.
- No mobile checkout plan should assume a supported payment integration or PCI scope.
