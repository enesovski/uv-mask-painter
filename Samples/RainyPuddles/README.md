# Rainy Puddles (planned)

**Pitch:** paint where the street is wet and where water pools, and watch reflections and ripples follow.

## Scene
A short street corner: road, curb, sidewalk, a few props (bench, bin, lamp post), overcast lighting and a reflection probe.

## Masks
| Mask | Channels | Used for |
|---|---|---|
| `Street Wetness` | R `Wet`, G `Puddle` | R darkens albedo and raises smoothness; G adds standing water (flat normal, max smoothness, ripples) |

UV0, 2048² for the road, 1024² for props.

## Shader (`Shaders/`, plus Built-in/HDRP variants in `Shared/Shaders`)
- Wet: `albedo *= lerp(1, 0.6, wet)`, `smoothness = lerp(base, 0.9, wet)`.
- Puddle: blend toward a flat normal plus animated ripple normals, with a height-based edge so puddles sit in the cracks.
- A global `_RainAmount` slider in the demo UI scales both channels from dry to soaked.

## Runtime (`Scripts/`)
- `PuddleFootsteps`: on each footstep, raycast down and call `MaskSurface.TrySampleAt(hit, "Puddle", out float puddle)` to pick splash or dry sounds and spawn a splash VFX.

## Store media
- GIF: paint a puddle into a crack, then drag the rain slider from dry to soaked.
- Screenshot: the overlay with the `Wet` and `Puddle` channels named.

## To do
- [ ] Street meshes (self-made, UV0 non-overlapping)
- [ ] Wetness shader (URP graph, HDRP graph, Built-in surface shader)
- [ ] Paint the masks and save them in `Masks/`
- [ ] `PuddleFootsteps` and a splash effect
- [ ] Lighting, post-processing, camera path for the video
