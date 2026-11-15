using System;
using System.Collections.Generic;
using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Editor.Geometry;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Painting
{
    /// <summary>Mouse-down to mouse-up. Feed surface hits; Dispose/End records one undo step.</summary>
    public sealed class MaskStroke : IDisposable
    {
        private readonly MeshSurface surface;
        private readonly int submesh;
        private readonly BrushSettings brush;
        private readonly MaskEdit edit;
        private readonly StrokeSampler sampler;
        private readonly MaterialPropertyBlock dabProperties = new MaterialPropertyBlock();
        private readonly List<Dab> dabs = new List<Dab>();

        public MaskDocument Document { get; }
        public bool IsActive { get; private set; } = true;

        public MaskStroke(MaskDocument document, MeshSurface surface, int submesh, BrushSettings brush)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            this.submesh = submesh;
            this.brush = (brush ?? throw new ArgumentNullException(nameof(brush))).Clone(); // settings are fixed for the stroke

            edit = new MaskEdit(document, surface, submesh);
            sampler = new StrokeSampler(this.brush.Radius * this.brush.Spacing);
        }

        public void AddPoint(in SurfaceHit hit, float pressure = 1f) => AddPoint(hit.Point, hit.Normal, pressure);

        public void AddPoint(Vector3 worldPosition, Vector3 worldNormal, float pressure = 1f)
        {
            if (!IsActive) throw new InvalidOperationException("The stroke has ended.");

            dabs.Clear();
            sampler.AddPoint(worldPosition, worldNormal, Mathf.Clamp01(pressure), dabs);
            if (dabs.Count == 0) return;

            PaintResources.SetUVChannel(surface.UVChannel);
            RectInt changed = default;
            bool anyPainted = false;
            foreach (Dab dab in dabs)
            {
                if (!surface.TryGetUVBounds(dab.Position, brush.Radius, out Rect uvBounds)) continue;

                RectInt dabRegion = edit.ToPixels(uvBounds, 1);
                DrawDab(dab, dabRegion);

                RectInt affected = edit.ToPixels(uvBounds, MaskEdit.Gutter);
                changed = anyPainted ? MaskEdit.Union(changed, affected) : affected;
                anyPainted = true;
            }

            if (anyPainted)
                edit.Composite(brush.Mode, brush.TargetValue, brush.Strength, brush.Channels, changed);
        }

        // Cursor left the surface; resume without connecting the gap.
        public void Lift() => sampler.Reset();

        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            edit.Commit(UndoName(brush.Mode));
        }

        public void Cancel()
        {
            if (!IsActive) return;
            IsActive = false;
            edit.Cancel();
        }

        public void Dispose() => End();

        private void DrawDab(Dab dab, RectInt region)
        {
            dabProperties.SetVector(PaintResources.BrushCenter, dab.Position);
            dabProperties.SetVector(PaintResources.BrushNormal, dab.Normal);
            dabProperties.SetFloat(PaintResources.BrushRadius, brush.Radius);
            dabProperties.SetFloat(PaintResources.BrushHardness, brush.Hardness);
            dabProperties.SetFloat(PaintResources.BrushPressure, dab.Pressure);
            edit.DrawCoverage(surface.Mesh, surface.LocalToWorld, submesh, PaintResources.CoveragePass, dabProperties, region);
        }

        private static string UndoName(BrushMode mode)
        {
            switch (mode)
            {
                case BrushMode.Erase: return "Erase Mask";
                case BrushMode.Smooth: return "Smooth Mask";
                default: return "Paint Mask";
            }
        }
    }
}
