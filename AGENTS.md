# AGENTS.md — MechWright

## Mission
Build the **complete** RimWorld mechanoid XML visual design workbench described in [docs/PLAN.md](docs/PLAN.md). Scope: RimWorld 1.7 Core + Royalty, Ideology, Biotech, Anomaly, Odyssey; extensible native XML catalogs and unknown-field preservation. User-facing work must be real, not mock-only.

## Repository rules
- Work **directly on `main`** unless the user explicitly changes this. Do not create feature branches or PRs automatically.
- Read `docs/PLAN.md` before significant architectural or UI edits; use the uploaded monochrome chip mark in `assets/branding/` unchanged as the logo source.
- Prefer small coherent commits. Inspect existing files and tests first. Do not overwrite unrelated work, delete docs, import copyrighted game assets, or invent unimplemented behaviors.
- Default project namespace is `MechWright`; user-defined *RimWorld Mod DefNames* in sample projects use `MP_` where relevant, but the app must support user-configurable prefixes.

## Architecture
- Target `.NET 10 LTS`, `Avalonia 12.x`, C#, compiled bindings and MVVM.
- Core domain is UI/framework/IO-agnostic. App presents; RimWorld layer owns game schema/XML; Infrastructure owns file IO and caching. Compose via explicit interfaces/DI.
- Single document state drives form, tree, visualization, XML preview and diagnostics. Use reversible commands and incremental validation.
- Preserve unknown XML, comments, order-sensitive list nodes and attributes; no DTO-only rewrites of imported files. Follow safe parsing and atomic export.
- Keep feature-related logic close; avoid giant service classes, reflection-heavy form switches, circular dependencies, unnecessary abstraction and empty scaffolding.

## UX contract
- Retro digital **without visual noise**. Legibility, no clipping, no white flashes, accessible keyboard focus, true selection state, responsive docking, reduced motion, high DPI.
- Prefer fully functional UI paths to placeholders. Never silently apply unconfirmed default values as user decisions.
- All interaction should be undoable when appropriate. Validation points to the exact offending field.

## Build and verification
- Once the solution exists: `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release` (from repo root).
- Add focused tests whenever changing field semantics, XML round trips, Def resolution, export or nontrivial UI state. Cover imported unknown fields.
- Validate on Windows for final UI, rendering and resource behavior. If the game installation or environment is unavailable, state which checks were not performed; never claim a successful game load.
- Keep benchmark fixtures for catalogs, large trees and XML import/export. Performance fixes require reproducible metrics.

## Definition of done
- Complete end-to-end functionality for the changed slice, readable maintainable code, correct game-native output, meaningful validation, tests, useful error states, and documentation only where it supports maintainability.
- Report commit SHA and actual checked behavior; distinguish implemented, stubbed, and unverified work.
