# Booking Reconciliation and Version History

## Purpose

Reconciliation retrieves the latest booking representation exposed by a supplier, maps it to the platform model, detects meaningful changes, updates current operational state and records immutable history.

## Current Normalised State

Relational current-state records support customer views, upcoming trips, support, notifications, cancellation eligibility and operational queries. They are not the history record.

## Immutable Versions

Create a new append-only version only when the deterministic canonical hash changes. Each version should retain booking ID, version number, observation time, effective time if known, source, canonicalisation version, canonical snapshot, hash, structured metadata, flags, `DiffJson`, correlation identifiers, and protected raw-payload evidence where retention is permitted.

## Canonicalisation

Canonicalisation must define property order, collection order, stable traveller/segment identity, null versus absent values, whitespace, number/currency representation, date/time/timezone handling, volatile supplier fields and supplier timestamps that should not create versions.

## Hash

The hash detects canonical changes and supports integrity checking. It does not replace the snapshot, authenticate the supplier, encrypt data or explain the change.

## Metadata, Flags and DiffJson

Metadata supports efficient classification by severity, affected travellers/segments, notification need, support need and automatic-action eligibility. Flags identify useful categories such as schedule, airport, status, room, price, policy, cancellation or refund change. `DiffJson` records stable object identity, semantic field, old/new values, change type and timezone/currency context.

## Trigger and Concurrency

Triggers may include webhooks, booking completion, scheduled checks, customer/support access and pending-operation recovery. A transaction and concurrency strategy must prevent webhook, worker and customer actions from overwriting newer state.

## Critical Limit

Reconciliation detects only what the supplier exposes. It is separate from live operational flight status. See [ADR-0005](../adr/accepted/ADR-0005-reconciliation-and-operational-flight-status-boundary.md).
