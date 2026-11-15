# UV Mask Painter Samples

Four demo scenes, each showing one way to use painted masks. **Status: planned, not built yet.**
This folder holds the agreed structure so each demo can be filled in on its own.

| Demo | Shows | Subject | Runtime code |
|---|---|---|---|
| [RainyPuddles](RainyPuddles/README.md) | Wetness and puddles driving smoothness and ripples | Street meshes | Footstep splashes via `MaskSurface` |
| [MossWalls](MossWalls/README.md) | Moss, dirt and damage on modular walls | Static meshes | None (pure shader) |
| [TerrainBiomes](TerrainBiomes/README.md) | Gameplay regions painted on a Unity Terrain | Terrain | Biome lookup and spawning via `MaskSampler` |
| [CharacterGrime](CharacterGrime/README.md) | Dirt, blood and wetness on a skinned character | SkinnedMeshRenderer | Grime that builds up during play |

## Layout
Every demo uses the same folders: `Scenes/`, `Materials/`, `Masks/`, `Shaders/`, `Scripts/`, `Textures/` (plus `Models/` where needed).
Things more than one demo needs live in `Shared/`.

## Render pipelines
- Scenes are authored for **URP**, the most common pipeline for Asset Store buyers.
- Every demo shader exists for Built-in, URP and HDRP in `Shared/Shaders/<Pipeline>/` (Shader Graph for URP/HDRP, a surface shader for Built-in).
- Mask properties are tagged `[ArchuraMask]` (or exposed with that name in Shader Graph), so masks plug in automatically.

## Scripts
Demo scripts go in one assembly, `Archura.UVMaskPainter.Samples` (create `Shared/Scripts/Archura.UVMaskPainter.Samples.asmdef` together with the first script),
which references only the runtime assembly `Archura.UVMaskPainter`. Samples must never depend on editor code.

## Rules for every demo
- Only assets we may redistribute: our own work or CC0, with the source listed in `Third-Party Notices.txt` at the package root.
- The scene opens without errors or missing references in a fresh project with the target pipeline.
- Masks in `Masks/` are real UV Mask Painter masks (metadata in their .meta), so buyers can open them in the painter and keep painting.
