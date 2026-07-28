# Initial Planning Roadmap

## Phase 1: Repository intake

- Review repository instructions and documentation templates.
- Identify existing ADR, issue, plan, security, architecture, and module conventions.
- Map this pack into those conventions.
- Preserve source files or archive them under a discovery area.
- Create a documentation work plan before rewriting everything.
- Record contradictions and missing decisions.

## Phase 2: Product boundary

Define:

- target customer;
- initial countries;
- initial product categories;
- MVP capabilities;
- explicit non-goals;
- trip model;
- support model;
- web-first release boundary;
- mobile release boundary;
- commercial assumptions.

Deliverables may include:

- product overview;
- scope document;
- glossary;
- capability map;
- open product questions.

## Phase 3: Supplier validation

Use LiteAPI sandbox and commercial discussions to validate:

- account and API access;
- hotels;
- flights;
- Australian airline coverage;
- payment;
- markup and commission;
- booking retrieval;
- cancellations;
- refunds;
- webhooks;
- post-booking servicing;
- schedule-change propagation;
- rate limits;
- data retention.

Every unverified point should remain an open issue.

## Phase 4: Foundational architecture decisions

Likely early decisions include:

- modular monolith;
- Keycloak identity boundary;
- Web API as public product boundary;
- PostgreSQL;
- supplier abstraction;
- trip and booking model;
- current state plus immutable booking versions;
- payment responsibility;
- idempotency;
- background processing;
- notification architecture;
- API compatibility;
- time and currency representation.

## Phase 5: Security and privacy design

Define:

- data classification;
- customer and traveller ownership;
- Keycloak flows;
- mobile token storage;
- support access;
- encryption;
- secret management;
- logging redaction;
- audit;
- passport-data policy;
- privacy lifecycle;
- PCI scope;
- incident response.

## Phase 6: Domain documentation

Document:

- customer;
- traveller;
- trip;
- accommodation;
- flight;
- search offer;
- prebook;
- booking;
- payment;
- cancellation;
- refund;
- notification;
- document;
- supplier mapping;
- reconciliation;
- version history.

## Phase 7: Workflow documentation

Describe end-to-end flows for:

- registration and login;
- hotel search and booking;
- flight search and booking;
- price change;
- payment challenge;
- payment succeeded but booking failed;
- pending booking;
- webhook processing;
- reconciliation;
- schedule change;
- cancellation;
- refund;
- support escalation;
- account deletion with active bookings.

## Phase 8: Delivery planning

Only after key decisions are settled:

- create implementation milestones;
- define test strategy;
- define environments;
- define CI/CD;
- create observability baseline;
- plan sandbox certification;
- plan production readiness;
- plan web launch;
- plan Android and iOS phases.

## Suggested dependency order

1. Product scope.
2. LiteAPI commercial and technical verification.
3. Identity and customer ownership.
4. Trip and booking domain.
5. Payment and merchant-of-record decision.
6. Supplier boundary.
7. Booking lifecycle and idempotency.
8. Reconciliation and versioning.
9. Security and privacy.
10. API contract and compatibility.
11. Web experience.
12. Mobile architecture.
13. Operational readiness.

## Planning completion test

The project is ready to enter implementation planning when it can answer:

- What exactly is the first sellable product?
- Which LiteAPI capabilities are contractually and technically available?
- Who is merchant of record?
- How does secure payment work on web and mobile?
- What customer and traveller data is stored?
- How is every booking state represented?
- How are duplicate financial and booking operations prevented?
- How are supplier changes detected?
- Which flight updates require another provider?
- What happens when payment and booking states disagree?
- How are customers notified?
- How can support investigate a disputed change?
- Which records are retained after account deletion?
- How will an old mobile app continue to work after API evolution?
