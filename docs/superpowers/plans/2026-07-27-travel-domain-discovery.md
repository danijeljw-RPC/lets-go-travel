# `.travel` Domain Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a portable Zsh command that generates energetic invitation-style `.travel` names, checks them through the official RDAP endpoint, and produces resumable ranked reports.

**Architecture:** One executable Zsh script owns candidate generation, ranking, lookup classification, persistence, and report rendering through focused shell functions. A standalone Zsh test harness invokes the command as a black box and places a deterministic fake `curl` earlier on `PATH`, keeping automated tests entirely offline.

**Tech Stack:** Zsh 5.x, POSIX utilities available on macOS (`awk`, `sort`, `sed`, `mktemp`, `mv`, `date`), and `curl` for opt-in live requests.

## Global Constraints

- Generate lowercase ASCII `.travel` labels of at most 20 characters.
- Give the strongest length preference to labels of 14 characters or fewer.
- Generate joined and natural hyphenated forms, with joined forms ranked first when otherwise equivalent.
- Classify RDAP `200` as `REGISTERED`, `404` as `MIGHT_BE_AVAILABLE`, and every other result or request failure as `UNKNOWN`.
- Send live requests sequentially with a configurable delay; never bypass registry rate limits.
- Treat RDAP results as discovery evidence, not a guarantee of purchase availability or price.
- Persist every result through an atomic canonical-ledger rewrite and never duplicate a domain row.
- Automated tests must never contact the network.

---

## File Structure

- `scripts/find-travel-domains.zsh`: public CLI, curated candidate grammar, scoring, RDAP client, CSV persistence, reports, and summary.
- `tests/find-travel-domains-test.zsh`: dependency-free black-box test runner plus fake `curl` fixture.

### Task 1: Candidate Generation, Normalisation, and CLI Validation

**Files:**

- Create: `tests/find-travel-domains-test.zsh`
- Create: `scripts/find-travel-domains.zsh`

**Interfaces:**

- Consumes: command-line options documented in the approved design.
- Produces: executable `scripts/find-travel-domains.zsh`; ranked tab-separated internal candidate records with `rank`, `domain`, and `score`; public `candidates.txt` containing one full `.travel` domain per line.

- [ ] **Step 1: Write failing black-box tests for generation and validation**

Create a minimal test harness with `assert_eq`, `assert_contains`, and isolated `mktemp -d` work directories. Add tests that run:

```zsh
"$SCRIPT" --generate-only --output-dir "$case_dir/output"
"$SCRIPT" --generate-only --limit 12 --output-dir "$case_dir/limited"
"$SCRIPT" --generate-only --input "$case_dir/input.txt" --output-dir "$case_dir/input-output"
```

Assert that generation creates more than 200 unique candidates, includes `letsgo.travel` and `lets-go.travel`, contains no label longer than 20 characters, ranks `letsgo.travel` ahead of `lets-go.travel`, and makes no `curl` call. Assert that `--limit 12` writes exactly 12 candidates. Supply mixed-case labels, full `.travel` domains, duplicates, and invalid labels through `--input`; assert valid names are normalised and deduplicated. Assert invalid options and numeric values exit non-zero with a useful error.

- [ ] **Step 2: Run the test and verify RED**

Run:

```bash
zsh tests/find-travel-domains-test.zsh
```

Expected: FAIL because `scripts/find-travel-domains.zsh` does not exist.

- [ ] **Step 3: Implement candidate and argument functions**

Implement these focused functions in `scripts/find-travel-domains.zsh`:

```zsh
die()                         # print to stderr and exit non-zero
usage()                       # document every supported option and warning
parse_args "$@"              # populate GENERATE_ONLY, LIMIT, DELAY, RESUME, INPUT_FILE, OUTPUT_DIR
normalise_label "$raw"       # print a valid lowercase label or return non-zero
add_phrase "$phrase" "$weight" "$curated"
generate_candidates           # populate deterministic label=>score records
load_input_candidates "$file"
write_candidates "$records_file" "$OUTPUT_DIR/candidates.txt"
```

Use curated invitation families rather than unrestricted dictionaries. Include hand-picked phrases plus bounded combinations from these semantic groups:

```zsh
actions=(go explore escape wander roam discover adventure fly getaway)
times=(now today soon tonight again)
directions=(away beyond far anywhere somewhere there)
companions=(withme withus together)
openers=(come lets get ready pack dream)
```

Generate joined forms for every accepted phrase and hyphenated forms only at word boundaries. Reject non-ASCII labels, leading/trailing or consecutive hyphens, and labels longer than 20 characters. Score with an explicit deterministic formula based on phrase weight, curated bonus, length, word count, and hyphen penalty; use `LC_ALL=C sort` with alphabetical tie-breaking.

- [ ] **Step 4: Run Task 1 tests and verify GREEN**

Run:

```bash
zsh tests/find-travel-domains-test.zsh
```

Expected: all generation, normalisation, ordering, no-network, limit, and argument tests pass.

- [ ] **Step 5: Commit Task 1**

```bash
git add -- scripts/find-travel-domains.zsh tests/find-travel-domains-test.zsh
git commit -m "feat: generate ranked travel domain candidates"
```

### Task 2: RDAP Classification and Durable Reports

**Files:**

- Modify: `tests/find-travel-domains-test.zsh`
- Modify: `scripts/find-travel-domains.zsh`

**Interfaces:**

- Consumes: ranked candidate records from Task 1 and the official RDAP endpoint `https://rdap.identitydigital.services/rdap/domain/<domain>.travel`.
- Produces: `results.csv`, `might-be-available.txt`, `unknown.txt`, progress output, and summary counts.

- [ ] **Step 1: Add failing classification and persistence tests**

Have the test harness create an executable fake `curl` in a temporary `bin` directory. It should log the requested URL and return controlled statuses by domain:

```zsh
case "$url" in
  *registered.travel) print -n -- 200 ;;
  *open-name.travel)  print -n -- 404 ;;
  *limited.travel)    print -n -- 429 ;;
  *broken.travel)     exit 28 ;;
  *)                  print -n -- 503 ;;
esac
```

Run the real script with `PATH="$fake_bin:$PATH"`, `--delay 0`, and a five-name input file. Assert exact classifications for `200`, `404`, `429`, `503`, and exit `28`; safe CSV headers and quoted domain fields; one row per domain; ranked availability output; unknown output; correct terminal totals; and one sequential fake-curl call per candidate.

Seed `results.csv` with final and unknown rows, run with `--resume`, and assert final rows are skipped, unknown rows are retried, and every domain still appears exactly once. Add a test that kills the command after a fake curl call and confirms the completed row was already persisted.

- [ ] **Step 2: Run Task 2 tests and verify RED**

Run:

```bash
zsh tests/find-travel-domains-test.zsh
```

Expected: generation tests pass; new lookup/report tests fail because network and persistence functions are absent.

- [ ] **Step 3: Implement lookup, ledger, reports, and summary**

Implement:

```zsh
lookup_domain "$domain"       # print "<classification>\t<status>"
existing_classification "$domain" "$results_file"
upsert_result "$rank" "$domain" "$score" "$class" "$status" "$timestamp"
refresh_reports
print_summary
run_checks "$records_file"
```

Invoke `curl` with `--silent`, `--show-error`, `--output /dev/null`, `--write-out '%{http_code}'`, and a finite timeout. Capture its exit status without allowing `set -e` to abort. Map only `200` and `404` to final classifications.

Write the CSV header exactly as:

```text
rank,domain,score,classification,http_status,checked_at
```

Quote every string field. For each lookup, use a temporary file in the output directory to copy the header and all rows except the current domain, append the new row, sort data rows numerically by rank, and atomically `mv` it over `results.csv`. Refresh `might-be-available.txt` and `unknown.txt` after each durable update so an interrupt leaves useful reports. Under `--resume`, skip final classifications and retry `UNKNOWN`.

- [ ] **Step 4: Run Task 2 tests and verify GREEN**

Run:

```bash
zsh tests/find-travel-domains-test.zsh
```

Expected: all offline tests pass with zero real network access.

- [ ] **Step 5: Commit Task 2**

```bash
git add -- scripts/find-travel-domains.zsh tests/find-travel-domains-test.zsh
git commit -m "feat: check travel domains through RDAP"
```

### Task 3: User-Facing Verification and Live Smoke Check

**Files:**

- Modify: `tests/find-travel-domains-test.zsh`
- Modify: `scripts/find-travel-domains.zsh`

**Interfaces:**

- Consumes: complete CLI from Tasks 1 and 2.
- Produces: verified help text, executable file modes, and live-check evidence without baking changing domain results into tests.

- [ ] **Step 1: Add failing tests for help and final warning text**

Assert `--help` exits zero and names every option, RDAP classification, and the registrar-confirmation warning. Assert generated `might-be-available.txt` contains the warning followed by ranked domains. Assert the production script is executable.

- [ ] **Step 2: Run the tests and verify RED**

Run:

```bash
zsh tests/find-travel-domains-test.zsh
```

Expected: FAIL on any missing help copy, warning, or executable mode requirement.

- [ ] **Step 3: Complete help text and executable setup**

Make the script help concise and copy-pasteable, including:

```text
MIGHT_BE_AVAILABLE means RDAP returned no registration record.
Confirm availability, eligibility, premium pricing, and purchase terms with an accredited registrar.
```

Set executable modes on both Zsh files.

- [ ] **Step 4: Run the full offline verification**

Run:

```bash
zsh -n scripts/find-travel-domains.zsh
zsh -n tests/find-travel-domains-test.zsh
zsh tests/find-travel-domains-test.zsh
```

Expected: both syntax checks and the full test suite exit zero.

- [ ] **Step 5: Run a bounded live smoke check**

Create a temporary two-line input containing one known registered `.travel` name and one generated candidate, then run with `--delay 1` and an isolated temporary output directory. Verify the registered control returns `REGISTERED`; report the candidate's actual classification without treating `MIGHT_BE_AVAILABLE` as purchasability.

- [ ] **Step 6: Inspect final scope and commit**

Run:

```bash
git status --short
git diff --check
git diff -- scripts/find-travel-domains.zsh tests/find-travel-domains-test.zsh
```

Then commit only the final script/test changes that remain:

```bash
git add -- scripts/find-travel-domains.zsh tests/find-travel-domains-test.zsh
git commit -m "chore: finish travel domain finder"
```
