# UV Mask Painter by Archura

Paint R/G/B/A data masks directly on meshes in the Unity Scene view: splat maps, wear, wetness, blend weights.

> **v2 is in development.** The painting engine is complete. The Scene view tool arrives in the next update.

## What v2 changes
- No setup: no components or MeshColliders are required, and any MeshRenderer or SkinnedMeshRenderer can be painted.
- Masks are ordinary PNG/EXR textures assigned to your material, so they work in builds without the tool.
- Strokes stay correct across UV seams and never leak into other UV islands.
- Full undo/redo through Unity's Undo.
- Saving happens in the background with no editor freeze.

## Requirements
Unity 2022.3 LTS or newer. Works with the Built-in pipeline, URP and HDRP.

## Shader authors
Tag a texture property with `[ArchuraMask]` so the painter picks it automatically:
```
[ArchuraMask] _Wear ("Wear", 2D) = "black" {}
```

## Upgrading from v1
Right-click a v1 Mask Asset and choose **Archura ▸ UV Mask Painter ▸ Convert Legacy Mask to PNG**, then assign the PNG to your material.
The `MaskPaintable` component and the MeshCollider can then be removed.
