# Identity and Access

## Proposed Identity Model

Keycloak owns credentials, verification, recovery, multifactor authentication, external identity federation, sessions and token issuance. The application stores a platform customer linked to the stable Keycloak subject ID.

## Clients

Browser and native clients use OAuth 2.0/OpenID Connect flows appropriate to public clients, including PKCE. No client secret is embedded in a browser or mobile application.

## API Validation

Validate issuer, audience, signature, expiry, not-before, token type and required permissions. Scopes communicate capability but never replace resource ownership checks.

## Ownership

A customer accesses only their trips, travellers and bookings unless an explicit future sharing model grants access. PNR and surname are not an authentication mechanism for the public platform API.

## Privileged Access

Support/admin access requires mandatory MFA, short sessions, explicit capabilities, masking, detailed audit and tightly controlled impersonation if impersonation is ever permitted.
