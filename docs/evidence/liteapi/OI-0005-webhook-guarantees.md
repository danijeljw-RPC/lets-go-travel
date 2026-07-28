---
document_type: issue-addendum
issue_id: OI-0005
title: LiteAPI Webhook Guarantees and Production Reliance Addendum
status: evidence-recorded
created: 2026-07-28
updated: 2026-07-28
owners:
  - Architecture
  - Security
  - Supplier Integration
applies_to:
  - LiteAPI
  - Nuitee Connect
related_adrs:
  - ADR-0002
  - ADR-0004
  - ADR-0008
related_docs:
  - OI-0005-liteapi-webhook-coverage.md
  - docs/integrations/liteapi-nuitee-connect.md
  - docs/operations/reliability-and-supportability.md
---

<!-- markdownlint-disable MD013 MD025 -->

# OI-0005 Addendum — LiteAPI Webhook Guarantees and Production Reliance

## Purpose

This addendum records the public LiteAPI/Nuitee Connect evidence available for the following open issue:

| Review | Evidence or approval required | Owner | Effect |
| --- | --- | --- | --- |
| OI-0005 webhook guarantees | Account event catalogue, authentication, ordering, duplication, retry, retention, replay and environment differences. | Architecture, Security, Supplier Integration | Does not block durable inbox/reconciliation implementation; blocks production webhook reliance. |

It supplements, rather than replaces, `OI-0005-liteapi-webhook-coverage.md`.

## Executive conclusion

LiteAPI publicly documents enough webhook behaviour to proceed with the selected **webhook-first plus reconciliation safety-net** architecture.

The documented model is:

- an HTTPS endpoint registered in the Nuitee Connect dashboard;
- selected hotel and, where enabled, flight event subscriptions;
- an optional static authentication token sent in the `authorization` request header;
- configurable maximum retries and initial retry delay;
- exponential retry backoff when a successful response is not received;
- acknowledgement through an HTTP `2xx` response;
- at-least-once delivery semantics;
- possible duplicate deliveries;
- a unique `event_id` intended for deduplication and idempotency;
- stringified JSON in the `request` and `response` properties; and
- a `sandbox` Boolean identifying the source environment.

LiteAPI does **not** publicly document:

- ordered delivery;
- a signed payload or HMAC verification scheme;
- a webhook timestamp, nonce or documented replay-prevention mechanism;
- webhook source IP ranges;
- default or maximum retry values;
- delivery timeout;
- event retention duration;
- a dead-letter facility;
- customer-initiated replay;
- provider-initiated replay after the configured retries are exhausted;
- a guaranteed maximum delivery delay;
- a complete account-specific production event catalogue; or
- a dedicated flight schedule-change, exchange or refund webhook event.

Therefore:

1. webhook receipt must never directly mutate authoritative booking state;
2. every accepted event must first be persisted to a durable inbox;
3. processing must be idempotent and safe for duplicates;
4. event ordering must be treated as undefined;
5. the event should trigger supplier retrieval and reconciliation;
6. scheduled reconciliation remains mandatory for missing, delayed or unsupported events; and
7. production reliance remains gated until the account-specific subscriptions, authentication configuration and controlled production delivery tests have been recorded.

## Recommended issue disposition

The architecture decision in OI-0005 is complete and Option A remains selected.

The issue should remain **in review** until the production-account evidence checklist in this addendum has been completed. Once completed, OI-0005 may be closed without changing the durable-inbox and reconciliation design.

Production approval must mean **approval to use webhooks as a low-latency trigger**, not approval to use webhooks as the sole source of booking truth.

## Publicly documented webhook contract

| Topic | LiteAPI public documentation | Resulting decision |
| --- | --- | --- |
| Transport | The registered endpoint must be publicly reachable over HTTPS. LiteAPI sends an HTTP POST request. | Expose a dedicated HTTPS ingress endpoint. Do not share a general application endpoint. |
| Acknowledgement | A `2xx` response acknowledges delivery and prevents continued retries under the configured retry policy. | Return `2xx` only after the raw authenticated event has been durably committed. Do not wait for supplier retrieval or downstream processing. |
| Authentication | An optional shared token is sent as the value of the `authorization` header. LiteAPI strongly recommends it for production. | Authentication is mandatory for every non-local environment. Reject missing or invalid tokens before persistence. |
| Authentication strength | No public HMAC signature, signed timestamp, nonce or source-IP allow-list is documented. | Treat the token as a static bearer-like shared secret. Do not treat a valid webhook payload as authoritative booking state. |
| Custom headers | Static custom headers can be attached to every event. LiteAPI suggests environment identifiers, correlation IDs and routing values as examples. | Custom headers may support routing, but must not replace authentication or the payload's `sandbox` check. |
| Retry | LiteAPI automatically retries when the endpoint does not return a successful response. The endpoint configuration includes maximum retries and an initial wait. Later retries use exponential backoff. | Design for transient redelivery. Record the actual account retry configuration separately for sandbox and production. |
| Retry defaults and limits | Public documentation does not state the default values, maximum values or total retry duration. | Do not base recovery objectives on undocumented defaults. Capture dashboard configuration and test actual behaviour. |
| Delivery semantics | LiteAPI explicitly says webhook delivery should be treated as at least once. | Duplicate delivery is expected, not exceptional. |
| Deduplication | Each event includes an `event_id`, described as a unique identifier for the webhook event delivery and intended for deduplication. | Enforce a unique inbox constraint for provider, environment and `event_id`. |
| Ordering | No delivery-order guarantee is documented. | Treat all events as unordered. Never reject a newer supplier state because an older webhook arrives later. |
| Payload structure | The top-level envelope contains `event_id`, `event_name`, `request`, `response` and `sandbox`. The `request` and `response` values are stringified JSON. | Persist the original body before parsing. Parse the envelope first, then parse the nested JSON strings according to `event_name`. |
| Environment identity | The payload contains `sandbox: true` for sandbox and `sandbox: false` for production. LiteAPI also notes that the project/environment must match the source being tested. | Use separate endpoints and secrets where practical, and reject an event whose `sandbox` value does not match the configured endpoint environment. |
| Endpoint management | Dashboard users can register, edit, enable or disable endpoints and update event subscriptions and retry behaviour. | Treat dashboard access as privileged production configuration. Audit changes outside LiteAPI where LiteAPI audit evidence is unavailable. |
| Event selection | Subscriptions are configured per endpoint in the dashboard. Flight events are available only where flight booking is enabled on the account. | The dashboard's selectable events are the account-level event catalogue and must be captured as production evidence. |
| Sandbox behaviour | LiteAPI states that some hotel events, including cancellation, only work in production because sandbox bookings do not complete downstream steps such as refund processing. | Sandbox tests do not prove production cancellation, refund or downstream event delivery. |
| Retention | No event-delivery retention duration is publicly documented. | readytogo.travel must retain its own raw accepted events and cannot rely on LiteAPI as an event archive. |
| Replay | No dashboard or API replay mechanism is publicly documented. | readytogo.travel must support internal reprocessing of retained inbox records. Missing events are recovered through reconciliation, not assumed vendor replay. |
| Availability | LiteAPI publishes a 99.95% monthly production API uptime commitment for informational purposes, excluding sandbox and third-party outages. The page does not state that webhook delivery is included. | Do not infer a webhook-specific SLA from the API uptime statement. Webhook outage and delay must be tolerated by reconciliation. |

## Public event catalogue

The following event names are documented publicly. The production dashboard remains the authoritative account-specific list of events that can actually be selected.

### Hotel booking lifecycle

| Event | Documented meaning | Recommended use |
| --- | --- | --- |
| `booking.prebook` | A room is selected and a prebook request is performed. LiteAPI describes this as generally too noisy for production. | Optional diagnostics and funnel monitoring. Do not make it a required production subscription. |
| `booking.book` | A hotel booking is successfully confirmed. | Enqueue retrieval and reconciliation; trigger confirmation workflow only after authoritative retrieval succeeds. |
| `booking.cancel` | A user cancels the booking. LiteAPI notes this may be production-only. | Enqueue retrieval and cancellation reconciliation. |
| `booking.book.hotelConfirmationNumber` | The hotel confirmation number is added or updated. | Reconcile and update the immutable booking version if changed. |
| `booking.checkinInstruction` | Hotel check-in instructions become available or are updated. | Reconcile and notify only after validation against current booking data. |

### Hotel booking errors

| Event | Documented meaning | Recommended use |
| --- | --- | --- |
| `booking.prebook_error` | Prebook failed because of availability, validation or supplier error. | Operational telemetry and checkout recovery. |
| `booking.book_error` | Booking failed because of payment, validation or supplier rejection. | Alert, reconcile payment/booking state and start recovery. |
| `booking.cancel_error` | Cancellation failed or was rejected. | Alert Operations and retain the prior confirmed booking state until retrieval proves otherwise. |

LiteAPI states that there are no separate `_error` events for amendments, rebooks, refunds, compensation or hotel confirmation-number updates in the published event catalogue.

### Hotel amendment and management events

| Event | Documented meaning | Recommended use |
| --- | --- | --- |
| `booking.rebook.rfn` | A refundable room is rebooked. | Enqueue complete booking retrieval and reconciliation. |
| `booking.rebook.nrfn` | A non-refundable room is rebooked. | Enqueue complete booking retrieval and reconciliation; raise an operational review where costs changed. |
| `booking.amendment` | Booking information, such as guest name or special requests, is changed. | Enqueue retrieval; do not mutate from the webhook body alone. |
| `booking.amendment.relocation` | The guest is relocated because of unforeseen circumstances. | High-priority Operations case plus retrieval and customer-contact workflow. |
| `booking.refund` | A refund is issued. | Reconcile booking and financial records; do not treat the webhook as settlement evidence. |
| `booking.compensation` | Compensation is provided, commonly following relocation or a room issue. | Reconcile commercial records and open an Operations review where needed. |

### Flight lifecycle events

Flight events are documented as available where flight booking is enabled on the account.

| Event | Documented meaning | Recommended use |
| --- | --- | --- |
| `flight.prebook` | Flight prebook session created or updated. | Optional flow telemetry and pricing/availability correlation. |
| `flight.attachServices` | Seats, baggage or other ancillary services attached. | Enqueue retrieval of the current prebook or booking. |
| `flight.book.created` | Initial flight booking record created after booking begins. | Start pending-booking tracking. |
| `flight.book.pending.confirmation` | The supplier or airline has not yet confirmed the booking. | Enqueue immediate retrieval and continue pending-confirmation polling. |
| `flight.book.confirmed` | Supplier confirms the flight booking. | Enqueue authoritative retrieval and confirmation processing. |
| `flight.book.cancelled` | Flight booking cancelled successfully. | Enqueue retrieval and cancellation/refund reconciliation. |
| `flight.book.failed` | Booking could not complete because of supplier rejection or another failure. | Alert and reconcile booking/payment state. |
| `flight.book.expired` | The offer or session expired before completion. | Expire the local workflow and reconcile any payment authorisation. |

The public flight catalogue does not identify dedicated events for:

- schedule or itinerary change;
- airline disruption;
- exchange;
- voluntary change;
- involuntary change;
- flight refund;
- ticket issuance;
- ticket void;
- re-accommodation; or
- ancillary cancellation/refund.

These gaps remain covered by retrieval, the OI-0004 manual-servicing boundary and ADR-0008 scheduled flight reconciliation.

## Authentication and security decision

### Approved minimum

Production webhooks must use all of the following:

- HTTPS;
- a dedicated webhook URL;
- the LiteAPI authentication token;
- a high-entropy secret stored in the environment's secret manager;
- a constant-time token comparison;
- separate sandbox and production secrets;
- environment validation using the payload's `sandbox` value;
- request-body size limits;
- content-type validation;
- structured security logging without secrets or unrestricted personal data;
- a durable inbox write before acknowledgement; and
- rate limiting and abuse monitoring that still permits legitimate retry bursts.

### Security limitation

The publicly documented token is a shared static value in the `authorization` header. No request-body signature or timestamp is documented.

Consequences:

- token possession is sufficient to imitate LiteAPI requests;
- the raw payload cannot be trusted as proof that the supplier's current booking state matches the payload;
- transport termination must be controlled;
- the token must never be logged;
- secret rotation must be supported;
- replayed events must be harmless; and
- every state-changing workflow must retrieve the current supplier booking before authoritative mutation.

### Secret rotation

Because LiteAPI documents one authentication token field per webhook endpoint but does not describe dual-token rotation, use one of these approaches:

1. register a replacement endpoint with a new secret, validate delivery, then disable the previous endpoint; or
2. temporarily accept both the old and new secret if the dashboard permits updating the existing endpoint without a delivery gap.

The chosen procedure must be tested in sandbox before production rotation.

## Delivery, ordering and duplicate decision

### Delivery model

Treat delivery as:

- at least once;
- potentially duplicated;
- potentially delayed;
- potentially missing after retries are exhausted; and
- not guaranteed to be ordered.

### Event identity

Use the compound identity:

`LiteAPI + environment + event_id`

Do not use `event_name`, booking ID or payload hash alone as the deduplication key.

### Processing rule

A webhook is a trigger to retrieve current supplier state.

It is not:

- an ordered event-sourced history;
- a guaranteed complete history;
- authoritative evidence of financial settlement;
- proof of current airline operational status; or
- a replacement for scheduled reconciliation.

### Acknowledgement rule

Return `2xx` only after:

1. HTTPS and request limits have been applied;
2. the authentication token is valid;
3. the top-level envelope is parseable;
4. the environment is valid;
5. the raw event is inserted into the durable inbox, or an existing duplicate is confirmed; and
6. the inbox transaction commits.

Do not wait for:

- nested payload parsing;
- supplier API retrieval;
- domain mutation;
- notifications;
- payment reconciliation; or
- downstream integrations.

Failures before durable persistence should return a non-`2xx` response so LiteAPI can retry.

Failures after persistence must be handled internally without asking LiteAPI to redeliver the same accepted event.

## Durable inbox requirements

The inbox record should contain at least:

| Field | Requirement |
| --- | --- |
| Provider | `LiteAPI` |
| Environment | Derived from endpoint configuration and validated against `sandbox` |
| Event ID | Original `event_id`; unique with provider and environment |
| Event name | Original `event_name` |
| Received timestamp | Platform UTC timestamp |
| Raw body | Original request body, encrypted or otherwise protected under data-classification rules |
| Request headers | Allow-listed headers only; authentication token must be removed |
| Payload hash | Hash of the exact raw body for integrity and diagnostics |
| Parse status | Envelope and nested-payload parse outcomes |
| Processing status | Pending, processing, completed, retrying, failed or quarantined |
| Attempt count | Internal processing attempts |
| Next attempt | Internal retry schedule |
| Correlation identifiers | Extracted booking, prebook, client-reference or provider identifiers where available |
| Supplier retrieval result | Link to the reconciliation execution and resulting version |
| Error record | Sanitised exception category and diagnostics |
| Completed timestamp | UTC timestamp after successful processing |

### Retention

Because LiteAPI does not publicly document event retention or replay, readytogo.travel must retain accepted webhook evidence according to its booking, audit, privacy and financial-record policies.

The deduplication key must be retained long enough to prevent an old captured event from being processed again as new. At minimum, retain the event identity for the lifetime of the related booking plus the applicable operational/audit retention period.

## Internal retry and replay

LiteAPI retries transport delivery before acknowledgement. readytogo.travel owns all retries after acknowledgement.

Internal retries must:

- use bounded exponential backoff with jitter;
- distinguish transient supplier/API errors from permanent schema or business-rule failures;
- stop automatic retries for poison messages and move them to quarantine;
- alert when retry age or count exceeds operational thresholds; and
- preserve the same inbox record rather than creating artificial webhook duplicates.

Internal replay means reprocessing the retained inbox event or re-running supplier reconciliation. It must not assume LiteAPI can resend an event.

## Environment differences

| Area | Sandbox | Production |
| --- | --- | --- |
| Payload marker | `sandbox: true` | `sandbox: false` |
| Endpoint | Dedicated sandbox endpoint recommended | Dedicated production endpoint required |
| Authentication secret | Separate sandbox secret | Separate production secret |
| Event catalogue | May include events that can be selected but cannot complete meaningful downstream processing | Account-enabled production catalogue |
| Hotel cancellation | LiteAPI states some cancellation behaviour only works in production because sandbox does not perform required downstream refund processing | Must be tested with controlled production evidence |
| Flight events | May depend on sandbox flight enablement and simulated provider behaviour | Available only where flight booking is enabled on the production account |
| Reliability commitment | Public 99.95% API uptime commitment excludes sandbox | Public commitment covers production APIs, but does not explicitly define webhook delivery as in scope |
| Test interpretation | Proves receiver integration and payload handling | Required to prove account subscriptions and actual downstream delivery |

No sandbox result should be used as proof of production cancellation, refund, amendment, schedule-change or flight event coverage.

## Reconciliation fallback

Missing or unsupported webhook coverage is handled by retrieval and scheduled reconciliation.

### Hotels

Hotel bookings must be reconciled:

- after confirmation;
- after any webhook that refers to the booking;
- before customer-visible servicing outcomes are finalised;
- when Operations receives a supplier/customer report;
- after cancellation, amendment, refund, relocation or compensation activity; and
- according to the platform's operational reconciliation schedule.

### Flights

Flight bookings follow ADR-0008:

- daily reconciliation while active;
- hourly reconciliation during the final 24 hours before each affected segment;
- immediate reconciliation when a relevant flight webhook is received;
- immediate reconciliation when a traveller, airline or Operations report indicates a possible change; and
- immutable itinerary versioning and customer notification for meaningful differences.

A webhook outage, unsupported event or exhausted LiteAPI retry sequence must not prevent eventual state convergence.

## Account-level evidence required before production reliance

The following evidence must be attached to OI-0005 or an approved supplier profile.

### Dashboard configuration

- production webhook endpoint URL, with secrets redacted;
- endpoint enabled status;
- production authentication token configured;
- maximum retry count;
- initial retry wait;
- selected event subscriptions;
- custom headers, if any;
- separate sandbox configuration; and
- screenshot or export date.

### Controlled delivery tests

For each required production event:

- test date and environment;
- action that triggered the event;
- event name received;
- event ID;
- delivery timestamp;
- HTTP acknowledgement result;
- duplicate/retry observation where deliberately tested;
- booking or prebook correlation;
- supplier retrieval result; and
- sanitised retained payload.

At minimum, test:

#### Hotels

- `booking.book`;
- `booking.cancel`;
- `booking.book_error` or another safely induced failure where feasible;
- `booking.amendment`;
- `booking.refund`; and
- any relocation, rebook or compensation workflow included in the customer promise.

#### Flights

Where flight booking is enabled:

- `flight.book.created`;
- `flight.book.pending.confirmation` where feasible;
- `flight.book.confirmed`;
- `flight.book.failed` or `flight.book.expired` where safely reproducible; and
- `flight.book.cancelled` where production servicing permits a controlled test.

### Authentication test

- missing token rejected;
- incorrect token rejected;
- correct token accepted;
- token excluded from logs;
- sandbox event rejected by the production endpoint;
- production event rejected by the sandbox endpoint; and
- rotation procedure tested.

### Retry and duplicate test

- receiver deliberately returns a non-`2xx` response;
- retry occurs;
- retry timing is recorded;
- duplicate `event_id` is observed or a duplicate request is safely simulated;
- only one domain reconciliation result is produced; and
- a duplicate receives `2xx` after the existing inbox record is confirmed.

### Failure and reconciliation test

- webhook processing is deliberately delayed after durable receipt;
- LiteAPI receives prompt `2xx`;
- background processing completes later;
- a missing event is simulated;
- scheduled reconciliation detects the supplier state; and
- customer/domain state converges without webhook delivery.

## Production reliance acceptance

Production webhook reliance may be approved when:

- [ ] The production event catalogue has been captured from the account dashboard.
- [ ] Required hotel and enabled-flight subscriptions have been selected.
- [ ] The production endpoint requires the LiteAPI authentication token.
- [ ] Security has approved the shared-secret limitations and compensating controls.
- [ ] Sandbox and production use separate endpoints or unambiguous routing and separate secrets.
- [ ] The durable inbox unique constraint and acknowledgement boundary have been tested.
- [ ] Duplicate delivery has been proven harmless.
- [ ] Ordering is explicitly treated as undefined.
- [ ] The actual retry configuration and observed retry behaviour are recorded.
- [ ] Internal retention and replay procedures are documented.
- [ ] Controlled production events have been received for the required customer workflows.
- [ ] Unsupported events have an explicit reconciliation or manual-support fallback.
- [ ] Operational alerts exist for authentication failures, unknown events, parsing failures, quarantined messages and stale reconciliation.
- [ ] No customer or domain state depends exclusively on webhook delivery.

## Recommended OI-0005 conclusion

The following text can be added to the parent issue:

> LiteAPI publicly documents HTTPS POST delivery, event subscriptions, an optional shared authentication token in the `authorization` header, configurable retry count and initial wait, exponential backoff, at-least-once delivery, possible duplicates, `event_id` deduplication, stringified request/response payloads and a sandbox/production marker. It does not publicly guarantee event ordering, retention, replay, a signed payload, default retry values, a webhook-specific SLA or complete flight servicing and schedule-change coverage. Option A remains selected: authenticated events are durably persisted, deduplicated and processed asynchronously as triggers for supplier retrieval. Scheduled reconciliation is the missing-event and unsupported-event safety net. Production webhook reliance is approved only after the account event subscriptions, authentication, retry settings and controlled production delivery tests are recorded.

## Decision impact

This addendum confirms that OI-0005 does not block:

- webhook ingress implementation;
- a durable inbox;
- event deduplication;
- asynchronous processing;
- supplier retrieval;
- immutable booking reconciliation;
- internal replay;
- scheduled fallback; or
- provider-neutral hotel and capability-gated flight implementation.

It continues to block:

- relying on an unverified account event subscription;
- using unauthenticated production webhooks;
- treating delivery as exactly once;
- assuming delivery order;
- assuming vendor retention or replay;
- using webhook payloads as authoritative booking state;
- removing scheduled reconciliation; and
- promising production event coverage that has not been tested.

## References

- [Using Nuitee Connect webhooks](https://docs.liteapi.travel/docs/using-liteapi-webhooks)
- [Performance, Reliability & Rate Limiting](https://docs.liteapi.travel/docs/performance-reliability-rate-limiting)
- [Service Availability & Uptime Commitment](https://docs.liteapi.travel/docs/service-availability-uptime-commitment)
- [Authentication & Access Control](https://docs.liteapi.travel/docs/authentication-access-control)

## Limitations

This addendum is based on public LiteAPI/Nuitee Connect documentation available on 28 July 2026 and the architecture direction already recorded in OI-0005.

The LiteAPI dashboard and production account configuration were not supplied for independent inspection. Statements that a guarantee is undocumented mean it was not found in the reviewed public material; they do not prove that LiteAPI cannot provide account-specific terms, dashboard controls or written commitments.

Any executed commercial agreement, account-specific support statement or later LiteAPI documentation takes precedence where it explicitly changes these findings.
