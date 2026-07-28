<!-- markdownlint-disable MD013 -->

# Document Control

## Authority

Repository documentation is the planning source of truth for product and technical direction. Source code must not be used to settle an unresolved product, vendor, commercial, security or architecture question silently.

## Document Types

| Type | Location | Lifecycle |
| --- | --- | --- |
| Architecture decision | `docs/adr/{status}/ADR-####-title.md` | proposed, accepted, rejected, superseded |
| Open issue | `docs/issues/open/OI-####-title.md` | open, blocked, in-review, resolved, closed, superseded |
| Plan | `docs/plans/{status}/PLAN-####-title.md` | active, paused, completed |
| Ordinary documentation | Existing topic directory | Updated in place, with decision links where relevant |

## Naming and Formatting

- Use lowercase kebab-case for ordinary Markdown filenames.
- Prefix lifecycle files with their stable identifier.
- Never reuse or renumber an identifier.
- Every Markdown file has exactly one H1.
- Write normal prose paragraphs on one physical line. Do not hard-wrap prose to a fixed width.
- Use tables, lists and code blocks only where they make the structure clearer.
- Use relative links between repository Markdown files and verify filename casing.

## Decision Language

Use **proposed** for a recommended direction awaiting approval, **accepted** only after explicit approval, and **open** for questions that still need evidence or a choice. Vendor documentation may establish that an API is documented; it does not prove that the capability is enabled for this account, commercially available, production-ready, or suitable for the intended market.

## Change Rules

- Do not rewrite accepted ADR meaning. Create a superseding ADR.
- Do not delete rejected or superseded decisions; retain them as history.
- Do not close an issue without recording the answer and evidence.
- Keep ADR, issue and plan indexes current.
- Keep only the project `README.md` at repository root; place durable documentation in the appropriate `docs/` category.
- Treat Git history as the provenance record for superseded discovery material rather than retaining duplicate working copies.
- Do not claim legal, privacy, security or PCI compliance without the applicable evidence.

## Review Checklist

- [ ] The document belongs in an existing category.
- [ ] Its decision state is explicit.
- [ ] Unresolved questions link to an issue.
- [ ] Durable architecture choices link to an ADR.
- [ ] Local Markdown links are relative and valid.
- [ ] Every concept is required by the consumer product described in this repository.
- [ ] No vendor capability is overstated.
- [ ] No implementation detail has been invented to fill a decision gap.
