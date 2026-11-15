# Changelog

## [2.0.0] - Unreleased
### Added
- Scene view paint mode. Press P or use the Tools overlay. Supports MeshRenderer, SkinnedMeshRenderer and Terrain.
- Terrain painting: the brush follows the terrain's heights, and masks cover the terrain (UV = normalized position) and are saved next to the TerrainData.
- Runtime sampling: `MaskSampler` (CPU copy of any mask, readable or not; allocation-free bilinear and point sampling) and the optional `MaskSurface` component (look up channels by name, `MaskSurface.TrySampleAt(hit, "Wet", out value)`, terrain lookups by world position). Add it from ⋯ ▸ Add Runtime Sampler; it stays in sync with the painter.
- User manual (Documentation/Manual.md) and sample folder structure for four planned demos.
- Masks are created explicitly with a name and 1–4 channels (R, RG, RGB, RGBA). Each channel can be named, and channel buttons and shortcuts only show the channels a mask has.
- Scene overlay with a mask list: switch, rename, ping, duplicate and delete masks, change channels or UV set, and choose which material property uses a mask.
- Hold S / A / D and move the mouse to adjust brush size, opacity and value (like Terrain Tools). `-` / `=` step the value.
- Brush cursor with falloff ring and mode label. Clicking another object never paints through it.
- Rebindable shortcuts, active only while painting.
- The first stroke on an object with no masks creates one, with an Undo option.
- Live preview on the object, and automatic background save after every stroke and after undo/redo.
- The mask view overlay tints the selected channels.
- Channel-packed PNG export from Assets ▸ Archura ▸ Pack Channels.
- Preferences: auto-save, undo memory, mask view opacity and brush spacing.
- Settings persist per user in UserSettings/.
- GPU painting engine, measured in world space and drawn in UV space. Strokes are correct across seams and stay inside UV islands, with seam padding.
- Render-mesh picking through a BVH. No colliders are needed, and skinned meshes and Prefab Mode are supported.
- Brush modes: Paint (to a chosen value), Erase and Smooth. Brushes can write any combination of R/G/B/A channels.
- Hardness works as named. Opacity caps how far one stroke moves pixels toward the value.
- Fill the whole texture, or fill only the UV island under the cursor.
- Undo/redo integrated with Unity's Undo. Each step keeps only the changed region, on the GPU.
- Lossless load and save of PNG/EXR files. Other formats are saved as a PNG copy. Background saving uses atomic writes.
- Mask metadata stored in the texture's .meta file. No components are added to the scene.
- New masks fill an empty `[ArchuraMask]` shader property automatically.
- EditMode test suite.

### Changed
- Namespace changed to `Archura.UVMaskPainter`. Code is split into Runtime (`Archura.UVMaskPainter`), Editor and Tests assemblies.

### Removed
- The v1 editor windows, inspectors, Mask Painter tool and manager.
- The MeshCollider and `MaskPaintable` requirements.
- v1 support: MaskAsset, MaskPaintable, the legacy converter, the v1 demo and the v1 manual. v1 projects show missing-script warnings until the components are removed.
