using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Editor.Geometry;
using Archura.UVMaskPainter.Editor.Painting;
using Archura.UVMaskPainter.Editor.Targeting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Tests
{
    public class TerrainAndRuntimeTests
    {
        private const float Tolerance = 0.02f;
        private GameObject created;

        [SetUp]
        public void SetUp() => TestUtility.CreateTempFolder();

        [TearDown]
        public void TearDown()
        {
            if (created != null) Object.DestroyImmediate(created);
            TestUtility.DeleteTempFolder();
        }

        private Terrain CreateTerrain()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(100f, 20f, 50f) };
            AssetDatabase.CreateAsset(data, $"{TestUtility.TempFolder}/Test.asset");
            created = Terrain.CreateTerrainGameObject(data);
            created.transform.position = new Vector3(10f, 0f, -5f);
            return created.GetComponent<Terrain>();
        }

        [Test]
        public void Terrain_RaycastReturnsNormalizedTerrainUV()
        {
            Terrain terrain = CreateTerrain();
            Assert.IsTrue(MeshSurface.TryCreate(terrain, 0, out MeshSurface surface, out string error), error);

            // 25% across the width, 80% along the length.
            var point = new Vector3(10f + 25f, 50f, -5f + 40f);
            Assert.IsTrue(surface.Raycast(new Ray(point, Vector3.down), out SurfaceHit hit));
            Assert.AreEqual(0.25f, hit.UV.x, Tolerance);
            Assert.AreEqual(0.8f, hit.UV.y, Tolerance);
            Assert.Less(Vector2.Distance(hit.UV, TerrainMesh.WorldToUV(terrain, hit.Point)), 0.001f);
            surface.Dispose();
        }

        [Test]
        public void Terrain_HasOnlyUV0AndIsOwnedByItsTerrainData()
        {
            Terrain terrain = CreateTerrain();
            PaintSubject subject = terrain;
            Assert.IsTrue(subject.HasUV(0));
            Assert.IsFalse(subject.HasUV(1));
            StringAssert.StartsWith("terrain:", MaskLibrary.OwnerKey(subject));
        }

        [Test]
        public void Terrain_StrokePaintsUnderTheBrush()
        {
            Terrain terrain = CreateTerrain();
            Texture2D mask = MaskFactory.Create(terrain, "Biomes", 1, 64);
            MaskDocument document = MaskDocument.Open(mask);
            MeshSurface.TryCreate(terrain, 0, out MeshSurface surface, out _);

            surface.Raycast(new Ray(new Vector3(60f, 50f, 20f), Vector3.down), out SurfaceHit hit);
            var stroke = new MaskStroke(document, surface, -1, new BrushSettings { Radius = 5f, Hardness = 1f });
            stroke.AddPoint(hit);
            stroke.End();

            Assert.AreEqual(1f, TestUtility.Texel(document, hit.UV.x, hit.UV.y).r, Tolerance);
            Assert.AreEqual(0f, TestUtility.Texel(document, 0.05f, 0.05f).r, Tolerance);
            document.Dispose();
            surface.Dispose();
        }

        [Test]
        public void MaskSampler_ReadsNonReadableMasksThroughTheGpu()
        {
            string path = $"{TestUtility.TempFolder}/Sampled.png";
            Texture2D mask = MaskFactory.CreateMaskFile(path, 8, 8, new Color(0.25f, 0.75f, 0f, 1f));
            Assert.IsFalse(mask.isReadable);

            var sampler = new MaskSampler(mask);
            Color value = sampler.Sample(new Vector2(0.5f, 0.5f));
            Assert.AreEqual(0.25f, value.r, Tolerance);
            Assert.AreEqual(0.75f, value.g, Tolerance);
            Assert.AreEqual(0.75f, sampler.Sample(new Vector2(0.1f, 0.9f), 1), Tolerance);
        }

        [Test]
        public void MaskSampler_InterpolatesBetweenTexels()
        {
            var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(new[] { new Color32(0, 0, 0, 255), new Color32(255, 0, 0, 255) });
            texture.Apply();

            var sampler = new MaskSampler(texture);
            Assert.AreEqual(0.5f, sampler.Sample(new Vector2(0.5f, 0.5f)).r, Tolerance);
            Assert.AreEqual(0f, sampler.SamplePoint(new Vector2(0.2f, 0.5f)).r, Tolerance);
            Assert.AreEqual(1f, sampler.SamplePoint(new Vector2(0.8f, 0.5f)).r, Tolerance);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void MaskSurface_LooksUpChannelsByName()
        {
            created = new GameObject("Surface");
            var surface = created.AddComponent<MaskSurface>();
            surface.Configure(null, 0, new[] { "Wet", "Moss" });
            Assert.AreEqual(1, surface.ChannelIndex("moss"));
            Assert.AreEqual(-1, surface.ChannelIndex("Snow"));
        }
    }
}
