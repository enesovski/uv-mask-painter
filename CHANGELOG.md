# Changelog

## [2.0.0-preview] - Unreleased
### Added
- GPU painting engine, measured in world space and drawn in UV space. Strokes are correct across seams and stay inside UV islands, with seam padding.
- Render-mesh picking through a BVH. No colliders are needed, and skinned meshes and Prefab Mode are supported.
- Brush modes: Paint (to a chosen value), Erase and Smooth. Brushes can write any combination of R/G/B/A channels.
- Hardness works as named. Strength caps the opacity of a stroke.
- Fill the whole texture, or fill only the UV island under the cursor.
- Undo/redo integrated with Unity's Undo. Each step keeps only the changed region, on the GPU.
- Lossless load and save of PNG/EXR files. Other formats are saved as a PNG copy. Background saving uses atomic writes.
- Automatic target detection: the remembered choice, then `[ArchuraMask]`, then mask-like property names.
- Automatic mask creation next to the material, with no dialog.
- Legacy converter from v1 MaskAsset to PNG.
- EditMode test suite.

### Changed
- Namespace changed to `Archura.UVMaskPainter`. Code is now split into Runtime, Editor and Tests assemblies.

### Removed
- The v1 editor windows, inspectors, Mask Painter tool and manager.
- The MeshCollider and `MaskPaintable` requirements.
