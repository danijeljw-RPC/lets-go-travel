<!-- markdownlint-disable MD013 -->

# Slice 3 Outcome — Search and Capability Registry

## Outcome

Slice 3 is implemented on branch `codex/slice-3-search`. It delivers supplier-neutral hotel and flight search contracts, minimum-total pricing, sanitized deterministic LiteAPI fixtures, explicit capability discovery, public API routes and a compact Blazor SSR search experience. It does not enable a live or production supplier.

The completed implementation follows the [Search and Capability Registry Implementation Plan](../superpowers/plans/2026-07-29-search-capability-registry.md) and advances [PLAN-0002](../plans/active/PLAN-0002-mvp-delivery.md) to Slice 4.

## Delivered Scope

- Added `ReadyToGoTravel.Search` as one internal feature-module assembly and no new deployable or database.
- Added validated hotel destination/date/occupancy and flight leg/passenger/cabin search requests.
- Added platform-owned hotel and flight offers with opaque `off_` identifiers; supplier references and payload models do not cross the API boundary.
- Added price records with minimum total, returned currency, requested currency, currency provenance, base amount, included taxes and included fees.
- Added offer expiry and an explicit revalidation requirement.
- Added capability records by provider, environment, point of sale, product, operation and carrier.
- Recorded Qantas (`QF`), Jetstar (`JQ`) and Virgin Australia (`VA`) as observed sandbox search carriers with `productionEnabled: false`.
- Added sanitized embedded hotel and flight fixtures with no credentials or traveller personal data.
- Added a public `/search` Blazor page that displays API-authoritative totals and sandbox limitations without a booking action.

## Public API

| Route | Outcome |
| --- | --- |
| `GET /api/v1/search/capabilities?pointOfSale=AU` | Returns the configured capability snapshot and evidence status. |
| `POST /api/v1/search/hotels` | Returns platform hotel offers from the enabled environment provider. |
| `POST /api/v1/search/flights` | Returns only offers whose carrier search capability is enabled. |

Validation errors use `ProblemDetails` with a stable `code` and `correlationId`. Search has a separate limit of 30 requests per minute per source. Unknown JSON members are rejected.

## Activation and Safety Outcome

Default application configuration uses `Search:Environment=Production` and `Search:EnableFixtures=false`. No production provider is registered, every production capability is disabled and a search request returns `503 search_capability_unavailable`.

Development configuration uses `Sandbox` with sanitized fixtures. The sandbox result label, expiry and revalidation requirement prevent the fixtures from being represented as live prices or production carrier capability.

The following remain unchanged and required before production activation:

- OI-0003 production entitlement plus dated verify, prebook, book, ticket, retrieve and servicing evidence for every enabled carrier;
- OI-0002 executed merchant, settlement, refund, dispute, chargeback and tax responsibilities;
- OI-0006 approved hosted-payment/PCI evidence; and
- OI-0005 webhook subscription, authentication and controlled delivery evidence for later reconciliation reliance.

## Test-first Evidence

The implementation used observed red-green cycles:

- domain tests first failed because price, request and capability types did not exist;
- adapter tests first failed because the LiteAPI fixture provider did not exist;
- in-process HTTP tests first failed because the search routes did not exist; and
- web client tests first failed because `SearchApiClient` did not exist.

The completed solution contains 52 tests: 17 Search, 15 Consumer, 7 Architecture, 7 Web, 5 API and 1 Building Blocks test.

## Pull-request CI

The GitHub Actions `CI` workflow now runs for every pull request whose base branch is `dev`. The workflow retains locked restore, formatting, a warning-as-error Release build, PostgreSQL migration verification, the full test suite, documentation validation and all four container builds. Push verification on `main` remains in place. A GitHub branch-protection rule or repository ruleset must separately require the `verify` job if failed or pending CI must technically prevent merging; this local branch does not mutate remote repository settings.

## Verification Record

The completion verification covers:

- locked solution restore;
- formatter verification;
- warning-as-error Release build;
- all 52 tests;
- Markdown structure and link validation;
- transitive vulnerable-package audit;
- API development/sandbox and production/fail-closed smoke tests; and
- API, web, general-worker and flight-reconciliation-worker container builds.

All commands completed successfully on 2026-07-29. The Release build reported zero warnings and zero errors; the test run reported zero failed or skipped tests.

## Commit Sequence

- `c740cb4` — plan the search and capability slice.
- `e1c94a4` — add supplier-neutral search contracts and capability policy.
- `79d7ad9` — map sanitized LiteAPI search fixtures.
- `31b2cb1` — expose the capability-gated search API.
- `b9097ab` — add the hotel and flight search shell.
- final completion commit — add PR-to-`dev` CI gating, documentation, verification record and this report.

The separate `.gitignore` change requested before Slice 3 was committed on `dev` as `ba40194` and is inherited by this branch.

## Explicit Exclusions

Slice 3 does not implement live LiteAPI calls, production credentials, offer persistence, offer verification/repricing, prebook, checkout, payment, booking, combined journeys, discounts, currency conversion, webhooks, reconciliation or support flows.

## Slice 4 Handoff

Slice 4 can consume the platform offer and capability contracts, but must add a server-owned checkout/offer-resolution boundary rather than trusting the browser's opaque offer ID. It must keep payment and booking states separate, require renewed customer acceptance after repricing, use the approved LiteAPI hosted/SDK payment route behind disabled production capabilities and represent combined journeys as separately evidenced hotel and flight bookings.
