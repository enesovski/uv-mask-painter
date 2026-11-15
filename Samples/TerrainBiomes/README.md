# Terrain Biomes (planned)

**Pitch:** paint gameplay regions straight onto a Unity Terrain (biomes, spawn zones, no-build areas) and read them in code.

## Scene
A 500 × 500 m terrain with hills and a river, a few prefab props, and a simple player capsule.

## Masks
| Mask | Channels | Used for |
|---|---|---|
| `Biomes` | R `Forest`, G `Dry`, B `Swamp`, A `NoSpawn` | Prop density per biome, ambience, no-spawn zone |

UV0 (terrain UV = normalized position), 1024². Saved next to the TerrainData in `Terrain/`.

## Visuals
Terrain layers are painted with Unity's own terrain tools as usual. The biome mask is **data**; Mask View shows it while editing.
Optional: a tint overlay shader that shows biomes in a debug view.

## Runtime (`Scripts/`)
- `BiomeSpawner`: on start, scatters props using `MaskSampler.Sample(MaskSampler.TerrainUV(terrain, position))` as density.
- `BiomeHud`: shows the biome under the player (`MaskSurface.TrySample(worldPosition, out Color biome)`).
- `BiomeAmbience`: crossfades ambience loops by channel weights.

## Store media
- GIF: paint a swamp region on the hills, press Play, and props and ambience follow.
- Screenshot: code snippet next to the painted mask.

## To do
- [ ] Terrain, terrain layers and props (self-made or CC0)
- [ ] Paint the `Biomes` mask
- [ ] `BiomeSpawner`, `BiomeHud`, `BiomeAmbience`
- [ ] Player controller (reuse a minimal one in `Shared/Scripts`)
