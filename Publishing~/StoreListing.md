# Store listing draft

## Title
UV Mask Painter: Paint Masks on Meshes & Terrains

## Short description (one line)
Paint R/G/B/A data masks directly on meshes, skinned characters and terrains in the Scene view. No colliders, no components, no round trip to other apps.

## Description
Paint wear, wetness, moss, grime or gameplay regions right where they belong: on your objects, in the Scene view.

Select a mesh or terrain, press **P**, and paint. Your first stroke creates the mask, saving is automatic, and Ctrl+Z works as you'd expect. Masks are ordinary PNG textures, so your shaders, VFX and gameplay code use them in any build without the tool.

**Built for artists**
- Seam-correct strokes that never leak into other UV islands.
- Brush in world units, with Terrain Tools-style hold-and-drag for size, opacity and value.
- Fill a UV island or the whole mask in one click.
- 1–4 channels per mask, each with a name (Wet, Moss, Blood…).
- Works on MeshRenderers, SkinnedMeshRenderers (in any pose) and Unity Terrains.

**Built for programmers**
- `MaskSurface.TrySampleAt(hit, "Wet", out float wet)`: read masks at runtime by channel name.
- Works with non-readable textures, and sampling allocates nothing.
- Terrain helpers: look up the mask under any world position.

**No setup, no clutter**
- Nothing is added to your scene unless you ask for a runtime sampler.
- No MeshColliders needed for painting.
- Mask metadata lives in the texture's .meta file.

## Key features (bullets for the store)
- Paint data masks in the Scene view: select, P, paint
- Meshes, skinned characters and Terrains
- Seam-correct, island-safe GPU painting
- 1–4 named channels per mask
- Runtime sampling API for gameplay
- Automatic background saving and full undo
- Channel packing utility
- Built-in, URP and HDRP · Unity 2022.3+

## Compatibility
| | Built-in | URP | HDRP |
|---|---|---|---|
| Painting tool | ✓ | ✓ | ✓ |
| Demo scenes | shaders included | scenes + shaders | shaders included |

## Keywords
mask, paint, texture painting, vertex paint alternative, splat map, terrain, biome, wetness, puddles, moss, wear, grime, decal, shader, tool, editor, mask map, data texture

## Media shot list
1. **Hero video (30–45 s):** select rock, P, paint moss, then a puddle on a street, a biome on a terrain, grime on a character, and finally a code snippet with the game reacting.
2. **GIF:** select, P, paint (5 seconds, no cuts).
3. **GIF:** hold S/A/D to adjust size, opacity and value.
4. **Screenshot:** the overlay with named channels and the mask list.
5. **Screenshot:** a stroke across a UV seam, compared with naive projection painting.
6. **Screenshot:** terrain biome mask with Mask View on, next to the spawned props.
7. **Screenshot:** the runtime code snippet beside the painted mask.
8. **Screenshot:** a skinned character painted mid-pose.
9. **Card:** feature list with pipeline badges.

## FAQ (for the store page)
- **Does it work without the tool in builds?** Yes. Masks are PNG textures; the optional `MaskSurface` component lives in a small runtime assembly.
- **Does it replace Unity's terrain painting?** No. Paint terrain layers with Unity's tools and paint data regions (biomes, zones) with this one.
- **Can I paint color/albedo?** Not yet; the tool focuses on masks. Color painting is on the roadmap.
