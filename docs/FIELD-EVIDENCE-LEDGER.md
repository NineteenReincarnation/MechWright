# RimWorld 1.7 field evidence ledger — import contract

This document defines an **audit input format**, not a claim of coverage. It complements [FIELD-COVERAGE.md](FIELD-COVERAGE.md) and is intended for the next implementation of machine-readable coverage statistics.

## Evidence records

Store a row per `(game_build, package, def_type, xml_path, runtime_class)`, **not** one row per field name. Suggested CSV columns:

```csv
game_build,package,def_type,xml_path,runtime_class,xml_sample_count,dll_member_declaring_type,dll_member_name,xml_roundtrip_test,game_load_test,evidence_status,source_sha256
```

- `package`: Core, Royalty, Ideology, Biotech, Anomaly, Odyssey; never infer installed or enabled DLC from a missing folder.
- `xml_path`: path relative to the containing Def, with repeated elements denoted by `li[]`; preserve element/attribute distinction (for example `@Class`).
- `runtime_class`: the actual `Class` override when present; otherwise blank means **unknown**, not necessarily the base CLR type.
- `xml_sample_count`: number of matching **source occurrences** in scanned files; counts must not be confused with unique field paths.
- `source_sha256`: hash of the source file or deterministic source manifest, allowing a report to be audited without committing proprietary game assets.
- `xml_roundtrip_test` and `game_load_test`: separate booleans with a third `unknown` state. A successful serializer roundtrip is not a game load.
- `evidence_status`: exactly one of `confirmed_xml_writable`, `sample_only`, `dll_only`, `unverified`. A DLL member alone is **not** XML serializability proof.

## Promotion rules

1. **Sample only**: observed in a versioned official Def XML; record package, build, source hash, and full parent path. Do not infer validity for a different `Class` or `Def` type.
2. **DLL only**: member observed in actual game assembly metadata, including declaring CLR type. Reflection visibility does not establish the game's XML loading semantics.
3. **Confirmed XML writable**: official source instance plus a successful import → targeted edit → export → reload test, preserving surrounding unknown nodes, `ParentName`, `Inherit`, `Class`, `MayRequire`, sibling ordering, and comments as applicable. This status still does **not** imply a successful game load.
4. **Unverified**: no qualifying evidence or conflicting observations. A built-in editor suggestion is not evidence.

If an XML source and DLL candidate disagree, keep both observations and mark the proposed write operation **unverified** until the class binding and loader behavior are checked. Never generate a new `li` or a `Class` override from a mere path match.

## Required regression fixtures before counting coverage

| Fixture | Expected invariant |
| --- | --- |
| Two `comps/li` entries with different `Class` values and identical child names | Edit the explicitly selected item only; preserve the other class and attributes |
| `verbs/li` with two nested `li` siblings | Explicit 1-based index; no implicit first match; preserve ordering |
| `ParentName` with `Inherit="False"` on a child | Import/edit/export/reload and undo/redo preserve both attributes |
| `MayRequire` / `MayRequireAnyOf` and unknown third-party node | No invented resolution; unknown nodes survive roundtrip |
| Empty scalar versus absent scalar | Blank UI inputs do not synthesize XML nodes; existing empty nodes are not silently removed |
| Text-only `abilities/li` and complex `tools/li` | Distinct editing behavior, with no coercion of complex list members into strings |
| CDATA leaf and mixed text leaf | Preserve single CDATA node; reject unsafe mixed-text scalar edit |
| Repeated DefName in different files | Ambiguous references never auto-resolve to the first match |

For each fixture, assert: exported XML semantics, reload semantics, undo/redo, revision/dirty behavior on rejected and no-op edits, and the selected UI member. Record CI job URL and commit SHA separately from any game-load evidence.

## Coverage denominator and report

Report **observed unique XML paths** and **DLL-only candidate paths** separately for every package and Def type. Never divide by a fabricated total of “all legal fields.” Publish counts of confirmed, sample-only, DLL-only and unverified records, plus unknown/ambiguous classes, without labeling the resulting ratio as total RimWorld coverage.

Do not copy official XML, game DLLs, or textures into the repository. Scan a user-provided lawful installation and store only paths, counts, metadata, hashes, and reproducible test identifiers. All newly created custom DefNames in fixtures must use the `MP_` prefix.

## Implementation handoff

Current `FieldCoverageReport` produces a Markdown inventory from local scan evidence. The next concrete engineering step is a deterministic CSV/JSON export matching this ledger, then a diff against the prior scan keyed by build/package/type/path/class. Only after that should the UI expose per-field provenance and verified write controls. This document adds **no newly verified game fields** and requires no CI build.
