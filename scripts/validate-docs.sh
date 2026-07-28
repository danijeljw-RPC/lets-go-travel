#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

python3 - "$repository_root" <<'PY'
from pathlib import Path
from urllib.parse import unquote
import re
import sys

root = Path(sys.argv[1]).resolve()
markdown_files = sorted(root.glob("*.md")) + sorted((root / "docs").rglob("*.md"))
errors: list[str] = []
link_pattern = re.compile(r"!?\[[^\]]*\]\(([^)]+)\)")

for document in markdown_files:
    content = document.read_text(encoding="utf-8-sig")
    heading_count = sum(1 for line in content.splitlines() if line.startswith("# "))
    if heading_count != 1:
        errors.append(f"{document.relative_to(root)}: expected one H1, found {heading_count}")

    for match in link_pattern.finditer(content):
        raw_target = match.group(1).strip()
        if raw_target.startswith("<") and raw_target.endswith(">"):
            raw_target = raw_target[1:-1]
        target = unquote(raw_target.split("#", 1)[0])
        if not target or target.startswith(("http://", "https://", "mailto:")):
            continue

        resolved = (document.parent / target).resolve()
        try:
            resolved.relative_to(root)
        except ValueError:
            errors.append(f"{document.relative_to(root)}: link escapes repository: {raw_target}")
            continue

        if not resolved.exists():
            errors.append(f"{document.relative_to(root)}: missing link target: {raw_target}")

if errors:
    print("\n".join(errors), file=sys.stderr)
    raise SystemExit(1)

print(f"Validated {len(markdown_files)} Markdown files.")
PY
