using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>Preferences ▸ Archura ▸ UV Mask Painter.</summary>
    internal static class MaskPainterPreferences
    {
        [SettingsProvider]
        private static SettingsProvider Create() => new SettingsProvider("Preferences/Archura/UV Mask Painter", SettingsScope.User)
        {
            keywords = new[] { "mask", "paint", "uv", "archura" },
            guiHandler = _ =>
            {
                MaskPainterSettings settings = MaskPainterSettings.instance;
                EditorGUIUtility.labelWidth = 180;

                bool autoSave = EditorGUILayout.Toggle(new GUIContent("Save After Every Stroke", "Writes the mask file in the background after each stroke."), settings.AutoSave);
                if (autoSave != settings.AutoSave) settings.AutoSave = autoSave;

                int undo = EditorGUILayout.IntSlider(new GUIContent("Undo Memory (MB)", "GPU memory kept for mask undo steps."), settings.UndoMemoryMB, 64, 4096);
                if (undo != settings.UndoMemoryMB) settings.UndoMemoryMB = undo;

                float opacity = EditorGUILayout.Slider("Mask View Opacity", settings.MaskOpacity, 0f, 1f);
                if (!Mathf.Approximately(opacity, settings.MaskOpacity)) settings.MaskOpacity = opacity;

                float spacing = EditorGUILayout.Slider(new GUIContent("Brush Spacing", "Distance between dabs, as a fraction of the radius."), settings.Brush.Spacing, 0.02f, 1f);
                if (!Mathf.Approximately(spacing, settings.Brush.Spacing))
                {
                    settings.Brush.Spacing = spacing;
                    settings.NotifyChanged();
                }

                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Shortcuts can be changed in Edit ▸ Shortcuts ▸ Archura. Channel packing: right-click textures ▸ Archura ▸ Pack Channels.", MessageType.None);
            }
        };
    }
}
