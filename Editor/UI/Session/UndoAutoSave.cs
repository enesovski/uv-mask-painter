using Archura.UVMaskPainter.Editor.History;
using UnityEditor;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>Saves masks changed by Ctrl+Z / Ctrl+Y, also after leaving paint mode.</summary>
    [InitializeOnLoad]
    internal static class UndoAutoSave
    {
        static UndoAutoSave()
        {
            PaintHistory.Restored += document =>
            {
                if (MaskPainterSettings.instance.AutoSave && document.AssetPath != null)
                    document.SaveInBackground();
            };
        }
    }
}
