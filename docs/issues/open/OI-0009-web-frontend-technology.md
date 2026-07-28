---
issue_id: OI-0009
title: Select the Web Frontend Technology
status: open
type: architecture-question
priority: p1
severity: medium
created: 2026-07-27
updated: 2026-07-27
decision_owners:
  - Architecture
  - Product
related_adrs:
  - ADR-0006
related_plans:
  - PLAN-0001
related_docs:
  - docs/applications/client-strategy.md
blocked_by:
  - OI-0001
---

# OI-0009 — Select the Web Frontend Technology

## Summary

Choose the web frontend after the MVP interaction and payment requirements are settled.

## Context

The backend direction is .NET, but the foundation does not choose Blazor, a JavaScript framework, or server-rendered pages. The web client must remain an API consumer and support secure payment integration.

## Options

### Option A — Blazor Web App

Use .NET end-to-end with interactive components where useful and clear API boundaries for later clients.

### Option B — React/Next.js or comparable TypeScript framework

Use a mature browser ecosystem and separate frontend deployment consuming the API.

### Option C — Razor Pages/MVC

Use server-rendered ASP.NET Core pages with progressive enhancement for a smaller initial surface.

## Recommendation

Prefer Option A if the team is strongest in .NET and the selected payment SDK works cleanly; otherwise Option C is the smallest operational footprint. Use Option B only when frontend skill, interaction complexity or ecosystem needs justify the additional toolchain.

## Evidence Required

- Team capability and maintenance ownership.
- Checkout SDK/browser requirements.
- SEO, accessibility, offline and interaction needs.
- Authentication/session design and API consumption boundary.
- Deployment and observability impact.

## Decision Impact

Controls client architecture, authentication integration, testing, deployment and developer workflow.

## Acceptance Criteria

- [ ] MVP UI requirements and payment constraints are known.
- [ ] A short proof validates authentication and payment integration.
- [ ] Hosting, testing and ownership are compared.
- [ ] Selected option is recorded in ADR-0006 or a dedicated ADR.

## Related Documents

- [Client Strategy](../../applications/client-strategy.md)
- [ADR-0006](../../adr/accepted/ADR-0006-application-and-deployment-baseline.md)
