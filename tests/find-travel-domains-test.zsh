#!/usr/bin/env zsh

set -u
setopt pipefail
unsetopt BG_NICE

readonly TEST_DIR="${0:A:h}"
readonly REPO_DIR="${TEST_DIR:h}"
readonly SCRIPT="${REPO_DIR}/scripts/find-travel-domains.zsh"

typeset -gi TESTS_RUN=0
typeset -gi TESTS_FAILED=0
typeset TEST_ROOT

pass() {
  print -r -- "ok - $1"
}

fail() {
  print -ru2 -- "not ok - $1"
  TESTS_FAILED+=1
}

assert_true() {
  local name="$1"
  shift
  TESTS_RUN+=1
  if "$@"; then
    pass "$name"
  else
    fail "$name"
  fi
}

assert_eq() {
  local name="$1"
  local expected="$2"
  local actual="$3"
  TESTS_RUN+=1
  if [[ "$actual" == "$expected" ]]; then
    pass "$name"
  else
    print -ru2 -- "  expected: $expected"
    print -ru2 -- "  actual:   $actual"
    fail "$name"
  fi
}

assert_contains() {
  local name="$1"
  local needle="$2"
  local file="$3"
  TESTS_RUN+=1
  if grep -Fqx -- "$needle" "$file"; then
    pass "$name"
  else
    print -ru2 -- "  missing line: $needle"
    fail "$name"
  fi
}

assert_file_has() {
  local name="$1"
  local needle="$2"
  local file="$3"
  TESTS_RUN+=1
  if grep -Fq -- "$needle" "$file"; then
    pass "$name"
  else
    print -ru2 -- "  missing text: $needle"
    fail "$name"
  fi
}

finish() {
  if [[ -n "${TEST_ROOT:-}" && -d "$TEST_ROOT" ]]; then
    rm -rf -- "$TEST_ROOT"
  fi
  print -r -- "${TESTS_RUN} tests, ${TESTS_FAILED} failures"
}

trap finish EXIT

assert_true "production script exists" test -f "$SCRIPT"
if [[ ! -f "$SCRIPT" ]]; then
  exit 1
fi

TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/travel-domain-tests.XXXXXX")"
readonly TEST_ROOT

mkdir -p "$TEST_ROOT/fake-bin"
printf '%s\n' '#!/usr/bin/env zsh' 'print -ru2 -- "curl must not run in generate-only mode"' 'exit 99' > "$TEST_ROOT/fake-bin/curl"
chmod +x "$TEST_ROOT/fake-bin/curl"

typeset generation_output
if generation_output="$(PATH="$TEST_ROOT/fake-bin:$PATH" zsh "$SCRIPT" --generate-only --output-dir "$TEST_ROOT/generated" 2>&1)"; then
  pass "generate-only exits successfully"
  TESTS_RUN+=1
else
  print -ru2 -- "$generation_output"
  fail "generate-only exits successfully"
  TESTS_RUN+=1
fi

readonly CANDIDATES="$TEST_ROOT/generated/candidates.txt"
assert_true "candidate report exists" test -f "$CANDIDATES"

typeset candidate_count=0
if [[ -f "$CANDIDATES" ]]; then
  candidate_count="$(grep -Ec '^[a-z0-9-]+\.travel$' "$CANDIDATES")"
fi
assert_true "generation creates more than 200 candidates" test "$candidate_count" -gt 200
assert_contains "joined invitation is generated" "letsgo.travel" "$CANDIDATES"
assert_contains "hyphenated invitation is generated" "lets-go.travel" "$CANDIDATES"
assert_contains "companion invitation uses natural word order" "gowithme.travel" "$CANDIDATES"
assert_true "awkward companion word order is excluded" sh -c "! grep -Fqx 'gomewith.travel' '$CANDIDATES'"

typeset unique_count="$(sort -u "$CANDIDATES" | wc -l | tr -d ' ')"
assert_eq "generated candidates are unique" "$candidate_count" "$unique_count"

typeset overlong_count="$(awk -F. 'length($1) > 20 { count++ } END { print count + 0 }' "$CANDIDATES")"
assert_eq "no generated label exceeds 20 characters" "0" "$overlong_count"

typeset joined_line="$(grep -n '^letsgo\.travel$' "$CANDIDATES" | cut -d: -f1)"
typeset hyphen_line="$(grep -n '^lets-go\.travel$' "$CANDIDATES" | cut -d: -f1)"
assert_true "joined spelling ranks before equivalent hyphenation" test "$joined_line" -lt "$hyphen_line"

PATH="$TEST_ROOT/fake-bin:$PATH" zsh "$SCRIPT" --generate-only --limit 12 --output-dir "$TEST_ROOT/limited" >/dev/null
typeset limited_count="$(wc -l < "$TEST_ROOT/limited/candidates.txt" | tr -d ' ')"
assert_eq "limit restricts generated candidates" "12" "$limited_count"

printf '%s\n' \
  '  LetsGo.TRAVEL  ' \
  'lets-go' \
  'LETSGO' \
  '-invalid' \
  'two--hyphens' \
  'this-label-is-far-too-long-for-the-tool' \
  'café' > "$TEST_ROOT/input.txt"

PATH="$TEST_ROOT/fake-bin:$PATH" zsh "$SCRIPT" --generate-only --input "$TEST_ROOT/input.txt" --output-dir "$TEST_ROOT/input-output" >/dev/null
assert_eq "input candidates are normalised and deduplicated" "2" "$(wc -l < "$TEST_ROOT/input-output/candidates.txt" | tr -d ' ')"
assert_contains "full domains are normalised" "letsgo.travel" "$TEST_ROOT/input-output/candidates.txt"
assert_contains "valid hyphenated input is retained" "lets-go.travel" "$TEST_ROOT/input-output/candidates.txt"

typeset invalid_output
if invalid_output="$(zsh "$SCRIPT" --not-an-option 2>&1)"; then
  fail "unknown option exits non-zero"
  TESTS_RUN+=1
else
  TESTS_RUN+=1
  if [[ "$invalid_output" == *"Unknown option"* ]]; then
    pass "unknown option exits non-zero"
  else
    fail "unknown option exits non-zero"
  fi
fi

if invalid_output="$(zsh "$SCRIPT" --limit zero 2>&1)"; then
  fail "invalid numeric option exits non-zero"
  TESTS_RUN+=1
else
  TESTS_RUN+=1
  if [[ "$invalid_output" == *"positive integer"* ]]; then
    pass "invalid numeric option exits non-zero"
  else
    fail "invalid numeric option exits non-zero"
  fi
fi

mkdir -p "$TEST_ROOT/rdap-bin"
printf '%s\n' \
  '#!/usr/bin/env zsh' \
  'typeset url="${@: -1}"' \
  'print -r -- "$url" >> "$FAKE_CURL_LOG"' \
  'case "$url" in' \
  '  *registered.travel) print -n -- 200 ;;' \
  '  *open-name.travel) print -n -- 404 ;;' \
  '  *limited.travel) print -n -- 429 ;;' \
  '  *broken.travel) exit 28 ;;' \
  '  *) print -n -- 503 ;;' \
  'esac' > "$TEST_ROOT/rdap-bin/curl"
chmod +x "$TEST_ROOT/rdap-bin/curl"

printf '%s\n' \
  'registered.travel' \
  'open-name.travel' \
  'limited.travel' \
  'server-error.travel' \
  'broken.travel' > "$TEST_ROOT/rdap-input.txt"

: > "$TEST_ROOT/curl.log"
typeset rdap_output
if rdap_output="$(FAKE_CURL_LOG="$TEST_ROOT/curl.log" PATH="$TEST_ROOT/rdap-bin:$PATH" zsh "$SCRIPT" --input "$TEST_ROOT/rdap-input.txt" --delay 0 --output-dir "$TEST_ROOT/rdap-output" 2>&1)"; then
  pass "RDAP check exits successfully"
  TESTS_RUN+=1
else
  print -ru2 -- "$rdap_output"
  fail "RDAP check exits successfully"
  TESTS_RUN+=1
fi

readonly RESULTS="$TEST_ROOT/rdap-output/results.csv"
assert_true "results ledger exists" test -f "$RESULTS"
assert_eq "results ledger has header and five rows" "6" "$(wc -l < "$RESULTS" | tr -d ' ')"
assert_file_has "HTTP 200 is registered" '"registered.travel"' "$RESULTS"
assert_file_has "registered classification is persisted" '"REGISTERED","200"' "$RESULTS"
assert_file_has "HTTP 404 might be available" '"open-name.travel"' "$RESULTS"
assert_file_has "available classification is persisted" '"MIGHT_BE_AVAILABLE","404"' "$RESULTS"
assert_file_has "HTTP 429 remains unknown" '"UNKNOWN","429"' "$RESULTS"
assert_file_has "unexpected HTTP status remains unknown" '"UNKNOWN","503"' "$RESULTS"
assert_file_has "network failure remains unknown" '"UNKNOWN","NETWORK_ERROR"' "$RESULTS"
assert_contains "available report contains the 404 domain" "open-name.travel" "$TEST_ROOT/rdap-output/might-be-available.txt"
assert_contains "unknown report contains rate-limited domain" "limited.travel" "$TEST_ROOT/rdap-output/unknown.txt"
assert_contains "unknown report contains server-error domain" "server-error.travel" "$TEST_ROOT/rdap-output/unknown.txt"
assert_contains "unknown report contains network-error domain" "broken.travel" "$TEST_ROOT/rdap-output/unknown.txt"
assert_true "one request is made per candidate" test "$(wc -l < "$TEST_ROOT/curl.log" | tr -d ' ')" -eq 5

TESTS_RUN+=1
if [[ "$rdap_output" == *"REGISTERED: 1"* && "$rdap_output" == *"MIGHT_BE_AVAILABLE: 1"* && "$rdap_output" == *"UNKNOWN: 3"* ]]; then
  pass "summary reports exact classification totals"
else
  fail "summary reports exact classification totals"
fi

: > "$TEST_ROOT/curl.log"
FAKE_CURL_LOG="$TEST_ROOT/curl.log" PATH="$TEST_ROOT/rdap-bin:$PATH" zsh "$SCRIPT" --input "$TEST_ROOT/rdap-input.txt" --delay 0 --resume --output-dir "$TEST_ROOT/rdap-output" >/dev/null 2>&1
assert_eq "resume retries only unknown results" "3" "$(wc -l < "$TEST_ROOT/curl.log" | tr -d ' ')"

typeset duplicate_domains="$(awk -F, 'NR > 1 { count[$2]++ } END { for (domain in count) if (count[domain] != 1) duplicates++ ; print duplicates + 0 }' "$RESULTS")"
assert_eq "resume keeps one canonical row per domain" "0" "$duplicate_domains"

printf '%s\n' 'registered.travel' 'server-error.travel' > "$TEST_ROOT/interrupt-input.txt"
: > "$TEST_ROOT/curl.log"
FAKE_CURL_LOG="$TEST_ROOT/curl.log" PATH="$TEST_ROOT/rdap-bin:$PATH" "$SCRIPT" \
  --input "$TEST_ROOT/interrupt-input.txt" \
  --delay 2 \
  --output-dir "$TEST_ROOT/interrupt-output" >/dev/null 2>&1 &
typeset interrupt_pid=$!
typeset -i poll_count=0
while (( poll_count < 100 )); do
  if [[ -f "$TEST_ROOT/interrupt-output/results.csv" ]] && grep -Fq '"registered.travel"' "$TEST_ROOT/interrupt-output/results.csv"; then
    break
  fi
  sleep 0.02
  poll_count+=1
done
kill -TERM "$interrupt_pid" 2>/dev/null || true
wait "$interrupt_pid" 2>/dev/null || true
assert_file_has "completed lookup survives interruption" '"registered.travel"' "$TEST_ROOT/interrupt-output/results.csv"

typeset help_output
if help_output="$("$SCRIPT" --help 2>&1)"; then
  TESTS_RUN+=1
  if [[ "$help_output" == *"--generate-only"* && "$help_output" == *"--resume"* && "$help_output" == *"MIGHT_BE_AVAILABLE means RDAP returned no registration record"* ]]; then
    pass "executable help documents usage and availability caveat"
  else
    fail "executable help documents usage and availability caveat"
  fi
else
  TESTS_RUN+=1
  fail "executable help documents usage and availability caveat"
fi

assert_file_has "availability report carries registrar warning" "MIGHT_BE_AVAILABLE means RDAP returned no registration record. Confirm availability, eligibility, premium pricing, and purchase terms with an accredited registrar." "$TEST_ROOT/rdap-output/might-be-available.txt"

(( TESTS_FAILED == 0 ))
