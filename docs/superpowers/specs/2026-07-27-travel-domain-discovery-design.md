<!-- markdownlint-disable MD013 -->

# `.travel` Domain Discovery Tool Design

## Purpose

Create a local Zsh tool that generates short, memorable, energetic invitation-style `.travel` domain names and identifies names that might be available according to the official registry RDAP service.

The tool is a discovery aid. It must not claim that an unregistered name is purchasable because registrars may still classify a name as reserved, restricted, or premium-priced.

## Naming Direction

The candidates should sound like short invitations or basic sentences when read with the `.travel` suffix. The dominant tone is energetic and consumer-friendly.

Representative forms include:

- `letsgo.travel`
- `comeexplore.travel`
- `gonow.travel`
- `readysetgo.travel`
- `get-away.travel`

Both joined and hyphenated labels are allowed. Joined forms rank ahead of otherwise equivalent hyphenated forms because they are easier to say and type.

## Candidate Generation

The generator uses curated phrase templates rather than unrestricted dictionary combinations. Templates combine deliberately selected words into patterns such as:

- invitation plus action;
- movement plus direction;
- readiness plus action;
- action plus time;
- action plus companion;
- short hand-curated phrases that do not fit a general template.

Each phrase may generate a joined form and, where natural, a hyphenated form. The generator normalises candidates to lowercase ASCII domain labels, removes duplicates, and rejects labels that:

- violate DNS label syntax;
- start or end with a hyphen;
- contain consecutive hyphens;
- exceed 20 characters before the `.travel` suffix;
- are obviously awkward or semantically unsuitable according to the curated exclusion list.

The initial grammar should yield hundreds of useful candidates. Coverage should come from adding good words and templates, not by producing thousands of low-quality random combinations.

Names of 14 characters or fewer receive the strongest length preference. Names from 15 through 20 characters remain eligible when they form an especially natural phrase.

## Ranking

Candidates receive a deterministic score before registry lookup. Ranking favours:

1. natural invitation-like phrasing;
2. shorter labels;
3. fewer words;
4. joined spelling over hyphenation;
5. familiar words that are easy to pronounce and spell;
6. hand-curated phrases over mechanically generated equivalents.

Alphabetical order acts as the final tie-breaker. The same input must always produce the same ranking.

## Command-Line Interface

The primary executable is `scripts/find-travel-domains.zsh`.

The default command generates candidates and checks them against RDAP. Supported options are:

- `--generate-only`: generate and rank candidates without network requests;
- `--limit N`: check only the first `N` ranked candidates;
- `--delay SECONDS`: set the delay between registry requests;
- `--resume`: skip domains already present in the results file;
- `--input FILE`: check newline-delimited user-supplied labels or `.travel` domains instead of generated candidates;
- `--output-dir DIR`: select the result directory;
- `--help`: show usage and classification guidance.

Invalid arguments, invalid numeric values, unreadable input files, and unwritable output locations produce a clear message and a non-zero exit status.

## RDAP Lookup

The checker queries the official `.travel` registry endpoint:

`https://rdap.identitydigital.services/rdap/domain/<domain>.travel`

Requests are sequential and use a conservative configurable delay. The default should favour registry courtesy and reliable completion over maximum throughput.

Each response is classified from its HTTP result:

| Result | Classification | Meaning |
| --- | --- | --- |
| `200` | `REGISTERED` | The registry has a domain record. |
| `404` | `MIGHT_BE_AVAILABLE` | No registry record was found at lookup time. |
| `429` | `UNKNOWN` | The registry rate-limited the request. |
| Other HTTP status | `UNKNOWN` | The response does not establish registration state. |
| Network or timeout failure | `UNKNOWN` | Registration state was not established. |

The tool must not reinterpret `UNKNOWN` as available. It may record diagnostic detail, but terminal output and saved reports must avoid exposing unnecessary registry response content.

## Persistence and Outputs

The script creates its output directory when needed and persists each completed lookup immediately so an interrupted run retains progress.

It writes:

- `results.csv`: the complete machine-readable lookup ledger, including rank, domain, score, classification, HTTP status, and lookup timestamp;
- `might-be-available.txt`: domains classified `MIGHT_BE_AVAILABLE`, ordered by candidate rank;
- `unknown.txt`: domains that require retrying or manual checking;
- `candidates.txt`: the generated or normalised candidate set in ranked order.

`--resume` reads the existing ledger, skips any domain already classified as `REGISTERED` or `MIGHT_BE_AVAILABLE`, and retries prior `UNKNOWN` results. After every request, the script rewrites the canonical ledger through a temporary file and atomic rename, replacing any prior row for that domain. Repeated runs therefore do not create duplicate final entries.

CSV output must be safely quoted. Output files are refreshed into a consistent ranked view after lookups while retaining enough state to recover from interruption.

## User Feedback

During a live lookup, the script prints concise progress with the current position, domain, and classification. At completion it prints counts for registered, might-be-available, and unknown names plus the paths to the result files.

Every availability report includes this warning:

> `MIGHT_BE_AVAILABLE` means RDAP returned no registration record. Confirm availability, eligibility, premium pricing, and purchase terms with an accredited registrar.

## Components

The implementation should keep distinct shell functions for:

- argument parsing and validation;
- curated candidate construction;
- normalisation and syntax validation;
- deterministic scoring and ranking;
- RDAP request execution;
- HTTP-result classification;
- persistent ledger updates;
- report generation;
- summary output.

These boundaries allow candidate quality, networking, and persistence behavior to be tested independently.

## Testing Strategy

Tests must run without contacting the live registry. A test-controlled `curl` executable placed earlier on `PATH` will return predetermined HTTP statuses and failures.

Automated coverage includes:

- joined and hyphenated generation;
- invalid-label rejection and duplicate removal;
- deterministic ranking and joined-form preference;
- `200`, `404`, `429`, unexpected status, timeout, and network-failure classification;
- input-file normalisation;
- limit handling;
- immediate ledger persistence;
- resume behavior, including retrying `UNKNOWN` without duplicating final rows;
- correct report ordering and summary counts;
- invalid option and filesystem error handling;
- proof that `--generate-only` performs no network request.

A separate opt-in smoke command may check one known registered domain and one deliberately improbable candidate against the live service. It must not be part of the default automated test suite, and its result must not be treated as a guarantee of purchasability.

## Scope Boundaries

This work does not:

- register or purchase a domain;
- call a registrar checkout or pricing API;
- guarantee availability;
- bypass RDAP rate limits;
- perform parallel or high-volume registry requests;
- assess trademarks or legal suitability;
- choose the final product brand.

The user will review the ranked `MIGHT_BE_AVAILABLE` list, select preferred names, and confirm each finalist with a registrar and appropriate brand checks.
