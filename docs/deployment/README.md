<!-- markdownlint-disable MD013 -->

# Deployment Documentation

Deployment records keep environment startup, migrations, configuration and release controls separate from product and architecture decisions.

- [Local Consumer Foundation](local-development.md) — pinned PostgreSQL and Keycloak dependencies, explicit migration execution and deterministic API/web startup.

Production remains governed by accepted [ADR-0010](../adr/accepted/ADR-0010-mvp-runtime-storage-and-delivery-baseline.md), the [Remaining Review Register](../decisions/review-register.md) and future environment-specific runbooks. Local development settings are never a production realm or secret template.
