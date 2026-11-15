using System;
using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Editor.Geometry;
using Archura.UVMaskPainter.Editor.History;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Archura.UVMaskPainter.Editor.Painting
{
    /// <summary>
    /// One undoable edit. Draws into a coverage buffer and composites it onto the pre-edit snapshot,
    /// so strength caps opacity within a stroke. All passes are scissored to the affected pixels.
    /// </summary>
    internal enum CompositeOp { Blend, Smooth, Invert }

    internal sealed class MaskEdit : IDisposable
    {
        // Seam dilation (3 texels) + filtering margin.
        public const int Gutter = 4;

        private readonly MaskDocument document;
        private readonly RenderTexture snapshot;
        private readonly RenderTexture occupancy;  // null when no surface (whole-texture edits)
        private readonly RenderTexture dilated;
        private readonly CommandBuffer commands = new CommandBuffer { name = "UV Mask Painter" };
        private readonly MaterialPropertyBlock passProperties = new MaterialPropertyBlock();
        private RectInt dirty;
        private bool hasDirty;
        private bool finished;

        public RenderTexture Coverage { get; }
        public RectInt FullRegion => new RectInt(0, 0, document.Width, document.Height);

        public MaskEdit(MaskDocument document, MeshSurface surface, int submesh)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));

            snapshot = RenderTexture.GetTemporary(document.Texture.descriptor);
            Graphics.CopyTexture(document.Texture, snapshot);

            Coverage = CreateBuffer();
            dilated = CreateBuffer();
            Clear(Coverage, Color.clear);

            if (surface != null)
            {
                occupancy = CreateBuffer();
                Clear(occupancy, Color.clear);
                PaintResources.SetUVChannel(surface.UVChannel);
                Draw(occupancy, surface.Mesh, surface.LocalToWorld, submesh, PaintResources.OccupancyPass, null, FullRegion);
            }
        }

        public RectInt ToPixels(Rect uv, int margin)
        {
            int xMin = Mathf.Clamp(Mathf.FloorToInt(uv.xMin * document.Width) - margin, 0, document.Width);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(uv.yMin * document.Height) - margin, 0, document.Height);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(uv.xMax * document.Width) + margin, 0, document.Width);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(uv.yMax * document.Height) + margin, 0, document.Height);
            return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
        }

        public void DrawCoverage(Mesh mesh, Matrix4x4 localToWorld, int submesh, int pass, MaterialPropertyBlock properties, RectInt region)
        {
            Draw(Coverage, mesh, localToWorld, submesh, pass, properties, region);
        }

        public void FillCoverage() => Clear(Coverage, Color.white);

        public void Composite(CompositeOp op, float value, float strength, MaskChannels channels, RectInt region)
        {
            if (region.width <= 0 || region.height <= 0) return;

            Material material = PaintResources.Material;
            material.SetVector(PaintResources.TexelSize, new Vector4(1f / document.Width, 1f / document.Height, document.Width, document.Height));
            material.SetVector(PaintResources.Value, new Vector4(value, value, value, value));
            material.SetFloat(PaintResources.Strength, strength);
            material.SetFloat(PaintResources.ColorMask, (float)channels.ToColorWriteMask());

            RenderTexture coverage = Coverage;
            if (occupancy != null)
            {
                passProperties.Clear();
                passProperties.SetTexture(PaintResources.MainTex, Coverage);
                passProperties.SetTexture(PaintResources.OccupancyTex, occupancy);
                Draw(dilated, PaintResources.Quad, Matrix4x4.identity, 0, PaintResources.DilatePass, passProperties, region);
                coverage = dilated;
            }

            passProperties.Clear();
            passProperties.SetTexture(PaintResources.MainTex, snapshot);
            passProperties.SetTexture(PaintResources.CoverageTex, coverage);
            int pass = op == CompositeOp.Smooth ? PaintResources.CompositeSmoothPass
                     : op == CompositeOp.Invert ? PaintResources.CompositeInvertPass
                     : PaintResources.CompositePass;
            Draw(document.Texture, PaintResources.Quad, Matrix4x4.identity, 0, pass, passProperties, region);

            dirty = hasDirty ? Union(dirty, region) : region;
            hasDirty = true;
            document.MarkDirty();
        }

        public void Commit(string undoName)
        {
            if (finished) return;
            if (hasDirty) PaintHistory.Record(document, dirty, snapshot, undoName);
            Release();
        }

        public void Cancel()
        {
            if (finished) return;
            Graphics.CopyTexture(snapshot, document.Texture);
            document.MarkDirty();
            Release();
        }

        public void Dispose() => Commit("Paint Mask");

        public static RectInt Union(RectInt a, RectInt b)
        {
            int xMin = Mathf.Min(a.xMin, b.xMin);
            int yMin = Mathf.Min(a.yMin, b.yMin);
            return new RectInt(xMin, yMin, Mathf.Max(a.xMax, b.xMax) - xMin, Mathf.Max(a.yMax, b.yMax) - yMin);
        }

        private void Draw(RenderTexture target, Mesh mesh, Matrix4x4 matrix, int submesh, int pass, MaterialPropertyBlock properties, RectInt region)
        {
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.EnableScissorRect(new Rect(region.x, region.y, region.width, region.height));
            // Negative submesh: the whole mesh (mask not tied to one material slot).
            if (submesh >= 0) commands.DrawMesh(mesh, matrix, PaintResources.Material, submesh, pass, properties);
            else
                for (int s = 0; s < mesh.subMeshCount; s++)
                    commands.DrawMesh(mesh, matrix, PaintResources.Material, s, pass, properties);
            commands.DisableScissorRect();
            Graphics.ExecuteCommandBuffer(commands);
        }

        private void Clear(RenderTexture target, Color color)
        {
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(false, true, color);
            Graphics.ExecuteCommandBuffer(commands);
        }

        private void Release()
        {
            finished = true;
            GpuReadback.Unbind(snapshot);
            RenderTexture.ReleaseTemporary(snapshot);
            DestroyBuffer(Coverage);
            DestroyBuffer(dilated);
            DestroyBuffer(occupancy);
            commands.Release();
        }

        private RenderTexture CreateBuffer()
        {
            var buffer = new RenderTexture(document.Width, document.Height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
            {
                name = "UV Mask Painter Buffer",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            buffer.Create();
            return buffer;
        }

        private static void DestroyBuffer(RenderTexture buffer)
        {
            if (buffer == null) return;
            GpuReadback.Unbind(buffer);
            buffer.Release();
            Object.DestroyImmediate(buffer);
        }
    }
}
