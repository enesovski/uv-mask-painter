# Moss Walls (planned)

**Pitch:** select a wall, press P, paint moss into the corners. Seam-correct, even across modular pieces.

## Scene
A small ruined courtyard built from modular wall, pillar and arch pieces, with a few rocks and steps.

## Masks
| Mask | Channels | Used for |
|---|---|---|
| `<Piece> Overgrowth` | R `Moss`, G `Dirt`, B `Damage` | Moss layer, dirt darkening, cracked/chipped detail |

UV0, 1024² per piece. Each placed piece uses its **own material instance**, so instances of one mesh can look different
(masks are listed per mesh, and the material decides which one shows).

## Shader
- Triplanar moss and dirt layers blended by the mask channels, plus an upward-facing bias so moss favors tops.
- Damage swaps in a cracked normal and darker albedo.
- The mask property is tagged `[ArchuraMask]`, so the first stroke plugs the new mask in automatically.

## Runtime
None: this demo is about the artist workflow.

## Store media
- Hero GIF: the "select, P, paint" loop on a wall, with no setup visible.
- Seam close-up: a stroke across a UV seam on an arch stays continuous.
- Before/after slider of the courtyard.

## To do
- [ ] Modular pieces (self-made, UV0 with seams in visible places to show seam handling)
- [ ] Overgrowth shader (URP, HDRP, Built-in)
- [ ] Paint masks for every piece
- [ ] Lighting and camera path
