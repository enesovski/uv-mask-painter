# UV Mask Painter by Archura

Paint R/G/B/A data masks directly on meshes in the Unity Scene view: splat maps, wear, wetness, blend weights.

## Quick start
1. Select any mesh in the scene.
2. Press **P** (or pick UV Mask Painter in the Tools overlay).
3. Paint. The first stroke creates a mask for that object. Saving is automatic.

Masks are listed in the Scene overlay. Use **+** to create one with a name and 1–4 channels (R, RG, RGB or RGBA), click a mask to switch to it, and use **⋯** to rename its channels, use it in a material property, fill/clear/invert, resize, duplicate or delete it.

| Action | Input |
|---|---|
| Paint / erase / smooth | LMB / Ctrl+LMB / Shift+LMB |
| Size / opacity / value | Hold **S** / **A** / **D** and move the mouse sideways |
| Size · hardness · value steps | `[` `]` · Shift+`[` `]` · `-` `=` (X flips the value) |
| Channel | 1–4 (Shift+1–4 paints several) |
| Mask view | V |
| Brush · Fill tool | B · G (Fill: click an island, Shift-click the whole mask) |
| Switch object · Exit | Double-click another mesh · Esc or P |

Channel packing: select textures, then **Assets ▸ Archura ▸ Pack Channels…**
Preferences: **Preferences ▸ Archura ▸ UV Mask Painter**. Shortcuts can be rebound in **Edit ▸ Shortcuts ▸ Archura**.

## What v2 changes
- No setup: no components or MeshColliders are required, and any MeshRenderer or SkinnedMeshRenderer can be painted.
- Masks are ordinary PNG/EXR textures (their settings live in the .meta file), so they work in builds without the tool.
- Strokes stay correct across UV seams and never leak into other UV islands.
- Full undo/redo through Unity's Undo.
- Saving happens in the background with no editor freeze.

## Requirements
Unity 2022.3 LTS or newer. Works with the Built-in pipeline, URP and HDRP.

## Shader authors
Tag a texture property with `[ArchuraMask]` and new masks are plugged into it automatically:
```
[ArchuraMask] _Wear ("Wear", 2D) = "black" {}
```

## Upgrading from v1
v2 doesn't read v1 Mask Assets. Remove the `MaskPaintable` components and create new masks in paint mode.
