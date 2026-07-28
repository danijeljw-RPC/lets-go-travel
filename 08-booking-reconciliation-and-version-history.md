# Booking Reconciliation and Version History

## Objective

The platform must be able to determine whether an externally managed booking has changed and respond appropriately.

Reconciliation means:

1. Retrieve the latest available supplier booking representation.
2. Map it into the platform's canonical booking model.
3. Produce a deterministic canonical snapshot.
4. calculate a hash of that snapshot;
5. compare it with the latest stored version;
6. record a new immutable version only when meaningful state changed;
7. compute structured change metadata and a detailed difference;
8. update the current normalised booking state;
9. initiate relevant notifications or support workflows.

## Critical dependency

This design can detect only changes exposed by the supplier.

If LiteAPI does not update its retrievable booking after an airline schedule change, reconciliation against LiteAPI cannot detect that change.

A separate operational flight-data provider may be required for:

- live delay;
- gate;
- terminal;
- aircraft;
- cancellation;
- diversion;
- actual departure and arrival.

Booking reconciliation and operational flight tracking should remain separate concepts.

## Current state plus immutable history

Use two complementary representations.

### Current operational state

Normalised relational tables represent the latest platform state and support:

- customer screens;
- support screens;
- search across current bookings;
- upcoming-trip queries;
- notification decisions;
- cancellation workflows.

### Immutable version history

Each changed version contains:

- booking ID;
- version number;
- observed timestamp;
- effective timestamp when known;
- source;
- canonical snapshot;
- canonical hash;
- change metadata;
- change flags;
- `DiffJson`;
- optional human-readable summary;
- raw supplier payload or protected reference, where permitted;
- supplier request and correlation identifiers.

The historical record should be append-only under normal operation.

## Canonical snapshot

The canonical snapshot should represent meaningful booking state, not incidental response formatting.

Canonicalisation should address:

- stable property order;
- stable collection ordering where order is not semantically meaningful;
- normalised date-time and timezone representation;
- null versus absent fields;
- whitespace;
- number formatting;
- currency precision;
- supplier-generated timestamps that should not trigger a version;
- volatile request metadata;
- stable traveller and segment identity.

Without canonicalisation, harmless response differences can produce false changes.

## Hash purpose

The hash is a change detector and integrity aid.

It is not:

- a replacement for the snapshot;
- proof that the supplier originated the data;
- an encryption mechanism;
- a complete legal signature;
- enough to explain what changed.

The selected algorithm and canonicalisation version should be recorded.

## Metadata and flags

Structured metadata allows efficient querying without repeatedly parsing every snapshot.

Potential metadata includes:

- previous version number;
- current version number;
- change classification;
- affected segment count;
- affected traveller count;
- severity;
- customer-notification requirement;
- support-review requirement;
- automatic-action eligibility.

Potential flags include:

- schedule changed;
- departure changed;
- arrival changed;
- airport changed;
- terminal changed;
- flight number changed;
- operating carrier changed;
- seat changed;
- baggage changed;
- traveller changed;
- accommodation dates changed;
- room changed;
- price changed;
- cancellation policy changed;
- status changed;
- cancelled;
- refund changed.

Flags are query aids, not the source of truth.

New flags may be added as real support and reporting needs are discovered.

## DiffJson

`DiffJson` should record the structured difference between the previous canonical snapshot and the new one.

It should identify:

- changed object;
- stable object identifier;
- field or semantic property;
- old value;
- new value;
- change type;
- relevant timezone and currency context.

It should support:

- customer notifications;
- support investigation;
- audit;
- analytics projection;
- later reporting;
- troubleshooting.

It should not expose sensitive values to consumers by default.

## Human-readable summary

A generated summary can support customer and support experiences.

Examples of summary intent:

- flight departure moved later;
- arrival airport changed;
- seat assignment removed;
- hotel check-in date changed;
- booking cancelled by supplier.

The summary should be generated from trusted structured differences. It should not be the only historical evidence.

## Change severity

Not every changed field should notify the customer.

A classification model may include:

- informational;
- minor;
- material;
- critical;
- support required.

Examples of material change criteria require product decisions, including:

- time shift threshold;
- airport change;
- connection duration risk;
- cabin downgrade;
- cancellation;
- traveller removal;
- hotel relocation;
- increased amount;
- cancellation-policy worsening.

## Reconciliation triggers

Potential triggers include:

- relevant supplier webhook;
- booking creation completion;
- customer opens booking;
- scheduled background check;
- approaching departure or check-in;
- support request;
- failed or pending booking recovery;
- cancellation or refund workflow.

Polling frequency should be based on supplier terms, quotas, booking proximity, event coverage, and customer value.

## Concurrency

Reconciliation must prevent conflicting updates.

The design should account for:

- two workers reconciling the same booking;
- webhook and scheduled job racing;
- customer cancellation during reconciliation;
- stale supplier response;
- out-of-order events;
- retry after partial database update.

A transaction and concurrency-control strategy is required.

## Failure handling

A failed reconciliation should record:

- attempt timestamp;
- supplier;
- failure category;
- retry eligibility;
- next retry;
- correlation ID;
- current known booking state;
- whether customer or support action is required.

Failure to reconcile must not silently imply that the booking is unchanged.

## Retention

Version and raw-payload retention should consider:

- dispute handling;
- support value;
- privacy;
- storage cost;
- supplier licensing;
- legal retention;
- deletion requests;
- encryption;
- backup retention.

Canonical versions may have a different retention policy from raw supplier payloads.
