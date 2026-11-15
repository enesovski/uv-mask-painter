# UV Mask Painter (Archura)

Unity Editor tool for painting R/G/B/A data masks directly on meshes in the Scene view.
Asset Store product of Archura. Git root is this folder (inside `Assets/Archura/UVMaskPainter`).

## Principles
- **UX first, fewest steps.** Target flow: select renderer → press P → paint. No dialogs, no required components or colliders. Defaults are decided automatically; informational messages go to the Console, never into the panel.
- **Maintainable, not overengineered.** Small focused classes, SOLID where it pays off, no speculative abstraction.
- **Simple UI.** Everything lives in the Scene view overlay. No main window; small utilities (channel packing) open from the Assets menu.
- **Scope:** mask painting only. Color/texture painting is a future extension.
- Comments: short, and only where intent isn't obvious.

## Layout
```
Editor/Core/             Archura.UVMaskPainter.Editor   (engine, no UI)
  Geometry/   MeshSurface (render-mesh picking, BVH), UVIslands
  Targeting/  MaskBinding (renderer+slot+property), MaskBindings (options, [ArchuraMask] suggestion, bind), drawer
  Document/   MaskInfo (.meta metadata), MaskLibrary (find masks for a renderer), MaskDocument (GPU working copy),
              TextureIO, MaskFactory, ChannelPacker, MaskChannels
  Painting/   BrushSettings, StrokeSampler, MaskStroke, MaskFill, MaskEdit, PaintResources
  History/    PaintHistory + HistoryProxy (pixel undo bridged into Unity Undo)
Editor/UI/               Archura.UVMaskPainter.Editor.UI (depends on Core, never the reverse)
  Session/    PainterSession (masks, active document, live preview, autosave), MaskPainterSettings, UndoAutoSave
  SceneView/  MaskPainterTool (+ Mesh/Skinned variants), shortcuts, BrushAdjust (hold-key adjust), overlay, drawing
  Controls/   PainterPanel (overlay content: mask list, tools, brush), NewMaskForm, InlinePrompt
  Window/     ChannelPackWindow (Assets ▸ Archura ▸ Pack Channels)
  Settings/   Preferences provider
Editor/Shaders/          Hidden/Archura/UVMaskPainter/Paint, /Preview
Tests/Editor/            Archura.UVMaskPainter.Editor.Tests (EditMode)
```
Core namespaces follow folders (`Archura.UVMaskPainter.Editor.<Folder>`). The UI assembly uses a single `Archura.UVMaskPainter.Editor.UI` namespace, because a `SceneView` namespace would shadow `UnityEditor.SceneView`.

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

## Mask model
- **A mask is a texture with metadata** in its importer `userData` (`MaskInfo`: channel count 1–4, channel names, owner, UV set) plus the asset label `ArchuraMask`. Ordinary material textures are never listed.
- **Owner** (`MaskLibrary.OwnerKey`): the mesh asset (so instances sharing a mesh share masks), or the scene object for built-in/procedural meshes.
- **A renderer's masks** = masks used by its materials + masks owned by its mesh.
- **Material use is explicit** ("Use in Material"). Exception: a new mask fills an empty `[ArchuraMask]` property.
- **Channel count drives import:** 1 → SingleChannel R8, 2–3 → no alpha, 4 → RGBA. Files are always RGBA PNGs, so changing the count never loses data.
- Reimport can replace the Texture2D object, so compare masks by GUID (`MaskEntry.Is`), never by reference.

## UX flow
- **Enter paint mode:** select a renderer, then press P (or use the Tools overlay). The last mask used on that object opens; with none, the first stroke creates one with the last-used channel count.
- **Overlay:** title names the object. Mask list (click to switch, double-click to rename, right-click or ⋯ for options; ping and ⋯ show on the active/hovered row). "+ New" opens the new-mask form. Channel chips have a "+" chip to add a channel; right-click a chip to rename it or remove the last one.
- **Panel shows only controls.** Status is "✓ saved" (or "● unsaved" when auto-save is off). The only inline message is a blocking error (e.g. missing UV set), and it never hides the mask list. UV sets the mesh lacks are disabled in the menu.
- Fill mode hides Size/Opacity/Hardness. Mask View is forced on (and shown checked + disabled) for masks no material uses.
- **While painting:** a MaterialPropertyBlock shows the working RenderTexture on the renderer without touching the material asset. Masks no material uses are shown through Mask View. A save starts in the background after every stroke.
- **Brush keys (Terrain Tools style):** hold S (size), A (opacity = BrushSettings.Strength) or D (value) and move the mouse sideways.
- **Leaving paint mode:** documents stay open, so Ctrl+Z still works. `UndoAutoSave` saves the restored pixels.
- **Settings:** brush and view settings live in `UserSettings/ArchuraUVMaskPainter.asset` (ScriptableSingleton).
- **Shortcuts:** in-tool shortcuts use the `PaintModeShortcutContext`, so they only override Unity's keys while painting.

## Conventions and gotchas
- On D3D, UV-space vertex shaders must flip Y (`UNITY_UV_STARTS_AT_TOP`). Tests cover orientation, so keep them passing.
- Call `GpuReadback.Unbind(rt)` before releasing any RT, because Blit leaves it active.
- Unity matches component EditorTools by exact type, so `typeof(Renderer)` never activates. Use `MaskPainterTool.Activate()` and `IsActive`.
- Don't use `??` or `?.` on UnityEngine.Object values; they bypass Unity's destroyed-object check.
- Before saving, `MaskDocument` checks the file's modification time and reloads a file that changed on disk (unless the document has unsaved edits). `SaveAndCloseAll` runs before a script reload or quit.
- Never call Unity APIs from worker threads. Poll from `EditorApplication.update` instead.
- New masks: linear (sRGB off), all channels 0, resolution matches the material's main texture (default 1024), saved as `<Name>.png` next to the material (else the scene/prefab folder).
- Minimum Unity version is 2022.3 LTS. The dev project runs on Unity 6000.3 with URP. The shaders are pipeline-agnostic (CGPROGRAM, off-screen only).

## Testing
Run the EditMode tests in Test Runner (`Archura.UVMaskPainter.Editor.Tests`). They create and clean up `Assets/__UVMaskPainterTests`.

## Roadmap
- [x] Phase 0: layout, asmdefs, namespaces
- [x] Phase 1: engine (picking, GPU painting, documents/IO, undo, fills)
- [x] Phase 2: Scene tool (P to paint, overlay, shortcuts, cursor, auto target, first stroke creates the mask, autosave, live preview)
- [x] Phase 3: fill/clear/invert/resize, channel-packed export, preferences
- [x] Redesign: explicit masks (name, channel count, channel names), mask list with switch/rename/ping/delete, hold-key brush adjust, window and v1 removed
- [ ] Next: value-under-cursor readout, Make Unique material, UV/2D view, pen-pressure options, PainterSession tests
- [ ] Phase 4: samples per render pipeline, docs, Asset Store polish
