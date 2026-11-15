using Archura.UVMaskPainter.Editor.Painting;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.UI
{
    internal enum BrushProperty { None, Size, Opacity, Value }

    /// <summary>
    /// Terrain Tools style adjustment: hold a key and move the mouse sideways. The cursor stays where
    /// the adjustment started so the ring shows the result in place.
    /// </summary>
    internal static class BrushAdjust
    {
        private const float PixelsPerDoubling = 120f;   // size
        private const float PixelsPerUnit = 250f;       // opacity, value

        public static BrushProperty Property { get; private set; }
        public static bool IsActive => Property != BrushProperty.None;

        private static bool anchored;
        private static float anchorX;
        private static float startValue;

        private static MaskPainterSettings Settings => MaskPainterSettings.instance;

        public static void Begin(BrushProperty property)
        {
            Property = property;
            anchored = false;
            SceneView.RepaintAll();
        }

        public static void End(BrushProperty property)
        {
            if (Property != property) return;
            Property = BrushProperty.None;
            SceneView.RepaintAll();
        }

        /// <summary>Call on mouse move/drag while active. The first call only sets the anchor.</summary>
        public static void Track(Vector2 mousePosition)
        {
            if (!IsActive) return;
            BrushSettings brush = Settings.Brush;
            if (!anchored)
            {
                anchored = true;
                anchorX = mousePosition.x;
                startValue = Property == BrushProperty.Size ? brush.Radius : Property == BrushProperty.Opacity ? brush.Strength : brush.Value;
                return;
            }

            float delta = mousePosition.x - anchorX;
            switch (Property)
            {
                case BrushProperty.Size: brush.Radius = startValue * Mathf.Pow(2f, delta / PixelsPerDoubling); break;
                case BrushProperty.Opacity: brush.Strength = startValue + delta / PixelsPerUnit; break;
                case BrushProperty.Value: brush.Value = startValue + delta / PixelsPerUnit; break;
            }
            Settings.NotifyChanged();
        }

        public static string Label()
        {
            BrushSettings brush = Settings.Brush;
            switch (Property)
            {
                case BrushProperty.Size: return "Size  " + FormatDistance(brush.Radius);
                case BrushProperty.Opacity: return $"Opacity  {brush.Strength:0.00}";
                case BrushProperty.Value: return $"Value  {brush.Value:0.00}";
                default: return null;
            }
        }

        public static string FormatDistance(float meters) =>
            meters >= 1f ? $"{meters:0.##} m" : meters >= 0.01f ? $"{meters * 100f:0.#} cm" : $"{meters * 1000f:0.#} mm";
    }
}
