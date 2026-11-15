using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>Compact panel shown in the Scene view only while painting.</summary>
    [Overlay(typeof(SceneView), "archura-uv-mask-painter", "UV Mask Painter", true)]
    internal sealed class MaskPainterOverlay : Overlay, ITransientOverlay
    {
        public bool visible => PainterSession.Active != null;

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingBottom = 4;
            root.Add(new PainterPanel());
            return root;
        }
    }
}
