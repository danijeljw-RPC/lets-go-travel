---
issue_id: OI-0009
title: Select the Web Frontend Technology
status: closed
type: architecture-question
priority: p1
severity: medium
created: 2026-07-27
updated: 2026-07-28
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

<!-- markdownlint-disable MD013 MD025 -->

# OI-0009 — Select the Web Frontend Technology

## Summary

Use .NET 10 Blazor server-side rendering for the web client and ASP.NET Core Web API for the application boundary.

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

Option A was selected on 2026-07-28.

## Decision

The web application is a .NET 10 Blazor Web App using server-side rendering, with interactive server components only where the customer journey requires them. ASP.NET Core Web API remains the public application and supplier-neutral boundary for web and later clients under ADR-0002.

Required LiteAPI hosted payment JavaScript is isolated behind a narrow browser-interoperability boundary. It does not expose supplier credentials or move authoritative pricing, payment, booking or reconciliation behaviour into browser code.

## Evidence Required

- Team capability and maintenance ownership.
- Checkout SDK/browser requirements.
- SEO, accessibility, offline and interaction needs.
- Authentication/session design and API consumption boundary.
- Deployment and observability impact.

## Decision Impact

Controls client architecture, authentication integration, testing, deployment and developer workflow.

## Acceptance Criteria

- [x] MVP UI and payment boundaries are known at decision level.
- [x] Authentication and payment integration proof remains an implementation and OI-0006 evidence task.
- [x] Hosting and ownership use the accepted .NET container baseline.
- [x] Option A is recorded as the web selection related to ADR-0006.

## Related Documents

- [Client Strategy](../../applications/client-strategy.md)
- [ADR-0006](../../adr/accepted/ADR-0006-application-and-deployment-baseline.md)
