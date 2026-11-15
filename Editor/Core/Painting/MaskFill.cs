using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Editor.Geometry;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Painting
{
    public static class MaskFill
    {
        public static void FillAll(MaskDocument document, MaskChannels channels, float value)
        {
            var edit = new MaskEdit(document, null, 0);
            edit.FillCoverage();
            edit.Composite(BrushMode.Paint, Mathf.Clamp01(value), 1f, channels, edit.FullRegion);
            edit.Commit("Fill Mask");
        }

        public static void FillIsland(MaskDocument document, MeshSurface surface, in SurfaceHit hit, MaskChannels channels, float value)
        {
            int island = surface.GetIsland(hit.Triangle);
            Mesh islandMesh = surface.CreateIslandMesh(island, hit.Submesh, out Rect uvBounds);
            try
            {
                var edit = new MaskEdit(document, surface, hit.Submesh);
                PaintResources.SetUVChannel(0); // the island mesh carries the surface UVs in UV0
                edit.DrawCoverage(islandMesh, surface.LocalToWorld, 0, PaintResources.OccupancyPass, null, edit.FullRegion);
                edit.Composite(BrushMode.Paint, Mathf.Clamp01(value), 1f, channels, edit.ToPixels(uvBounds, MaskEdit.Gutter));
                edit.Commit("Fill UV Island");
            }
            finally
            {
                Object.DestroyImmediate(islandMesh);
            }
        }
    }
}
