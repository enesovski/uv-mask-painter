# Character Grime (planned)

**Pitch:** paint dirt, blood and wetness on a skinned character in any pose, then drive it from gameplay.

## Scene
A posed and animated humanoid on a turntable, with three buttons in the demo UI: Clean, Muddy, Battle-worn.

## Masks
| Mask | Channels | Used for |
|---|---|---|
| `<Character> Grime` | R `Dirt`, G `Blood`, B `Wet` | Grime layers on body and clothes |

UV0, 2048². Painted on the SkinnedMeshRenderer (the painter bakes the current pose, so painting works mid-animation).

## Shader
- Dirt and blood detail textures blended by channel, each multiplied by a global amount (`_DirtAmount`, `_BloodAmount`, `_WetAmount`).
- The mask decides *where*; gameplay decides *how much*.

## Runtime (`Scripts/`)
- `GrimeController`: ramps the amounts over time or on events (fall in mud, take a hit, walk in rain).

## Store media
- GIF: paint blood on a sleeve while the character is posed, then hit Play and watch it fade in on hits.

## To do
- [ ] Character model and animation (CC0, e.g. a rigged humanoid; list it in `Third-Party Notices.txt`)
- [ ] Grime shader (URP, HDRP, Built-in)
- [ ] Paint the grime mask
- [ ] `GrimeController` and demo UI
