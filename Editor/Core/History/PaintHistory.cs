using System.Collections.Generic;
using Archura.UVMaskPainter.Editor.Document;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.History
{
    /// <summary>
    /// Pixel undo bridged into Unity's Undo via <see cref="HistoryProxy"/>'s revision.
    /// Steps store only the changed region, on the GPU (no stalls).
    /// </summary>
    public static class PaintHistory
    {
        private sealed class Step
        {
            public MaskDocument Document;
            public RectInt Region;
            public RenderTexture Pixels;   // the "other" state; swapped on every undo/redo
            public int Revision;
            public long Bytes;
        }

        public static long MemoryBudgetBytes { get; set; } = 512L * 1024 * 1024;

        private static readonly List<Step> steps = new List<Step>();
        private static HistoryProxy proxy;
        private static int appliedRevision;
        private static long usedBytes;

        public static int StepCount => steps.Count;
        public static long UsedBytes => usedBytes;

        static PaintHistory()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }

        public static void Record(MaskDocument document, RectInt region, RenderTexture before, string name)
        {
            EnsureProxy();

            // New step drops the redo branch, like Unity.
            for (int i = steps.Count - 1; i >= 0; i--)
                if (steps[i].Revision > appliedRevision) RemoveAt(i);

            RenderTexture pixels = CreateRegionTexture(document, region);
            Graphics.CopyTexture(before, 0, 0, region.x, region.y, region.width, region.height, pixels, 0, 0, 0, 0);

            var step = new Step
            {
                Document = document,
                Region = region,
                Pixels = pixels,
                Revision = appliedRevision + 1,
                Bytes = (long)region.width * region.height * (document.IsHdr ? 8 : 4)
            };

            Undo.IncrementCurrentGroup();   // every edit is its own undo step
            Undo.RecordObject(proxy, name);
            proxy.revision = step.Revision;
            appliedRevision = step.Revision;

            steps.Add(step);
            usedBytes += step.Bytes;
            while (usedBytes > MemoryBudgetBytes && steps.Count > 1) RemoveAt(0);
        }

        public static void Clear()
        {
            for (int i = steps.Count - 1; i >= 0; i--) RemoveAt(i);
        }

        private static void EnsureProxy()
        {
            if (proxy != null) return;

            // Reuse the proxy surviving a script reload so existing undo entries stay valid.
            HistoryProxy[] existing = Resources.FindObjectsOfTypeAll<HistoryProxy>();
            proxy = existing.Length > 0 ? existing[0] : ScriptableObject.CreateInstance<HistoryProxy>();
            proxy.hideFlags = HideFlags.HideAndDontSave;
            appliedRevision = proxy.revision;

            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private static void OnUndoRedo()
        {
            if (proxy == null) return;
            int target = proxy.revision;

            while (appliedRevision > target)
            {
                Swap(appliedRevision);
                appliedRevision--;
            }
            while (appliedRevision < target)
            {
                appliedRevision++;
                Swap(appliedRevision);
            }
        }

        private static void Swap(int revision)
        {
            Step step = steps.Find(s => s.Revision == revision);
            if (step == null || step.Document == null || step.Document.IsDisposed) return;

            RectInt r = step.Region;
            RenderTexture documentTexture = step.Document.Texture;
            RenderTexture current = CreateRegionTexture(step.Document, r);
            Graphics.CopyTexture(documentTexture, 0, 0, r.x, r.y, r.width, r.height, current, 0, 0, 0, 0);
            Graphics.CopyTexture(step.Pixels, 0, 0, 0, 0, r.width, r.height, documentTexture, 0, 0, r.x, r.y);

            Destroy(step.Pixels);
            step.Pixels = current;
            step.Document.MarkDirty();
        }

        private static RenderTexture CreateRegionTexture(MaskDocument document, RectInt region)
        {
            var texture = new RenderTexture(region.width, region.height, 0, document.Texture.format, RenderTextureReadWrite.Linear)
            {
                name = "UV Mask Painter Undo",
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        private static void RemoveAt(int index)
        {
            usedBytes -= steps[index].Bytes;
            Destroy(steps[index].Pixels);
            steps.RemoveAt(index);
        }

        private static void Destroy(RenderTexture texture)
        {
            if (texture == null) return;
            GpuReadback.Unbind(texture);
            texture.Release();
            Object.DestroyImmediate(texture);
        }
    }
}
