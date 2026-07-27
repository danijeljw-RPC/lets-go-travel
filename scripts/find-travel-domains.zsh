#!/usr/bin/env zsh

set -u
setopt pipefail

readonly RDAP_BASE_URL="https://rdap.identitydigital.services/rdap/domain"
readonly AVAILABILITY_WARNING="MIGHT_BE_AVAILABLE means RDAP returned no registration record. Confirm availability, eligibility, premium pricing, and purchase terms with an accredited registrar."

typeset -gi GENERATE_ONLY=0
typeset -gi RESUME=0
typeset -gi LIMIT=0
typeset DELAY="0.5"
typeset INPUT_FILE=""
typeset OUTPUT_DIR="travel-domain-results"

typeset -A CANDIDATE_SCORES

die() {
  print -ru2 -- "Error: $*"
  exit 2
}

usage() {
  print -r -- "Usage: ${0:t} [options]"
  print -r -- ""
  print -r -- "Generate energetic .travel domain ideas and check them through RDAP."
  print -r -- ""
  print -r -- "Options:"
  print -r -- "  --generate-only     Generate candidates without network requests"
  print -r -- "  --limit N           Use only the first N ranked candidates"
  print -r -- "  --delay SECONDS     Delay between RDAP requests (default: 0.5)"
  print -r -- "  --resume            Keep final results and retry UNKNOWN entries"
  print -r -- "  --input FILE        Read labels or .travel domains from FILE"
  print -r -- "  --output-dir DIR    Write reports to DIR (default: travel-domain-results)"
  print -r -- "  --help              Show this help"
  print -r -- ""
  print -r -- "RDAP classifications: REGISTERED, MIGHT_BE_AVAILABLE, UNKNOWN"
  print -r -- "$AVAILABILITY_WARNING"
}

parse_args() {
  while (( $# > 0 )); do
    case "$1" in
      --generate-only)
        GENERATE_ONLY=1
        ;;
      --resume)
        RESUME=1
        ;;
      --limit)
        (( $# >= 2 )) || die "--limit requires a positive integer"
        [[ "$2" =~ '^[1-9][0-9]*$' ]] || die "--limit requires a positive integer"
        LIMIT="$2"
        shift
        ;;
      --delay)
        (( $# >= 2 )) || die "--delay requires a non-negative number"
        [[ "$2" =~ '^([0-9]+([.][0-9]*)?|[.][0-9]+)$' ]] || die "--delay requires a non-negative number"
        DELAY="$2"
        shift
        ;;
      --input)
        (( $# >= 2 )) || die "--input requires a readable file"
        INPUT_FILE="$2"
        shift
        ;;
      --output-dir)
        (( $# >= 2 )) || die "--output-dir requires a directory"
        OUTPUT_DIR="$2"
        shift
        ;;
      --help)
        usage
        exit 0
        ;;
      --*)
        die "Unknown option: $1"
        ;;
      *)
        die "Unexpected argument: $1"
        ;;
    esac
    shift
  done

  [[ -n "$OUTPUT_DIR" ]] || die "--output-dir cannot be empty"
  if [[ -n "$INPUT_FILE" ]]; then
    [[ -r "$INPUT_FILE" && -f "$INPUT_FILE" ]] || die "Input file is not readable: $INPUT_FILE"
  fi
}

normalise_label() {
  local raw="$1"
  local label

  label="$(print -r -- "$raw" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' | tr '[:upper:]' '[:lower:]')"
  label="${label%.travel}"

  (( ${#label} >= 1 && ${#label} <= 20 )) || return 1
  [[ "$label" != *--* ]] || return 1
  [[ "$label" =~ '^[a-z0-9]([a-z0-9-]*[a-z0-9])?$' ]] || return 1

  print -r -- "$label"
}

store_candidate() {
  local label="$1"
  local score="$2"
  local normalised

  normalised="$(normalise_label "$label")" || return 0
  if [[ -z "${CANDIDATE_SCORES[$normalised]-}" || "$score" -gt "${CANDIDATE_SCORES[$normalised]}" ]]; then
    CANDIDATE_SCORES[$normalised]="$score"
  fi
}

add_phrase() {
  local phrase="$1"
  local weight="$2"
  local curated="$3"
  local -a words
  local joined hyphenated
  local joined_score hyphenated_score

  words=(${=phrase})
  joined="${phrase// /}"
  hyphenated="${phrase// /-}"
  joined_score=$(( 1000 + weight + curated * 40 - ${#joined} * 3 - ${#words} * 4 ))
  (( ${#joined} <= 14 )) && joined_score=$(( joined_score + 50 ))
  hyphenated_score=$(( joined_score - 20 - (${#hyphenated} - ${#joined}) * 3 ))

  store_candidate "$joined" "$joined_score"
  if (( ${#words} > 1 )); then
    store_candidate "$hyphenated" "$hyphenated_score"
  fi
}

generate_candidates() {
  local action time direction companion motion phrase
  local -a actions times directions companions motions

  CANDIDATE_SCORES=()

  for phrase in \
    "lets go" "lets explore" "lets escape" "lets wander" "lets roam" \
    "come along" "come explore" "come wander" "come away" "come fly" \
    "go now" "go today" "go explore" "go discover" "go beyond" \
    "get going" "get moving" "get away" "get out there" "get exploring" \
    "ready set go" "ready to go" "pack and go" "dream and go" "book and go" \
    "pick and go" "plan and go" "choose and go" "time to go" "dare to go" \
    "take me away" "take us away" "take the trip" "make the trip" \
    "see the world" "meet the world" "explore more" "wander more" \
    "roam free" "fly away" "escape today" "travel happy" "travel light"; do
    add_phrase "$phrase" 260 1
  done

  actions=(go fly roam wander explore escape discover adventure travel ride sail cruise journey)
  times=(now today tonight soon again anytime onward)
  directions=(beyond far anywhere somewhere there afar abroad outside)
  companions=(me us together)
  motions=(going moving away outside exploring wandering roaming travelling)

  for action in $actions; do
    add_phrase "lets $action" 210 0
    add_phrase "come $action" 190 0
    add_phrase "ready to $action" 170 0
    for time in $times; do
      add_phrase "$action $time" 150 0
    done
    for direction in $directions; do
      add_phrase "$action $direction" 140 0
    done
    for companion in $companions; do
      if [[ "$companion" == together ]]; then
        add_phrase "$action together" 150 0
      else
        add_phrase "$action with $companion" 130 0
      fi
    done
  done

  for motion in $motions; do
    add_phrase "get $motion" 180 0
    add_phrase "lets get $motion" 150 0
  done
}

load_input_candidates() {
  local file="$1"
  local raw label score

  CANDIDATE_SCORES=()
  while IFS= read -r raw || [[ -n "$raw" ]]; do
    label="$(normalise_label "$raw")" || continue
    score=$(( 1000 - ${#label} * 3 ))
    (( ${#label} <= 14 )) && score=$(( score + 50 ))
    [[ "$label" == *-* ]] && score=$(( score - 20 ))
    store_candidate "$label" "$score"
  done < "$file"
}

write_candidates() {
  local records_file="$1"
  local destination="$2"
  local label score
  local -a pairs

  : > "$records_file" || die "Cannot write temporary candidate data in $OUTPUT_DIR"
  for label score in ${(kv)CANDIDATE_SCORES}; do
    print -r -- "${score}"$'\t'"${label}" >> "$records_file"
  done

  LC_ALL=C sort -t $'\t' -k1,1nr -k2,2 "$records_file" > "${records_file}.sorted" || die "Unable to rank candidates"
  mv -f -- "${records_file}.sorted" "$records_file" || die "Unable to save ranked candidates"

  if (( LIMIT > 0 )); then
    awk -F '\t' -v limit="$LIMIT" 'NR <= limit { print $2 ".travel" }' "$records_file" > "$destination"
    head -n "$LIMIT" "$records_file" > "${records_file}.limited"
    mv -f -- "${records_file}.limited" "$records_file"
  else
    awk -F '\t' '{ print $2 ".travel" }' "$records_file" > "$destination"
  fi
}

lookup_domain() {
  local domain="$1"
  local http_status

  if http_status="$(curl \
    --silent \
    --show-error \
    --output /dev/null \
    --write-out '%{http_code}' \
    --max-time 15 \
    "$RDAP_BASE_URL/$domain")"; then
    case "$http_status" in
      200)
        print -r -- "REGISTERED"$'\t'"200"
        ;;
      404)
        print -r -- "MIGHT_BE_AVAILABLE"$'\t'"404"
        ;;
      *)
        print -r -- "UNKNOWN"$'\t'"${http_status:-NO_HTTP_STATUS}"
        ;;
    esac
  else
    print -r -- "UNKNOWN"$'\t'"NETWORK_ERROR"
  fi
}

existing_classification() {
  local domain="$1"
  local results_file="$2"

  [[ -f "$results_file" ]] || return 1
  awk -F, -v wanted="\"$domain\"" '
    NR > 1 && $2 == wanted {
      value = $4
      gsub(/^"|"$/, "", value)
      print value
      exit
    }
  ' "$results_file"
}

upsert_result() {
  local rank="$1"
  local domain="$2"
  local score="$3"
  local classification="$4"
  local http_status="$5"
  local timestamp="$6"
  local results_file="$OUTPUT_DIR/results.csv"
  local replacement sorted

  replacement="$(mktemp "$OUTPUT_DIR/.results.XXXXXX")" || die "Unable to create a temporary results file"
  sorted="$(mktemp "$OUTPUT_DIR/.results-sorted.XXXXXX")" || {
    rm -f -- "$replacement"
    die "Unable to create a sorted results file"
  }

  if [[ -f "$results_file" ]]; then
    awk -F, -v wanted="\"$domain\"" 'NR == 1 || $2 != wanted' "$results_file" > "$replacement"
  else
    print -r -- 'rank,domain,score,classification,http_status,checked_at' > "$replacement"
  fi

  print -r -- "${rank},\"${domain}\",${score},\"${classification}\",\"${http_status}\",\"${timestamp}\"" >> "$replacement"
  head -n 1 "$replacement" > "$sorted"
  tail -n +2 "$replacement" | LC_ALL=C sort -t, -k1,1n >> "$sorted"
  mv -f -- "$sorted" "$results_file" || die "Unable to save results ledger"
  rm -f -- "$replacement"
}

refresh_reports() {
  local results_file="$OUTPUT_DIR/results.csv"
  local available_file="$OUTPUT_DIR/might-be-available.txt"
  local unknown_file="$OUTPUT_DIR/unknown.txt"
  local available_tmp unknown_tmp

  available_tmp="$(mktemp "$OUTPUT_DIR/.available.XXXXXX")" || die "Unable to create availability report"
  unknown_tmp="$(mktemp "$OUTPUT_DIR/.unknown.XXXXXX")" || {
    rm -f -- "$available_tmp"
    die "Unable to create unknown report"
  }

  print -r -- "# $AVAILABILITY_WARNING" > "$available_tmp"
  awk -F, '
    NR > 1 && $4 == "\"MIGHT_BE_AVAILABLE\"" {
      domain = $2
      gsub(/^"|"$/, "", domain)
      print domain
    }
  ' "$results_file" >> "$available_tmp"

  awk -F, '
    NR > 1 && $4 == "\"UNKNOWN\"" {
      domain = $2
      gsub(/^"|"$/, "", domain)
      print domain
    }
  ' "$results_file" > "$unknown_tmp"

  mv -f -- "$available_tmp" "$available_file" || die "Unable to save availability report"
  mv -f -- "$unknown_tmp" "$unknown_file" || die "Unable to save unknown report"
}

print_summary() {
  local results_file="$OUTPUT_DIR/results.csv"
  local registered available unknown

  registered="$(awk -F, '$4 == "\"REGISTERED\"" { count++ } END { print count + 0 }' "$results_file")"
  available="$(awk -F, '$4 == "\"MIGHT_BE_AVAILABLE\"" { count++ } END { print count + 0 }' "$results_file")"
  unknown="$(awk -F, '$4 == "\"UNKNOWN\"" { count++ } END { print count + 0 }' "$results_file")"

  print -r -- ""
  print -r -- "REGISTERED: $registered"
  print -r -- "MIGHT_BE_AVAILABLE: $available"
  print -r -- "UNKNOWN: $unknown"
  print -r -- "Results: $OUTPUT_DIR/results.csv"
  print -r -- "Possible names: $OUTPUT_DIR/might-be-available.txt"
  print -r -- "Retry list: $OUTPUT_DIR/unknown.txt"
  print -r -- "$AVAILABILITY_WARNING"
}

run_checks() {
  local records_file="$1"
  local results_file="$OUTPUT_DIR/results.csv"
  local score label domain prior lookup_result classification http_status timestamp
  local -i rank=0
  local -i total

  total="$(wc -l < "$records_file" | tr -d ' ')"
  if (( ! RESUME )) || [[ ! -f "$results_file" ]]; then
    print -r -- 'rank,domain,score,classification,http_status,checked_at' > "$results_file"
  fi

  while IFS=$'\t' read -r score label; do
    [[ -n "$label" ]] || continue
    rank+=1
    domain="${label}.travel"
    prior="$(existing_classification "$domain" "$results_file")"
    if (( RESUME )) && [[ "$prior" == REGISTERED || "$prior" == MIGHT_BE_AVAILABLE ]]; then
      print -r -- "[$rank/$total] $domain $prior (cached)"
      continue
    fi

    lookup_result="$(lookup_domain "$domain")"
    classification="${lookup_result%%$'\t'*}"
    http_status="${lookup_result#*$'\t'}"
    timestamp="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"

    upsert_result "$rank" "$domain" "$score" "$classification" "$http_status" "$timestamp"
    refresh_reports
    print -r -- "[$rank/$total] $domain $classification"

    if [[ "$DELAY" != 0 && "$DELAY" != 0.0 ]]; then
      sleep "$DELAY"
    fi
  done < "$records_file"

  refresh_reports
  print_summary
}

main() {
  parse_args "$@"
  mkdir -p -- "$OUTPUT_DIR" || die "Cannot create output directory: $OUTPUT_DIR"
  [[ -w "$OUTPUT_DIR" ]] || die "Output directory is not writable: $OUTPUT_DIR"

  local records_file="$OUTPUT_DIR/.candidate-records.tsv"
  if [[ -n "$INPUT_FILE" ]]; then
    load_input_candidates "$INPUT_FILE"
  else
    generate_candidates
  fi

  write_candidates "$records_file" "$OUTPUT_DIR/candidates.txt"

  if (( GENERATE_ONLY )); then
    print -r -- "Generated $(wc -l < "$OUTPUT_DIR/candidates.txt" | tr -d ' ') ranked candidates."
    print -r -- "Candidates: $OUTPUT_DIR/candidates.txt"
    return 0
  fi

  run_checks "$records_file"
}

main "$@"
