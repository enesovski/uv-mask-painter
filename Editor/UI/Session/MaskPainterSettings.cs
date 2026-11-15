using System;
using Archura.UVMaskPainter.Editor.Painting;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.UI
{
    public enum PaintTool { Brush, Fill }

    /// <summary>Per-user, per-project settings (UserSettings/, not version controlled). Survives reloads.</summary>
    [FilePath("UserSettings/ArchuraUVMaskPainter.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MaskPainterSettings : ScriptableSingleton<MaskPainterSettings>
    {
        [SerializeField] private BrushSettings brush = new BrushSettings();
        [SerializeField] private PaintTool tool = PaintTool.Brush;
        [SerializeField] private bool showMask = true;
        [SerializeField] private float maskOpacity = 0.6f;
        [SerializeField] private bool autoSave = true;
        [SerializeField] private int undoMemoryMB = 512;
        [SerializeField] private int newMaskChannels = 1;

        public static event Action Changed;
        private bool saveScheduled;

        public BrushSettings Brush => brush;
        public PaintTool Tool { get => tool; set { tool = value; NotifyChanged(); } }
        public bool ShowMask { get => showMask; set { showMask = value; NotifyChanged(); } }
        public float MaskOpacity { get => maskOpacity; set { maskOpacity = Mathf.Clamp01(value); NotifyChanged(); } }
        public bool AutoSave { get => autoSave; set { autoSave = value; NotifyChanged(); } }
        public int UndoMemoryMB { get => undoMemoryMB; set { undoMemoryMB = Mathf.Clamp(value, 64, 4096); NotifyChanged(); } }
        /// <summary>Channel count for new masks; remembers the last choice.</summary>
        public int NewMaskChannels { get => Mathf.Clamp(newMaskChannels, 1, 4); set { newMaskChannels = Mathf.Clamp(value, 1, 4); NotifyChanged(); } }

        /// <summary>Call after changing <see cref="Brush"/> directly. Saving to disk is batched.</summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
            SceneView.RepaintAll();
            if (saveScheduled) return;
            saveScheduled = true;
            EditorApplication.delayCall += () =>
            {
                saveScheduled = false;
                Save(true);
            };
        }
    }
}
