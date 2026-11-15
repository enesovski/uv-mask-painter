using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Editor.Geometry;
using Archura.UVMaskPainter.Editor.History;
using Archura.UVMaskPainter.Editor.Painting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Tests
{
    /// <summary>GPU painting on Unity's quad (world XY = UV - 0.5), using a 64x64 in-memory mask.</summary>
    public class PaintingTests
    {
        private const float Tolerance = 0.02f;

        private MeshRenderer quad;
        private MeshSurface surface;
        private MaskDocument document;

        [SetUp]
        public void SetUp()
        {
            PaintHistory.Clear();
            quad = TestUtility.CreateQuad();
            Assert.IsTrue(MeshSurface.TryCreate(quad, 0, out surface, out string error), error);
            document = MaskDocument.CreateUnsaved(64, 64, Color.clear);
        }

        [TearDown]
        public void TearDown()
        {
            document.Dispose();
            surface.Dispose();
            Object.DestroyImmediate(quad.gameObject);
            PaintHistory.Clear();
        }

        [Test]
        public void Raycast_ReturnsTheQuadUV()
        {
            SurfaceHit hit = HitAt(0.25f, 0.75f);
            Assert.AreEqual(0.25f, hit.UV.x, 1e-4f);
            Assert.AreEqual(0.75f, hit.UV.y, 1e-4f);
        }

        [Test]
        public void SingleClick_PaintsADab()
        {
            Paint(Brush(), 0.5f, 0.5f);

            Assert.AreEqual(1f, TestUtility.Texel(document, 0.5f, 0.5f).r, Tolerance);
            Assert.AreEqual(0f, TestUtility.Texel(document, 0.9f, 0.9f).r, Tolerance);
        }

        [Test]
        public void Dab_LandsAtTheHitUV_NotFlipped()
        {
            Paint(Brush(), 0.25f, 0.75f);

            Assert.Greater(TestUtility.Texel(document, 0.25f, 0.75f).r, 0.9f);
            Assert.Less(TestUtility.Texel(document, 0.25f, 0.25f).r, 0.01f, "Vertically flipped");
            Assert.Less(TestUtility.Texel(document, 0.75f, 0.75f).r, 0.01f, "Horizontally flipped");
        }

        [Test]
        public void Strength_IsTheMaximumOpacityOfAStroke()
        {
            BrushSettings brush = Brush();
            brush.Strength = 0.5f;

            using (var stroke = new MaskStroke(document, surface, 0, brush))
            {
                for (int i = 0; i < 10; i++)
                {
                    stroke.AddPoint(HitAt(0.5f, 0.5f));
                    stroke.AddPoint(HitAt(0.52f, 0.5f));
                }
            }

            Assert.AreEqual(0.5f, TestUtility.Texel(document, 0.5f, 0.5f).r, Tolerance);
        }

        [Test]
        public void Hardness_ControlsTheFalloff()
        {
            BrushSettings hard = Brush(radius: 0.2f);
            hard.Hardness = 1f;
            Paint(hard, 0.5f, 0.5f);
            Assert.AreEqual(1f, TestUtility.Texel(document, 0.65f, 0.5f).r, Tolerance, "Hard brush should be solid near its edge");

            MaskFill.FillAll(document, MaskChannels.R, 0f);

            BrushSettings soft = Brush(radius: 0.2f);
            soft.Hardness = 0f;
            Paint(soft, 0.5f, 0.5f);
            float nearEdge = TestUtility.Texel(document, 0.65f, 0.5f).r;
            Assert.Greater(nearEdge, 0.01f);
            Assert.Less(nearEdge, 0.5f, "Soft brush should fade towards its edge");
        }

        [Test]
        public void OnlySelectedChannelsAreWritten()
        {
            BrushSettings brush = Brush();
            brush.Channels = MaskChannels.G | MaskChannels.A;
            Paint(brush, 0.5f, 0.5f);

            Color texel = TestUtility.Texel(document, 0.5f, 0.5f);
            Assert.AreEqual(0f, texel.r, Tolerance);
            Assert.AreEqual(1f, texel.g, Tolerance);
            Assert.AreEqual(0f, texel.b, Tolerance);
            Assert.AreEqual(1f, texel.a, Tolerance);
        }

        [Test]
        public void PaintValue_IsWrittenExactly()
        {
            BrushSettings brush = Brush();
            brush.Value = 0.5f;
            Paint(brush, 0.5f, 0.5f);

            Assert.AreEqual(0.5f, TestUtility.Texel(document, 0.5f, 0.5f).r, Tolerance);
        }

        [Test]
        public void Erase_PaintsZero()
        {
            MaskFill.FillAll(document, MaskChannels.R, 1f);
            BrushSettings brush = Brush();
            brush.Mode = BrushMode.Erase;
            Paint(brush, 0.5f, 0.5f);

            Assert.AreEqual(0f, TestUtility.Texel(document, 0.5f, 0.5f).r, Tolerance);
            Assert.AreEqual(1f, TestUtility.Texel(document, 0.9f, 0.9f).r, Tolerance);
        }

        [Test]
        public void Smooth_BlursAHardEdge()
        {
            BrushSettings hard = Brush(radius: 0.15f);
            hard.Hardness = 1f;
            Paint(hard, 0.5f, 0.5f);
            float edgeBefore = TestUtility.Texel(document, 0.5f + 0.15f, 0.5f).r;

            BrushSettings smooth = Brush(radius: 0.3f);
            smooth.Mode = BrushMode.Smooth;
            Paint(smooth, 0.5f, 0.5f);
            float edgeAfter = TestUtility.Texel(document, 0.5f + 0.15f, 0.5f).r;

            Assert.Greater(Mathf.Abs(edgeBefore - edgeAfter), 1e-3f);
        }

        [Test]
        public void UnityUndoAndRedo_RestorePixels()
        {
            Undo.IncrementCurrentGroup();
            Paint(Brush(), 0.5f, 0.5f);
            Assert.AreEqual(1f, TestUtility.Texel(document, 0.5f, 0.5f).r, Tolerance);

            Undo.PerformUndo();
            Assert.AreEqual(0f, TestUtility.Texel(document, 0.5f, 0.5f).r, Tolerance, "Undo");

            Undo.PerformRedo();
            Assert.AreEqual(1f, TestUtility.Texel(document, 0.5f, 0.5f).r, Tolerance, "Redo");
        }

        [Test]
        public void Undo_StoresOnlyThePaintedRegion()
        {
            Paint(Brush(radius: 0.05f), 0.5f, 0.5f);
            long fullTexture = document.Width * document.Height * 4;
            Assert.Less(PaintHistory.UsedBytes, fullTexture / 4);
        }

        [Test]
        public void FillAll_SetsTheChannelEverywhere()
        {
            MaskFill.FillAll(document, MaskChannels.B, 0.75f);

            Color texel = TestUtility.Texel(document, 0.1f, 0.9f);
            Assert.AreEqual(0.75f, texel.b, Tolerance);
            Assert.AreEqual(0f, texel.r, Tolerance);
        }

        [Test]
        public void FillIsland_FillsOnlyTheIslandUnderTheCursor()
        {
            Mesh mesh = TestUtility.CreateTwoQuadMesh();
            MeshRenderer renderer = TestUtility.CreateRenderer(mesh);
            MeshSurface.TryCreate(renderer, 0, out MeshSurface twoQuads, out _);

            Assert.IsTrue(twoQuads.Raycast(new Ray(new Vector3(0.5f, 0.5f, -1f), Vector3.forward), out SurfaceHit hit));
            MaskFill.FillIsland(document, twoQuads, hit, MaskChannels.R, 1f);

            Assert.AreEqual(1f, TestUtility.Texel(document, 0.2f, 0.2f).r, Tolerance, "Inside island A");
            Assert.AreEqual(0f, TestUtility.Texel(document, 0.8f, 0.8f).r, Tolerance, "Island B untouched");
            Assert.AreEqual(1f, TestUtility.Texel(document, 0.41f, 0.2f).r, Tolerance, "Seam padding around island A");

            Object.DestroyImmediate(renderer.gameObject);
            Object.DestroyImmediate(mesh);
        }

        private static BrushSettings Brush(float radius = 0.1f) => new BrushSettings { Radius = radius, Hardness = 1f, Strength = 1f };

        private void Paint(BrushSettings brush, float u, float v)
        {
            using (var stroke = new MaskStroke(document, surface, 0, brush))
                stroke.AddPoint(HitAt(u, v));
        }

        private SurfaceHit HitAt(float u, float v)
        {
            var ray = new Ray(new Vector3(u - 0.5f, v - 0.5f, -1f), Vector3.forward);
            Assert.IsTrue(surface.Raycast(ray, out SurfaceHit hit), $"No hit at uv ({u}, {v})");
            return hit;
        }
    }
}
