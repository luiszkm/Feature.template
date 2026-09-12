# Optional docs (not in eval unless approved)

Non-agent documents cited by T0/T1. **ADRs / RFCs are always excluded.**
Agent skill-tree files (`.agents/skills`, `.cursor/skills`, `.claude/skills`) are always in scope.

## Ask the user

Present the doc **types** below and ask which (if any) to include in this run.
Then re-run inventory with `--include-doc-type <type>` and/or `--include-doc <path>`.

## Always excluded (decision records)

_None discovered in one-hop cites._

## Optional types (default: omitted)

| Type | Count | Example paths |
|------|------:|---------------|
| `.github` | 1 | `.github/workflows/ci.yml` |
| `docs/architecture` | 2 | `docs/architecture/guidelines.md`, `docs/architecture/vsa.md` |
| `docs/guides` | 1 | `docs/guides/getting-started.md` |
| `docs/security` | 1 | `docs/security/RBAC_MATRIX.md` |
| `features.json` | 1 | `features.json` |
| `src` | 4 | `src/App/Features/Ai/AGENTS.md`, `src/App/Features/Authorization/AGENTS.md`, `src/App/Features/Identity/AGENTS.md` (+1) |

### Re-run examples

```bash
python3 "$SKILL_DIR/scripts/inventory_extract.py" --root . --run-id "$RUN_ID" \
  --include-doc-type docs
python3 "$SKILL_DIR/scripts/inventory_extract.py" --root . --run-id "$RUN_ID" \
  --include-doc docs/context.md --include-doc docs/infrastructure.md
```

## Included optional docs this run

_None (default)._

