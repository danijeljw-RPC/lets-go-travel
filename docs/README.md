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
| [Open issues](issues/README.md) | Questions requiring product, vendor, commercial, legal or technical answers. |
| [Plans](plans/README.md) | Dependency-ordered documentation and delivery planning. |
| [Decisions](decisions/README.md) | Current assumptions, constraints and question summaries. |
| [Domain](domain/README.md) | Trips, travellers, bookings, lifecycle and version history. |
| [Integrations](integrations/README.md) | Supplier boundaries and LiteAPI/Nuitee Connect evidence. |
| [API](api/README.md) | Public application API principles and client contract. |
| [Security](security/README.md) | Identity, privacy, payment scope and data protection. |
| [Operations](operations/README.md) | Reliability, reconciliation, observability and recovery. |
| [Applications](applications/README.md) | Web and future mobile client responsibilities. |

## Decision State

Documentation may describe a recommendation without making it binding. Only accepted ADRs are decisions. All seven initial ADRs are accepted; unresolved vendor, commercial, compliance and product questions remain tracked as open issues with options, recommendations and evidence gates.

## Source Material

The Markdown files in the repository root are the preserved discovery pack. Keep them until this structured documentation is reviewed and the active foundation plan is complete.

## Contribution Rules

Follow [Document Control](document-control.md). Use relative links for repository Markdown, keep one H1 per file, write prose paragraphs on one physical line, and update relevant indexes whenever an ADR, issue or plan is added or changes state.
