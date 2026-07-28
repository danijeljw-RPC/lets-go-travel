<!-- markdownlint-disable MD013 -->

# Identity and Access

## Proposed Identity Model

Keycloak owns credentials, verification, recovery, multifactor authentication, external identity federation, sessions and token issuance. The application stores a platform customer linked to the stable Keycloak subject ID.

The platform customer record may hold its internal ID, Keycloak subject ID, status, profile-completion state, locale, currency, notification preferences, consent records and lifecycle timestamps. Authentication credentials remain exclusively in Keycloak.

## Clients

Browser and native clients use OAuth 2.0/OpenID Connect flows appropriate to public clients, including PKCE. No client secret is embedded in a browser or mobile application.

## API Validation

Validate issuer, audience, signature, expiry, not-before, token type and required permissions. Scopes communicate capability but never replace resource ownership checks.

Potential scope areas include profile, travellers, trips, search, bookings, documents and notifications. The API derives identity from the non-reassignable subject identifier rather than mutable email.

## Ownership

A customer accesses only their trips, travellers and bookings unless an explicit future sharing model grants access. PNR and surname are not an authentication mechanism for the public platform API.

## Privileged Access

Support/admin access requires mandatory MFA, short sessions, explicit capabilities, masking, detailed audit and tightly controlled impersonation if impersonation is ever permitted.

Capabilities should separately control reading bookings, changing state, initiating cancellations, viewing sensitive traveller data, resending documents, issuing manual credits, inspecting permitted supplier evidence, changing pricing rules and viewing audit history. High-impact actions may require approval, device/network restrictions or a separately reviewed break-glass path.

## Account Lifecycle

Identity planning must cover registration, verification, external sign-in, duplicate-account detection and linking, password/MFA recovery, concurrent devices, suspension, token/session revocation, export, closure and support-assisted recovery. Account access removal is distinct from anonymising or retaining booking, refund, dispute and legal evidence.
