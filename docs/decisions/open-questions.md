<!-- markdownlint-disable MD013 -->

# Open Questions

The [Issue Index](../issues/index.md) is authoritative.

## Selected Directions Awaiting External Evidence

1. OI-0002: LiteAPI/provider-controlled customer payment is selected, but written merchant, settlement, refund, dispute, tax and consumer responsibilities remain required.
2. OI-0003: Qantas, Jetstar and Virgin Australia were observed in the developer portal, but production entitlement, fare completeness, booking, ticketing and servicing remain unverified.
3. OI-0004: durable daily and proximity flight reconciliation is accepted, but supplier retrieval freshness and servicing boundaries remain unverified.
4. OI-0005: webhook-first processing with scheduled reconciliation fallback is selected, but account event coverage and delivery guarantees remain unverified.
5. OI-0006: officially supported LiteAPI hosted/SDK payment components are selected, but platform support and qualified PCI scope remain unverified.
6. OI-0011: canonical history with selective protected raw evidence is selected, but LiteAPI terms, record classification, exact periods, backup expiry and the legal-hold procedure remain unverified.

## Open Decisions

No MVP decision remains open. OI-0010 remains open only as a non-blocking post-MVP wishlist item; OI-0002 through OI-0006 and OI-0011 have selected directions awaiting evidence.

## Deferred Wishlist

OI-0010 records live operational flight status as a post-MVP option. It is excluded from launch, directs customers to the airline for live operations and does not block MVP planning or implementation.

The first sellable scope, launch market/locale, sensitive traveller opt-in, Blazor frontend and ticket-support model are closed decisions. The only remaining ticket behaviour to settle during implementation planning is whether a customer response to a closed ticket reopens it or creates a linked follow-up ticket.

Secondary product, regulatory and implementation choices that do not yet justify separate open issues remain explicit work items in [PLAN-0001](../plans/active/PLAN-0001-project-planning-readiness.md). If one of those choices becomes a durable architecture decision or blocks progress independently, promote it to an ADR or OI rather than deciding it silently in implementation.
