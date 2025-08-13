# UV Mask Painter for Unity

**UV Mask Painter** is a Unity Editor extension that lets you paint directly onto mask textures in the Scene view using your object's UVs.  
It supports multi-channel masks (R, RG, RGBA), adjustable brush settings, and real-time preview — perfect for workflows like vertex blending, material masking, or texture-based effects.

---

## Features
- Paint, Erase, Smooth, and Fill tools.
- Target specific channels (R, G, B, A) based on your mask asset.
- Adjustable radius, strength, and hardness.
- Live brush preview in the Scene view.
- Channel preview in the editor.
- Export masks to PNG directly from the UI.
- Create and assign mask assets from the inspector.

---

## Requirements
- Unity 2021.3 or newer (Editor-only tool).
- A **MeshCollider** on the paintable object (for UV raycasting).
- `MaskPaintable` component with an assigned **MaskAsset**.

---

## Quick Start
1. **Add a MeshCollider** to your object (required for UV painting).
2. **Add the `MaskPaintable` component** to your GameObject.
3. **Create a mask asset**:
   - Menu: `Tools → Mask Painter → Create Mask Asset`
   - Or use **Create & Assign Mask Asset** in the `MaskPaintable` inspector.
4. **Open the Mask Painter Window**:
   - Menu: `Tools → Mask Painter → Mask Painter Window`
5. Select your object → Click **Start Painting** in the window.
6. Paint directly in the Scene view.

---

## Shortcuts
- **Ctrl + Mouse Wheel** → Adjust radius
- **Shift + Mouse Wheel** → Adjust strength
- **Ctrl + Right-Drag** → Horizontal = radius, Vertical = hardness
- **1, 2, 3, 4** → Switch tools (Paint, Erase, Smooth, Fill)

---

## License
MIT License – free to use and modify.

---
