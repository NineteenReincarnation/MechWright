<h1 align="center">MechWright</h1>

<p align="center"><strong>A Visual Mechanoid Editor for RimWorld</strong></p>

<p align="center">
  <a href="./README.md"><img src="https://img.shields.io/badge/%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-Switch-0969da?style=for-the-badge" alt="Switch to Simplified Chinese"></a>
  <a href="./README.en.md"><img src="https://img.shields.io/badge/English-Current-238636?style=for-the-badge" alt="English (current)"></a>
</p>

<p align="center">
  <a href="./LICENSE"><img src="https://img.shields.io/badge/License-Apache--2.0-2ea043?style=flat-square" alt="Apache 2.0 License"></a>
  <img src="https://img.shields.io/badge/RimWorld-1.7-986f4b?style=flat-square" alt="RimWorld 1.7">
  <img src="https://img.shields.io/badge/Platform-Windows%20x64-3776ab?style=flat-square" alt="Windows x64">
  <a href="https://github.com/NineteenReincarnation/MechWright/stargazers"><img src="https://img.shields.io/github/stars/NineteenReincarnation/MechWright?style=flat-square&color=e3b341" alt="GitHub Stars"></a>
</p>

---

Designing RimWorld mechanoids often involves several XML files for body structures, stats, weapons, and spawning rules.

**MechWright** brings those definitions together in a retro-digital visual editor. Start a new mechanoid project or import an existing mod to continue editing.

## Features

- **Freeform vertical worksheet**: Enter every stat from top to bottom with inline value controls and per-row save/inheritance reset. No chassis preset is required.
- **Add unlimited native entries**: Search and append scalar options, repeated XML list items, nested groups, editable values, and node attributes using ordinary UI controls.
- **Construct bodies from scratch**: Add child parts to any selected BodyDef node and configure coverage, height, inside/outside placement independently.
- **Create blank native Defs**: Author new body parts, weapon and projectile definitions, recipes, abilities, and other native types field by field. C#-dependent game behavior still needs runtime validation.
- **Visual editing**: Edit body parts, coverage, and native XML lists while preserving unknown fields.
- **Vanilla reference library**: Read mechanoid definitions from installed RimWorld Core and DLC files, including stats, body structures, weapon information, and comparisons.
- **Combat and texture previews**: Inspect weapon burst timing, theoretical DPS, and locally available mechanoid textures.
- **Project management**: Edit multiple XML files, check references and conflicts, and manage project textures, audio, and assemblies.
- **Export**: Generate standard mod folders and XML, with optional copies of your own assets.

## Download and Run

On Windows x64, download or clone the repository, open [app](./app/), and run [MechWright.App.exe](./app/MechWright.App.exe). Keep the other runtime files in the same folder.

The build is self-contained and requires no separate .NET installation. To browse vanilla mechanoids, select your local RimWorld installation.

MechWright is still in development. Exported XML should be tested with the intended RimWorld and DLC setup.

## Development and License

Building from source requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The project is licensed under the [Apache License 2.0](./LICENSE).

---

<div align="center">
  If MechWright is useful to you, consider giving the project a <strong>Star</strong>.
</div>
