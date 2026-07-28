<!-- markdownlint-disable MD013 -->

# Open-Issue Decision Closeout Design

## Purpose

Record the product owner's 2026-07-28 answers for OI-0001 through OI-0012, distinguish settled product decisions from unverified supplier or compliance evidence, and define the documentation changes required before implementation planning. OI-0010 and OI-0011 remain open because the product owner explicitly deferred them.

This is a documentation and planning design. It does not implement application code, database schemas, supplier calls, payment routes, background workers, support tooling or infrastructure.

## Decision-Lifecycle Approach

Use a mixed lifecycle:

- close an issue when the product or architecture choice is explicit and the issue can record the owner's answer as decision evidence;
- move an issue to `in-review` when a direction is selected but external supplier, contractual, security, PCI or production evidence is still required;
- leave an issue open when the owner deferred the decision;
- retain every production evidence gate even when the preferred option is selected.

This approach rejects two extremes: closing supplier-dependent issues from portal observations alone, and leaving settled product choices open merely because implementation or go-live validation has not begun.

## Issue Disposition

| Issue | Target status | Recorded outcome |
| --- | --- | --- |
| OI-0001 | closed | The first release supports hotel-only, flight-only and combined hotel-plus-flight trips out of the box. A combined trip may contain separately fulfilled bookings; it does not imply a package, one price, one contract or one payment. Flight launch remains gated by the in-review flight issues. |
| OI-0002 | in-review | Select Option A as the initial operating direction: LiteAPI or its payment entity handles customer payment and the platform facilitates booking and operational review. Written merchant-of-record, settlement, refund, dispute, tax and consumer-responsibility evidence remains required before production. |
| OI-0003 | in-review | Record the product owner's portal observation that Qantas, Jetstar and Virgin Australia are visible. Do not treat visibility as proof of production account entitlement, bookable content, fare completeness or servicing capability. |
| OI-0004 | in-review | Adopt automated flight-booking reconciliation, immutable itinerary versions and customer notification. Supplier retrieval freshness, schedule-change propagation, exchanges, cancellations, refunds and manual escalation remain evidence gates. |
| OI-0005 | in-review | Select webhook-first processing with a durable inbox and scheduled reconciliation fallback. Exact event coverage, authentication, ordering, retries, replay and environment differences remain evidence gates. |
| OI-0006 | in-review | Prefer LiteAPI's officially supported SDK, hosted JavaScript or hosted payment experience on each supported client platform. Raw card data never enters the platform. Platform-specific support and qualified PCI scope remain evidence gates. |
| OI-0007 | closed | Australia is the initial operating focus with `en-AU` and AUD defaults, but the application does not geo-block customers from other countries. Locale is user-selectable from the first anonymous and authenticated web experiences. |
| OI-0008 | closed | Booking-time sensitive traveller data is entered for each booking by default. Saving date of birth, passport or identity-document data requires a separate, explicit, granular opt-in that is off by default and can never be bundled into account registration or booking acceptance. |
| OI-0009 | closed | Use .NET 10 Blazor Web App with server-side rendering for the web experience and ASP.NET Core Web API for the application boundary. JavaScript is limited to necessary browser interoperability such as an approved provider payment component. |
| OI-0010 | open | No decision. Operational flight-status scope and provider remain deferred. |
| OI-0011 | open | No decision. Supplier payload and booking-evidence retention remain deferred. |
| OI-0012 | closed | Use first-party asynchronous ticket support, with no built-in live chat. A later third-party chat tool may be added without replacing ticket history or the support module's authority. |

Closed issue records move from `docs/issues/open/` to `docs/issues/closed/`. In-review records remain in `docs/issues/open/` with updated status, decision, evidence and remaining-gate sections. The issue index will show five closed, five in review and two open issues.

## Product Scope and Booking Composition

The initial customer experience supports three entry paths: hotel, flight and hotel plus flight. Hotel and flight offers retain product-specific search, terms, booking, payment, cancellation and servicing behaviour. A combined trip is an orchestration and presentation capability over one or more supplier bookings; it must not be described as a regulated package, dynamically packaged price, single supplier order or single merchant transaction unless later commercial and legal evidence establishes that model.

The platform must not expose a flight offer that it cannot complete through an approved booking and payment route. Feature controls may suppress flight search or booking for unsupported markets, routes or provider capabilities without removing flights from the product's intended launch scope.

## LiteAPI Commercial and Payment Boundary

The initial direction is supplier/provider-controlled customer payment through LiteAPI-supported payment facilities. The platform initiates the required payment and booking workflow, stores only permitted opaque references, reconciles outcomes and presents them through customer and staff views. It does not receive raw PAN, CVV or sensitive authentication data.

The selected direction does not establish merchant-of-record responsibility by itself. OI-0002 remains in review until written terms establish the merchant, statement descriptor, commission or margin, settlement, booking-failure ownership, refunds, disputes, chargebacks, tax and consumer obligations for hotels, flights and any combined customer journey.

The web client uses LiteAPI's approved hosted or embedded payment component where officially supported. Any later mobile client must use an officially supported platform SDK, hosted system-browser flow or approved embedded component. Custom card forms and unsupported web views remain prohibited. A future platform-owned Stripe route requires a separate activation decision and does not belong to the initial LiteAPI route.

## Flight Reconciliation and Notification Architecture

Flight monitoring uses a dedicated private .NET 10 worker container. It is a workload-specific worker permitted by ADR-0006, not a serverless function and not an operating-system crontab embedded in a container. Durable PostgreSQL work records and the selected background scheduling mechanism determine due work, claim execution safely and survive restarts.

The schedule is:

- check every active future flight booking at least once per calendar day outside the final 24 hours before scheduled departure;
- check every active flight booking at least once per hour during the final 24 hours before each affected flight segment's scheduled departure;
- stop proximity checks after departure, cancellation or another terminal condition defined by the booking lifecycle;
- allow authenticated webhooks and pending-operation recovery to enqueue immediate reconciliation without waiting for the next scheduled check.

Each check retrieves the latest booking representation available from the fulfilment supplier and follows ADR-0004: map, canonicalise, hash, compare, append an immutable version only for meaningful change, update current state transactionally and emit an outbox event. A notification handler classifies the change, deduplicates customer communication, renders it in the customer's effective locale and records delivery attempts. A failed retrieval records an inspectable failure and never implies that the itinerary is unchanged.

These checks reconcile booking, ticket and itinerary data only. They do not claim live gate, terminal, aircraft, diversion or actual movement status; OI-0010 remains the boundary for that capability.

## Webhooks and Scheduled Safety Net

Authenticated supplier webhooks are persisted before processing in a durable inbox. Duplicate delivery, out-of-order arrival or handler failure cannot directly overwrite current booking state. Processing enqueues supplier retrieval and reconciliation rather than trusting an event payload as complete current state.

Scheduled reconciliation covers active hotel and flight bookings where customer value or unresolved state justifies polling. Frequency is capability-aware and must respect LiteAPI terms and quotas. The flight proximity schedule above is the minimum product policy, subject to supplier rate limits; if the supplier cannot support it, flight production readiness remains blocked rather than silently reducing the customer promise.

## Locale and Translation Design

Australia is the operating launch market, not an access restriction. The initial defaults are `en-AU` and AUD, while itinerary times retain each travel location's timezone and supplier transaction currency remains authoritative.

Anonymous visitors select a locale that is stored in a secure, same-site preference cookie. Authenticated customers store their preferred BCP 47 locale in the PostgreSQL customer profile. On sign-in, the account preference is authoritative; when an authenticated customer changes language, the profile and current browser preference are updated consistently.

UI translations live in version-controlled .NET localisation resources deployed with the Blazor application. The database stores locale preference and locale-dependent customer content where required, but it is not the source for static UI translations. Missing resources fall back through the configured culture hierarchy to the product's `en-AU` source text. Notification templates use the same locale identifiers and require a deliberate fallback so a missing translation cannot suppress a critical message.

Currency selection is separate from language. AUD is the initial display default, but the system preserves supplier, transaction, charged, settlement and refund currencies independently. Supporting additional display or transaction currencies remains subject to supplier and commercial evidence.

## Sensitive Traveller Data

The default booking flow does not create a reusable sensitive traveller profile. Customers enter required date-of-birth, passport and identity-document fields for the booking that needs them. Booking evidence may retain the minimum required snapshot under the unresolved OI-0011 retention policy; that is distinct from saving the data for future bookings.

Saving sensitive traveller data requires a separate control that clearly names the data categories, purpose and effect. It is unchecked by default, requires an affirmative action, and cannot be implied by account creation, a general privacy notice, booking submission or use of a previously saved low-risk traveller profile. Date of birth and each identity document can be removed from future-use storage without rewriting an existing booking record.

Saved sensitive values require field-level protection, masking, least-privilege access, audit, key management and explicit deletion behaviour before the opt-in feature can be activated. The closed issue records the product policy; security and privacy readiness remain implementation and go-live gates rather than reasons to default the feature on.

## Blazor Web Boundary

The web application is a .NET 10 Blazor Web App using server-side rendering. Interactive server components are introduced only where the customer flow requires them. The ASP.NET Core Web API remains the public application and supplier-neutral contract for web and later clients, consistent with ADR-0002.

The browser never receives supplier API credentials. Necessary LiteAPI payment JavaScript is isolated behind a small interoperability boundary and may create provider-scoped tokens or references that are submitted to the platform API. It does not move authoritative pricing, booking or payment state into browser code.

## Ticket Support Design

The support module provides asynchronous ticket support for authenticated customers and guests. Ticket creation collects:

- name;
- email;
- optional booking or customer reference;
- a required support-category selection;
- initial message;
- optional attachments subject to type, size and malware controls.

For an authenticated customer, the email field is populated from the authoritative account email and cannot be edited in the ticket form. Guest users provide an email address. Every ticket begins in `New`.

The supported states are:

- `New`: no support response has yet been sent;
- `Waiting on Support`: the customer's most recent accepted message is newer than the latest support response;
- `Waiting on Customer`: support's most recent accepted response is newer than the latest customer response;
- `Closed`: an authorised support user closed the ticket because no further action is required.

An accepted customer reply changes `New` or `Waiting on Customer` to `Waiting on Support`. An accepted support reply changes `New` or `Waiting on Support` to `Waiting on Customer`. Closing is an explicit support action. A later customer reply to a closed ticket does not silently mutate history; the implementation plan must choose and test either controlled reopening or creation of a linked follow-up ticket before that behaviour ships.

Each ticket has an internal UUIDv7 identifier and a separate cryptographically random bearer token for guest access. Only a one-way hash of the bearer token is stored. The emailed magic link contains both routing information and the secret token, can be revoked or rotated, and grants access only to that ticket. A UUIDv7 alone is not an access secret.

Attachments are private objects in an S3-compatible object store. Ticket messages store an object reference and safe display metadata, not a permanently public URL. After ticket authorisation, the platform issues a short-lived signed download response or streams the object. Uploads use non-executable content disposition, allowlisted types, size/count limits and malware scanning or quarantine before customer/support download.

Every accepted customer or support update records an immutable thread entry and queues an email notification to the ticket email address. Notification delivery is durable, retryable and deduplicated. Emails contain the magic link but do not include sensitive attachments or unnecessary booking/passport data. A later third-party live-chat integration may create or append ticket messages through a controlled adapter, but live chat is not part of the initial product.

## Error Handling and Operational Rules

- Supplier timeouts and ambiguous booking or payment outcomes reconcile before retry.
- Duplicate webhooks, scheduled jobs and notifications are idempotent.
- Missed worker schedules become overdue durable work and are observable after restart.
- A supplier result that cannot be mapped enters an inspectable support-required state without discarding the last known booking version.
- A critical itinerary change that cannot be delivered to the customer raises an operational alert and remains visible in the support dashboard.
- Invalid, expired or revoked ticket tokens reveal no ticket existence or customer information.
- Attachment failures do not discard an otherwise valid message; the UI reports which attachment failed and permits a safe retry.
- Email delivery failure remains visible to support and does not roll back the persisted ticket reply.
- Missing translations use the explicit locale fallback and produce telemetry rather than broken or empty UI.

## Documentation Change Set

After this design is approved, the canonical documentation pass will:

1. create accepted ADR-0008 for the flight-reconciliation worker and notification schedule, relating it to ADR-0004, ADR-0005 and ADR-0006 without superseding their broader decisions;
2. move OI-0001, OI-0007, OI-0008, OI-0009 and OI-0012 to `docs/issues/closed/` with recorded decisions and evidence;
3. mark OI-0002 through OI-0006 `in-review`, preserving each outstanding external evidence gate;
4. leave OI-0010 and OI-0011 open and substantively unchanged;
5. update the ADR and issue indexes, working assumptions, open-question summary and PLAN-0001 status/checklists;
6. update MVP scope, roadmap, payment, client, architecture, reconciliation, traveller-data, privacy and operational support documents so they match the decisions;
7. retain all implementation details that still require mechanism selection in PLAN-0001 rather than inventing code, schema or infrastructure.

## Validation

The documentation pass must verify:

- exactly one H1 in every Markdown file;
- every local Markdown link resolves with correct filename casing;
- ADR and issue index counts match filesystem lifecycle state;
- no open-path link remains for a closed issue;
- OI-0010 and OI-0011 retain open status and unresolved meaning;
- LiteAPI portal visibility is never described as production entitlement or complete servicing evidence;
- payment text never claims a merchant, PCI SAQ or compliance result without evidence;
- flight reconciliation is never described as live operational flight status;
- `UUIDv7` is never treated as the guest access secret;
- ticket attachments are never described as permanently public objects;
- no C360, tenant, TMC, reseller or enterprise assumptions enter the consumer product documentation;
- `git diff --check` passes and the final diff contains documentation only.

## Deferred Decisions

Only the following named decisions remain intentionally unresolved by this design:

- OI-0010: whether live operational flight status belongs in the product and which provider supplies it;
- OI-0011: raw supplier payload and booking-evidence retention periods and protections;
- LiteAPI production commercial responsibilities, account-level carrier capability, flight servicing, webhook guarantees and qualified PCI evidence tracked by OI-0002 through OI-0006;
- the mechanism used for durable scheduling, provided it satisfies the accepted container and PostgreSQL durability boundaries;
- whether a customer reply reopens a closed support ticket or creates a linked follow-up ticket.

These deferrals do not permit implementation to select an answer silently.
