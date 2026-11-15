using Archura.UVMaskPainter.Editor.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Tests
{
    public class GeometryTests
    {
        private GameObject created;
        private Mesh createdMesh;

        [TearDown]
        public void TearDown()
        {
            if (created != null) Object.DestroyImmediate(created);
            if (createdMesh != null) Object.DestroyImmediate(createdMesh);
        }

        [Test]
        public void Raycast_MatchesUnityMeshCollider_OnSphere()
        {
            created = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            created.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(10, 40, 70));
            created.transform.localScale = new Vector3(1.5f, 0.7f, 2f);
            Object.DestroyImmediate(created.GetComponent<Collider>());
            var reference = created.AddComponent<MeshCollider>();
            Physics.SyncTransforms();

            Assert.IsTrue(MeshSurface.TryCreate(created.GetComponent<Renderer>(), 0, out MeshSurface surface, out _));
            var random = new System.Random(42);
            int hits = 0;
            for (int i = 0; i < 200; i++)
            {
                Vector3 origin = created.transform.position + RandomDirection(random) * 5f;
                Vector3 target = created.transform.position + RandomDirection(random) * 0.6f;
                var ray = new Ray(origin, target - origin);

                bool expected = reference.Raycast(ray, out RaycastHit expectedHit, 100f);
                bool actual = surface.Raycast(ray, out SurfaceHit actualHit);
                Assert.AreEqual(expected, actual, $"Ray {i} hit mismatch");
                if (!expected) continue;

                hits++;
                Assert.AreEqual(expectedHit.distance, actualHit.Distance, 1e-3f, $"Ray {i} distance");
                Assert.AreEqual(expectedHit.textureCoord.x, actualHit.UV.x, 1e-3f, $"Ray {i} u");
                Assert.AreEqual(expectedHit.textureCoord.y, actualHit.UV.y, 1e-3f, $"Ray {i} v");
            }
            Assert.Greater(hits, 100);
            surface.Dispose();
        }

        [Test]
        public void Raycast_FollowsTheObjectWhenItMoves()
        {
            MeshRenderer quad = TestUtility.CreateQuad();
            created = quad.gameObject;
            Assert.IsTrue(MeshSurface.TryCreate(quad, 0, out MeshSurface surface, out _));

            created.transform.position = new Vector3(10, 0, 0);
            Assert.IsFalse(surface.Raycast(new Ray(new Vector3(0, 0, -1), Vector3.forward), out _));
            Assert.IsTrue(surface.Raycast(new Ray(new Vector3(10, 0, -1), Vector3.forward), out SurfaceHit hit));
            Assert.AreEqual(0.5f, hit.UV.x, 1e-4f);
        }

        [Test]
        public void TryCreate_ExplainsMissingUVs()
        {
            createdMesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.right }, triangles = new[] { 0, 1, 2 } };
            created = TestUtility.CreateRenderer(createdMesh).gameObject;

            Assert.IsFalse(MeshSurface.TryCreate(created.GetComponent<Renderer>(), 0, out _, out string error));
            StringAssert.Contains("UV0", error);
        }

        [Test]
        public void Islands_SeparateUVRegionsAreSeparateIslands()
        {
            createdMesh = TestUtility.CreateTwoQuadMesh();
            created = TestUtility.CreateRenderer(createdMesh).gameObject;
            MeshSurface.TryCreate(created.GetComponent<Renderer>(), 0, out MeshSurface surface, out _);

            Assert.AreEqual(surface.GetIsland(0), surface.GetIsland(1));
            Assert.AreNotEqual(surface.GetIsland(0), surface.GetIsland(2));
        }

        [Test]
        public void Islands_OverlappingUVsAtDifferentPositionsStaySeparate()
        {
            createdMesh = TestUtility.CreateTwoQuadMesh(overlappingUVs: true);
            created = TestUtility.CreateRenderer(createdMesh).gameObject;
            MeshSurface.TryCreate(created.GetComponent<Renderer>(), 0, out MeshSurface surface, out _);

            Assert.AreNotEqual(surface.GetIsland(0), surface.GetIsland(2));
        }

        [Test]
        public void Islands_SplitVerticesWithSameUVFormOneIsland()
        {
            createdMesh = TestUtility.CreateSplitQuadMesh();
            created = TestUtility.CreateRenderer(createdMesh).gameObject;
            MeshSurface.TryCreate(created.GetComponent<Renderer>(), 0, out MeshSurface surface, out _);

            Assert.AreEqual(surface.GetIsland(0), surface.GetIsland(1));
        }

        [Test]
        public void UVBounds_CoverTheBrushArea()
        {
            MeshRenderer quad = TestUtility.CreateQuad();
            created = quad.gameObject;
            MeshSurface.TryCreate(quad, 0, out MeshSurface surface, out _);

            Assert.IsTrue(surface.TryGetUVBounds(Vector3.zero, 0.1f, out Rect bounds));
            Assert.IsTrue(bounds.Contains(new Vector2(0.5f, 0.5f)));
            Assert.IsFalse(surface.TryGetUVBounds(new Vector3(0, 0, -5f), 0.1f, out _));
        }

        private static Vector3 RandomDirection(System.Random random)
        {
            var v = new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1);
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.up;
        }
    }
}
