# Architecture Decision Records

ADRs record durable project decisions. New ADRs start under `proposed/` and are not binding until explicitly accepted.

## Lifecycle

```text
proposed -> accepted
proposed -> rejected
accepted -> superseded
```

Accepted ADRs are not rewritten to change their meaning. A changed decision requires a new superseding ADR. Update [ADR Index](index.md) whenever an ADR is created, moved or changes status.

## Identifiers and Files

Use the next unused four-digit identifier and the filename format `ADR-####-short-kebab-title.md`. Never reuse or renumber identifiers.

## Required Content

Use the [ADR Template](templates/adr-template.md). Every ADR must explain context, options, recommendation or decision, consequences, risks, dependencies, decision owners and related records.
