# UV Mask Painter (Archura)

Unity Editor tool for painting R/G/B/A data masks directly on meshes in the Scene view.
Asset Store product of Archura. Git root is this folder (inside `Assets/Archura/UVMaskPainter`).

## Principles
- **UX first, fewest steps.** Target flow: select renderer → press P → paint. No dialogs, no required components or colliders. Defaults are decided automatically and shown in a toast with Change/Undo.
- **Maintainable, not overengineered.** Small focused classes, SOLID where it pays off, no speculative abstraction.
- **Simple UI.** Prefer Scene view overlays plus a plain window over complex custom UI.
- **Scope:** mask painting only. Color/texture painting is a future extension.
- Comments: short, and only where intent isn't obvious.

## Layout
```
Runtime/                 Archura.UVMaskPainter          (only v1 legacy types, kept for migration)
Editor/Core/             Archura.UVMaskPainter.Editor   (engine, no UI)
  Geometry/   MeshSurface (render-mesh picking, BVH), UVIslands
  Targeting/  PaintTarget, TargetResolver, [ArchuraMask] drawer
  Document/   MaskDocument (GPU working copy), TextureIO, MaskFactory, MaskChannels
  Painting/   BrushSettings, StrokeSampler, MaskStroke, MaskFill, MaskEdit, PaintResources
  History/    PaintHistory + HistoryProxy (pixel undo bridged into Unity Undo)
  Legacy/     v1 MaskAsset → PNG converter
Editor/UI/               (Phase 2) Archura.UVMaskPainter.Editor.UI, depends on Core, never the reverse
Editor/Shaders/          Hidden/Archura/UVMaskPainter/Paint
Tests/Editor/            Archura.UVMaskPainter.Editor.Tests (EditMode)
Samples/LegacyDemo/      v1 demo (replace in Phase 4)
Documentation/           v1 manual
```
Namespaces follow folders: `Archura.UVMaskPainter.Editor.<Folder>`.

## How the engine works
- **Picking:** CPU raycast on the render mesh through a BVH. Skinned meshes are baked once per surface.
- **Painting:** the mesh is drawn in UV space (`uv*2-1` as clip position) and the brush is measured in world space, which keeps strokes seam-correct and island-safe.
  - Dabs go into an R8 coverage buffer with Max blending.
  - The composite step does `lerp(snapshot, value, coverage*strength)` using ColorMask channels. Strength therefore caps opacity within a stroke.
  - Every pass is scissored to the affected pixels.
  - Dilation pads seams by 3 texels.
- **Documents:** one per texture GUID, holding a linear RenderTexture.
  - Loaded from the source file (PNG/JPG/EXR), never from the compressed import.
  - Formats that can't be written are saved as a PNG copy.
  - `SaveInBackground` uses async readback, then a worker-thread encode, then an atomic replace via `Temp/`.
- **Undo:** each step holds the changed region in a GPU RenderTexture. `HistoryProxy.revision` is recorded with `Undo.RecordObject`, and `undoRedoPerformed` swaps the regions back.

## Conventions and gotchas
- On D3D, UV-space vertex shaders must flip Y (`UNITY_UV_STARTS_AT_TOP`). Tests cover orientation, so keep them passing.
- Call `GpuReadback.Unbind(rt)` before releasing any RT, because Blit leaves it active.
- Never call Unity APIs from worker threads. Poll from `EditorApplication.update` instead.
- New masks: linear (sRGB off), all channels 0, resolution matches the material's main texture (default 1024), saved as `<Material>_<Property>.png` next to the material.
- Minimum Unity version is 2022.3 LTS. The dev project runs on Unity 6000.3 with URP. The shaders are pipeline-agnostic (CGPROGRAM, off-screen only).

## Testing
Run the EditMode tests in Test Runner (`Archura.UVMaskPainter.Editor.Tests`). They create and clean up `Assets/__UVMaskPainterTests`.

## Roadmap
- [x] Phase 0: layout, asmdefs, namespaces, legacy isolation
- [x] Phase 1: engine (picking, GPU painting, documents/IO, undo, fills, converter)
- [ ] Phase 2: Scene tool (P to paint, overlay, shortcuts, cursor, auto target, first stroke creates the mask, autosave, live preview via MaterialPropertyBlock)
- [ ] Phase 3: simple window (mask ops, presets, export/channel packing), settings
- [ ] Phase 4: samples per render pipeline, docs, Asset Store polish
