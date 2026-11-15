using UnityEngine;

namespace Archura.UVMaskPainter.Editor.History
{
    /// <summary>Hidden object whose revision Unity's Undo tracks; <see cref="PaintHistory"/> syncs pixels to it.</summary>
    internal sealed class HistoryProxy : ScriptableObject
    {
        public int revision;
    }
}
