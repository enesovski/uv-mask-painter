# Asset Store release checklist

This folder ends in `~`, so Unity never imports it and it never ships. The same goes for `.claude/` (dev instructions) and `.git/`.

## 1. Decisions only you can make
- [ ] **LICENSE (MIT, "Enes Ceran")**: shipping an MIT license inside a paid Asset Store package conflicts with the Asset Store EULA (it would let buyers redistribute the code). Remove it from the package, or keep the GitHub repo private / separately licensed.
- [ ] **Tests**: ship `Tests/` (it only compiles when the Test Framework is installed) or leave it out of the export.
- [ ] **Price, category** (suggested: *Tools / Painting*), publisher name, support email/Discord.
- [ ] Which pipeline the demo scenes target first (plan: URP, with Built-in/HDRP shaders in `Samples/Shared/Shaders`).

## 2. Package contents
- [ ] Export root: `Assets/Archura/UVMaskPainter/` (Editor, Runtime, Samples, Documentation, Tests?, README.md, CHANGELOG.md).
- [ ] Nothing in the export references assets outside this folder.
- [ ] `Third-Party Notices.txt` at the root, if any demo uses CC0/third-party assets.
- [ ] Remove `.gitkeep` placeholders and empty folders from `Samples/` once the demos are built.
- [ ] Documentation: export `Documentation/Manual.md` to PDF (or host it and link it). Fill in the support line.
- [ ] Version: set `CHANGELOG.md` to `2.0.0 - <date>`.

## 3. Demos (see `Samples/README.md`)
- [ ] RainyPuddles · [ ] MossWalls · [ ] TerrainBiomes · [ ] CharacterGrime
- [ ] Each scene opens with no errors or missing references in a fresh project.

## 4. Compatibility testing
Run the EditMode tests and the manual smoke test (select, P, paint, undo, leave, Ctrl+Z, terrain, skinned, runtime sample) on:

| Unity | Pipeline | Graphics API |
|---|---|---|
| 2022.3 LTS | Built-in, URP, HDRP | DX11 (Windows) |
| Unity 6 LTS | URP, HDRP | DX12, Vulkan (Windows) |
| Unity 6 LTS | URP | Metal (macOS) |

Watch especially for UV orientation (D3D vs Metal/Vulkan), shortcut conflicts, and overlay layout in light and dark skins.

## 5. Validation and upload
- [ ] Install **Asset Store Publishing Tools** (Package Manager ▸ Unity Registry or the Asset Store) and run its **Validator** on the export folder. Fix all errors.
- [ ] Upload with the Publishing Tools to a draft created in the Publisher Portal.
- [ ] Fill in the listing from `StoreListing.md`, with media from its shot list.
- [ ] Re-read the current Asset Store submission guidelines before submitting; they change over time.

## 6. After release
- [ ] Tag the release in git (`v2.0.0`).
- [ ] Watch reviews and support for the first two weeks; prepare a 2.0.1 if needed.
