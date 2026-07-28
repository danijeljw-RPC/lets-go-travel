<!-- markdownlint-disable MD013 -->

# MVP Operational Policy

## Status

Approved implementation baseline on 2026-07-29. Staffing contacts, supplier contract values and exercised production evidence remain launch gates.

## Customer Support

- Support is asynchronous ticket and email support only.
- The published initial service window is 08:00–20:00 Australia/Sydney, seven days a week, excluding any separately published outage window.
- Every accepted ticket update receives an immediate automated acknowledgement and reference.
- `Travel within 24 hours`, payment/booking mismatch, supplier cancellation/relocation and traveller-safety categories receive the urgent queue.
- During service hours, the internal acknowledgement target is 30 minutes for urgent tickets and four hours for ordinary tickets. These are platform targets, not supplier resolution promises.
- Outside service hours, urgent auto-replies direct travellers to the operating airline, property or emergency services when immediate operational action is required and state when platform support resumes.
- Supplier escalation contacts and staffing assignments must be populated before live bookings are accepted.

## Change Notification

Meaningful booking differences are classified as:

| Severity | Examples | Customer action |
| --- | --- | --- |
| Informational | confirmation reference added; non-material instruction change | In-app history; email only when useful. |
| Minor | time moves by less than 30 minutes without connection/check-in impact; non-critical wording change | Email and in-app record; no urgent escalation. |
| Material | time moves by 30 minutes or more; flight number, terminal, airport, room/rate inclusion or cancellation term changes | Immediate email, in-app alert and acknowledgement tracking. |
| Travel-blocking | cancellation, failed confirmation, missed-connection risk, property relocation, supplier action required within 24 hours | Immediate email, urgent support case and repeated operational escalation. |

Quiet hours are 22:00–07:00 in the customer's selected timezone. Informational and minor emails wait until 07:00. Material and travel-blocking notifications bypass quiet hours. The application records the old/new canonical versions, reason, severity, template/version, locale, attempts and final delivery state.

## Recovery Objectives

| Capability | RPO | RTO | Notes |
| --- | ---: | ---: | --- |
| Booking, payment-reference and support PostgreSQL data | 5 minutes | 4 hours | Point-in-time recovery plus supplier reconciliation before normal write traffic resumes. |
| Keycloak identity/configuration | 15 minutes | 4 hours | Restore configuration and sessions/credentials under the approved Keycloak backup design. |
| Ticket attachments and booking documents | 1 hour | 8 hours | Versioned private objects; metadata remains in PostgreSQL. |
| Search/reference caches | None | 24 hours | Rebuildable and never authoritative. |
| Notification delivery | 15 minutes | 4 hours | Durable outbox is replayed after recovery. |

Restore exercises run before production, at least twice yearly and after a material storage/topology change. Post-restore steps reapply retention tombstones, rotate exposed secrets if required, reconcile affected supplier bookings and verify idempotent outbox/inbox replay.

## Operational Evidence Still Required

- Named on-call/support assignments and escalation contacts.
- Controlled urgent-ticket and notification exercises.
- PostgreSQL, Keycloak and object restore evidence against the stated objectives.
- Retention expiry, legal-hold, deletion receipt and restored-backup tests.
- Supplier support contact and SLA evidence where available.
