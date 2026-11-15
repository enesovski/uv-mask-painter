# UV Mask Painter: Manual

UV Mask Painter lets you paint data masks (R, G, B, A) directly on meshes and terrains in the Unity Scene view.
Masks are ordinary PNG textures, so shaders, VFX and gameplay code can use them in any build, with or without the tool.

**Requirements:** Unity 2022.3 LTS or newer. Built-in, URP and HDRP. MeshRenderer, SkinnedMeshRenderer and Terrain.

---

## 1. Quick start
1. Select a mesh or a terrain in the scene.
2. Press **P** (or choose **UV Mask Painter** in the Scene view's Tools overlay).
3. Paint. If the object has no mask yet, your first stroke creates one. Saving is automatic.
4. Press **Esc** or **P** to leave paint mode.

No components, colliders or setup are needed.

---

## 2. The overlay
The **UV Mask Painter** overlay appears in the Scene view while you paint.

### Masks
The title names the selected object. The list shows its masks:
- masks used by the object's materials, and
- masks made for the object's mesh (or terrain), even if no material uses them yet.

| Action | How |
|---|---|
| Paint a different mask | Click it |
| Rename | Double-click it |
| Show it in the Project window | The magnifier button |
| More options | **⋯** or right-click |
| New mask | **+ New** (name, channels, size, or turn an existing texture into a mask) |

A mask row shows its channels (`R`, `RG`, `RGB`, `RGBA`) and, if a material uses it, the property (`→ Wear`).

**The ⋯ menu:** Rename · Channels · Rename Channel · UV Set · Use in Material · Fill / Clear / Invert (the selected channels) · Resize · Add Runtime Sampler · Duplicate · Show in Project · Delete.

### Channels
The buttons next to **Brush / Fill** are the mask's channels, using their names (for example `Wet`, `Moss`).
- Click: paint that channel. Shift-click: paint several at once.
- **+**: add a channel. Right-click a channel: rename it, or remove the last channel.

### Brush
| Setting | Meaning |
|---|---|
| **Value** | What the brush writes into the selected channels (0 = black, 1 = white) |
| **Size** | Brush radius in world units |
| **Opacity** | How far one stroke moves pixels toward Value. Strokes never build up beyond it |
| **Hardness** | Soft (0) to hard (1) edge |

**Fill** mode fills the UV island you click, or the whole mask with Shift-click. Ctrl-click clears instead.

### Mask View
Tints the object with the selected channels. It's always on for masks that no material uses yet, because there's no other way to see them.

---

## 3. Shortcuts
All shortcuts can be changed in **Edit ▸ Shortcuts ▸ Archura**, and only apply while you're painting.

| Action | Default |
|---|---|
| Enter / leave paint mode | P (Esc also leaves) |
| Paint · erase · smooth | LMB · Ctrl+LMB · Shift+LMB |
| Adjust size · opacity · value | Hold **S** · **A** · **D** and move the mouse sideways |
| Size · hardness | `[` `]` · Shift+`[` `]` · Ctrl+Scroll |
| Value step · flip | `-` `=` · X |
| Channel 1–4 · add channel to selection | 1–4 · Shift+1–4 |
| Brush · Fill tool | B · G |
| Mask View | V |
| Switch object | Double-click another mesh or terrain |

---

## 4. How masks work
- **A mask is a PNG** with a small amount of metadata in its `.meta` file: channel count, channel names, UV set and the object it was made for. It also gets the asset label `ArchuraMask`. Moving, renaming or committing it to version control works like any other texture.
- **Channel count** (1–4) sets the import format: one channel imports as a compact single-channel texture, two or three without alpha, four with alpha. The file always keeps all four channels, so changing the count never loses paint.
- **Where it's saved:** next to the object's material (terrains: next to the TerrainData), otherwise next to the scene.
- **Shared meshes share masks.** Every object using the same mesh asset lists the same masks, because they share UVs. To give two copies different masks, give each copy its own material and use a different mask in each.
- **Using a mask in a material** is always your choice (⋯ ▸ Use in Material). The one exception: if the shader has an empty property tagged `[ArchuraMask]`, new masks fill it automatically.
- **Undo** works with Ctrl+Z, even after leaving paint mode.

---

## 5. Terrains
Select a Terrain and press P. The brush follows the terrain's heights, and the mask covers the whole terrain (UV 0–1 across its width and length).

Terrain masks are usually **data**: biomes, spawn density, no-build zones, wetness. Use Unity's terrain tools for terrain layers as usual. Use this tool for regions your code or custom shaders read.

To use a terrain mask in a shader, sample it with `(worldPos.xz - terrainPos.xz) / terrainSize.xz`.

---

## 6. Reading masks at runtime
Add a runtime sampler from the painter: **⋯ ▸ Add Runtime Sampler (Mask Surface)**. This adds a `MaskSurface` component with the mask, its UV set and its channel names filled in, and keeps them in sync when you change the mask in the painter.

```csharp
using Archura.UVMaskPainter;

// Footsteps: how wet is the ground where we stepped?
if (Physics.Raycast(foot.position, Vector3.down, out RaycastHit hit, 1f) &&
    MaskSurface.TrySampleAt(hit, "Wet", out float wet))
{
    PlayFootstep(wet > 0.5f ? splashClip : dryClip);
}

// Terrains: no raycast needed.
if (terrainMask.TrySample(player.position, out Color biome))
    UpdateAmbience(forest: biome.r, swamp: biome.b);
```

- **Raycast hits** need a **MeshCollider** using the same mesh (UV0 or UV1), or a **TerrainCollider**. Other colliders have no UVs.
- **Without the component**, use `new MaskSampler(texture)` and `MaskSampler.TryGetUV(hit, uvChannel, out Vector2 uv)` or `MaskSampler.TerrainUV(terrain, position)`.
- `MaskSampler` copies the mask to the CPU once, which also works for non-readable textures, and sampling allocates nothing. Set **Max Resolution** to save memory on large masks.
- `SamplePoint` returns the nearest texel without blending, which suits hard region IDs.

---

## 7. Shaders
Any 2D texture property can use a mask. To have new masks plug in automatically, tag the property:

```hlsl
// Built-in / hand-written shaders
[ArchuraMask] _Wear ("Wear", 2D) = "black" {}
...
float wear = tex2D(_Wear, i.uv).r;            // channel R
float3 albedo = lerp(baseColor, wornColor, wear);
```

Shader Graph can't add attributes. Expose a Texture2D property and choose it once with ⋯ ▸ Use in Material.
Masks are linear (not sRGB), so sample them as data. In Shader Graph, use **Sample Texture 2D** with Type = Default and read the channel you need.

---

## 8. Channel packing
Select up to four textures, then **Assets ▸ Archura ▸ Pack Channels…**. Choose a source channel (or black/white) for each output channel and export a PNG.

---

## 9. Preferences
**Preferences ▸ Archura ▸ UV Mask Painter**: save after every stroke, undo memory, Mask View opacity and brush spacing.

---

## 10. Troubleshooting
| Problem | Fix |
|---|---|
| "This mesh has no UV1" | The mask uses a UV set the mesh doesn't have. Pick another mask or change ⋯ ▸ UV Set |
| Can't use a mask in a material ("read-only") | The material comes from a package or a model file. Extract or duplicate it first |
| Values look washed out in the shader | The texture was switched to sRGB. Untick sRGB in its import settings (the Console warns about this) |
| Painting one rock also changes another | They share a mesh and a material. Give one its own material and mask |
| Runtime sampling returns false | The hit collider must be a MeshCollider with UVs, or a TerrainCollider |
| A stroke doesn't show on the object | No material uses the mask yet; Mask View shows it. Use ⋯ ▸ Use in Material |

**Support:** _add your support email / Discord link here before publishing._
