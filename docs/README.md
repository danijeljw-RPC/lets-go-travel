<!-- markdownlint-disable MD013 -->

# readytogo.travel Documentation

This directory contains the planning and design documentation for `readytogo.travel`, a standalone consumer travel product focused on customer-owned trips and bookings. Use `RTGT` as the product shortcode and `ReadyToGoTravel` as the default root namespace or package prefix for all code; apply the language's normal casing conventions where a package ecosystem requires lowercase names.

## Product Direction

The accepted direction is a web-first consumer experience organised around trips and bookings. The technical foundation is a containerised .NET 10 modular monolith with an ASP.NET Core Web API, managed Azure PostgreSQL, containerised Keycloak, private background work, server-side supplier integrations and later mobile clients using the same platform API.

## Documentation Map

| Area | Purpose |
| --- | --- |
| [Product](product/README.md) | Product vision, MVP boundary, roadmap and terminology. |
| [Architecture](architecture/README.md) | System context, target shape and trust boundaries. |
| [ADRs](adr/README.md) | Proposed, accepted, rejected and superseded architecture decisions. |
| [Issues](issues/README.md) | Open, in-review and closed product, vendor, commercial, legal or technical questions. |
| [Plans](plans/README.md) | Dependency-ordered documentation and delivery planning. |
| [Decisions](decisions/README.md) | Current assumptions, constraints, question summaries and remaining review register. |
| [Evidence](evidence/README.md) | Dated supplier, regulatory and account evidence supporting decisions and production gates. |
| [Australian legal pack](australian-market-legal-pack/00-README.md) | Australia-first legal/compliance implementation baseline and launch approval checklist. |
| [Domain](domain/README.md) | Trips, travellers, bookings, lifecycle and version history. |
| [Integrations](integrations/README.md) | Supplier boundaries and LiteAPI/Nuitee Connect evidence. |
| [API](api/README.md) | Public application API principles and client contract. |
| [Security](security/README.md) | Identity, privacy, payment scope, retention, legal hold and data protection. |
| [Operations](operations/README.md) | Reliability, reconciliation, observability and recovery. |
| [Applications](applications/README.md) | Web and future mobile client responsibilities. |
| [Deployment](deployment/README.md) | Local runtime, migrations and later environment/release runbooks. |
| [Delivery](delivery/README.md) | Completed slice outcome reports, verification evidence and handoffs. |

## Decision State

Documentation may describe a recommendation without making it binding. Accepted ADRs and closed product issues record approved direction. Ten ADRs are accepted; seven issues are closed, four LiteAPI-dependent directions are in review for production evidence and OI-0010 is the only open issue because it remains a non-blocking post-MVP wishlist item.

## Source of Truth

The structured documentation under `docs/` contains the product's durable planning, decisions, open issues and delivery records. The original numbered discovery pack was consolidated into these canonical documents and removed; Git history preserves its provenance.

## Contribution Rules

Follow [Document Control](document-control.md). Use relative links for repository Markdown, keep one H1 per file, write prose paragraphs on one physical line, and update relevant indexes whenever an ADR, issue or plan is added or changes state.

Keep only the conventional project `README.md` at repository root. Put all other durable Markdown in the appropriate `docs/` category.
