# Shared sample content

- `Shaders/BuiltIn`, `Shaders/URP`, `Shaders/HDRP`: the **Archura Mask Blend** shader family.
  - URP/HDRP: a Shader Graph subgraph `Archura Mask Blend` (inputs: base layer, overlay layer, mask, channel, contrast) plus one graph per demo.
  - Built-in: a surface shader per demo with the same inputs.
- `Scripts/`: the samples assembly and helpers shared by several demos:
  - `DemoOrbitCamera`: simple orbit/zoom camera for showcase scenes.
  - `MaskReadout`: on-screen readout of the mask value under the cursor (uses `MaskSurface.TrySampleAt`).
- `Models/`, `Textures/`: CC0 or self-made assets used by more than one demo.
