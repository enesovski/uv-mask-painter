using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Archura.UVMaskPainter.Editor.Geometry
{
    /// <summary>Paintable geometry of a renderer (the drawn mesh, no colliders/physics, works in Prefab Mode).</summary>
    public sealed class MeshSurface : IDisposable
    {
        public Renderer Renderer { get; }
        // Baked snapshot for skinned meshes.
        public Mesh Mesh { get; }
        public int UVChannel { get; }
        public int SubmeshCount => Mesh.subMeshCount;
        public Matrix4x4 LocalToWorld => Renderer.transform.localToWorldMatrix;

        private readonly bool ownsMesh;
        private readonly Vector3[] vertices;
        private readonly Vector2[] uvs;
        private readonly Vector3[] normals;        // may be empty
        private readonly int[] triangles;          // all submeshes, concatenated
        private readonly int[] triangleSubmesh;
        private readonly TriangleBvh bvh;
        private int[] islandOfTriangle;            // computed on first use

        private MeshSurface(Renderer renderer, Mesh mesh, bool ownsMesh, int uvChannel, Vector2[] uvs)
        {
            Renderer = renderer;
            Mesh = mesh;
            this.ownsMesh = ownsMesh;
            UVChannel = uvChannel;
            this.uvs = uvs;
            vertices = mesh.vertices;
            normals = mesh.normals;

            var allTriangles = new List<int>();
            var submeshOfTriangle = new List<int>();
            var submeshTriangles = new List<int>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                mesh.GetTriangles(submeshTriangles, s);
                allTriangles.AddRange(submeshTriangles);
                for (int i = 0; i < submeshTriangles.Count / 3; i++) submeshOfTriangle.Add(s);
            }

            triangles = allTriangles.ToArray();
            triangleSubmesh = submeshOfTriangle.ToArray();
            bvh = new TriangleBvh(vertices, triangles);
        }

        public static bool TryCreate(Renderer renderer, int uvChannel, out MeshSurface surface, out string error)
        {
            surface = null;
            error = null;

            if (renderer == null)
            {
                error = "No renderer selected.";
                return false;
            }
            if (uvChannel < 0 || uvChannel > 3)
            {
                error = "UV channel must be between 0 and 3.";
                return false;
            }

            Mesh mesh;
            bool ownsMesh = false;
            switch (renderer)
            {
                case MeshRenderer _:
                    var filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                    break;

                case SkinnedMeshRenderer skinned when skinned.sharedMesh != null:
                    mesh = new Mesh { name = skinned.sharedMesh.name + " (Baked)", hideFlags = HideFlags.HideAndDontSave };
                    skinned.BakeMesh(mesh, false);
                    ownsMesh = true;
                    break;

                case SkinnedMeshRenderer _:
                    mesh = null;
                    break;

                default:
                    error = $"{renderer.GetType().Name} can't be painted. Use a MeshRenderer or SkinnedMeshRenderer.";
                    return false;
            }

            if (mesh == null)
            {
                error = $"'{renderer.name}' has no mesh.";
                return false;
            }

            var uvList = new List<Vector2>();
            mesh.GetUVs(uvChannel, uvList);
            if (uvList.Count == 0)
            {
                error = $"'{mesh.name}' has no UV{uvChannel} coordinates.";
                if (ownsMesh) Object.DestroyImmediate(mesh);
                return false;
            }

            surface = new MeshSurface(renderer, mesh, ownsMesh, uvChannel, uvList.ToArray());
            return true;
        }

        public bool Raycast(Ray worldRay, out SurfaceHit hit)
        {
            hit = default;
            Matrix4x4 toWorld = LocalToWorld;
            Matrix4x4 toLocal = toWorld.inverse;
            var localRay = new Ray(toLocal.MultiplyPoint3x4(worldRay.origin), toLocal.MultiplyVector(worldRay.direction));

            float bestT = float.MaxValue;
            int bestTriangle = -1;
            float bestU = 0, bestV = 0;

            bvh.Query(
                bounds => bounds.IntersectRay(localRay, out float entry) && entry <= bestT,
                t =>
                {
                    if (IntersectTriangle(localRay, t, out float distance, out float u, out float v) && distance < bestT)
                    {
                        bestT = distance;
                        bestTriangle = t;
                        bestU = u;
                        bestV = v;
                    }
                });

            if (bestTriangle < 0) return false;

            int i0 = triangles[bestTriangle * 3], i1 = triangles[bestTriangle * 3 + 1], i2 = triangles[bestTriangle * 3 + 2];
            float w = 1f - bestU - bestV;

            Vector3 a = toWorld.MultiplyPoint3x4(vertices[i0]);
            Vector3 b = toWorld.MultiplyPoint3x4(vertices[i1]);
            Vector3 c = toWorld.MultiplyPoint3x4(vertices[i2]);
            Vector3 point = a * w + b * bestU + c * bestV;

            // Mesh normals, because the shader's back-face rejection compares against them.
            Vector3 normal = normals.Length == vertices.Length
                ? toLocal.transpose.MultiplyVector(normals[i0] * w + normals[i1] * bestU + normals[i2] * bestV).normalized
                : Vector3.Cross(b - a, c - a).normalized;
            Vector2 uv = uvs[i0] * w + uvs[i1] * bestU + uvs[i2] * bestV;

            hit = new SurfaceHit(point, normal, uv, Vector3.Distance(worldRay.origin, point), bestTriangle, triangleSubmesh[bestTriangle]);
            return true;
        }

        /// <summary>UV bounds a brush sphere can touch (per-triangle footprint, so big triangles don't inflate it).</summary>
        public bool TryGetUVBounds(Vector3 worldCenter, float worldRadius, out Rect uvBounds)
        {
            Matrix4x4 toWorld = LocalToWorld;
            Vector3 localCenter = toWorld.inverse.MultiplyPoint3x4(worldCenter);
            float minScale = Mathf.Min(toWorld.GetColumn(0).magnitude, Mathf.Min(toWorld.GetColumn(1).magnitude, toWorld.GetColumn(2).magnitude));
            float localRadius = worldRadius / Mathf.Max(minScale, 1e-6f);
            float sqrRadius = localRadius * localRadius;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;

            bvh.Query(
                bounds => bounds.SqrDistance(localCenter) <= sqrRadius,
                t =>
                {
                    if (bvh.GetTriangleBounds(t).SqrDistance(localCenter) > sqrRadius) return;
                    if (!TryGetTriangleFootprint(t, localCenter, localRadius, out Rect footprint)) return;
                    min = Vector2.Min(min, footprint.min);
                    max = Vector2.Max(max, footprint.max);
                    any = true;
                });

            uvBounds = any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
            return any;
        }

        // Disc around the projected center, mapped through the triangle's UV Jacobian, clipped to the triangle.
        private bool TryGetTriangleFootprint(int triangle, Vector3 center, float radius, out Rect footprint)
        {
            int i0 = triangles[triangle * 3], i1 = triangles[triangle * 3 + 1], i2 = triangles[triangle * 3 + 2];
            Vector3 a = vertices[i0], b = vertices[i1], c = vertices[i2];
            Vector2 uvA = uvs[i0], uvB = uvs[i1], uvC = uvs[i2];

            Vector2 triMin = Vector2.Min(uvA, Vector2.Min(uvB, uvC));
            Vector2 triMax = Vector2.Max(uvA, Vector2.Max(uvB, uvC));
            footprint = Rect.MinMaxRect(triMin.x, triMin.y, triMax.x, triMax.y);

            // 2D frame in the triangle's plane.
            Vector3 edge1 = b - a, edge2 = c - a;
            Vector3 normal = Vector3.Cross(edge1, edge2);
            if (normal.sqrMagnitude < 1e-20f || edge1.sqrMagnitude < 1e-20f) return true; // degenerate: use the whole triangle
            Vector3 axisX = edge1.normalized;
            Vector3 axisY = Vector3.Cross(normal.normalized, axisX);

            // Solve UV = uvA + J * planePos for the 2x2 Jacobian J.
            float x1 = Vector3.Dot(edge1, axisX), y1 = Vector3.Dot(edge1, axisY);
            float x2 = Vector3.Dot(edge2, axisX), y2 = Vector3.Dot(edge2, axisY);
            float det = x1 * y2 - x2 * y1;
            if (Mathf.Abs(det) < 1e-20f) return true;

            Vector2 du = uvB - uvA, dv = uvC - uvA;
            float j00 = (du.x * y2 - dv.x * y1) / det, j01 = (dv.x * x1 - du.x * x2) / det;
            float j10 = (du.y * y2 - dv.y * y1) / det, j11 = (dv.y * x1 - du.y * x2) / det;

            Vector3 local = center - a;
            float px = Vector3.Dot(local, axisX), py = Vector3.Dot(local, axisY);
            Vector2 uvCenter = uvA + new Vector2(j00 * px + j01 * py, j10 * px + j11 * py);
            Vector2 extent = new Vector2(Mathf.Sqrt(j00 * j00 + j01 * j01), Mathf.Sqrt(j10 * j10 + j11 * j11)) * radius;

            Vector2 footMin = Vector2.Max(uvCenter - extent, triMin);
            Vector2 footMax = Vector2.Min(uvCenter + extent, triMax);
            if (footMin.x > footMax.x || footMin.y > footMax.y) return false;
            footprint = Rect.MinMaxRect(footMin.x, footMin.y, footMax.x, footMax.y);
            return true;
        }

        public int GetIsland(int triangle)
        {
            EnsureIslands();
            return islandOfTriangle[triangle];
        }

        /// <summary>Temp mesh of one island (surface UVs moved to UV0). Caller destroys it.</summary>
        public Mesh CreateIslandMesh(int island, int submesh, out Rect uvBounds)
        {
            EnsureIslands();

            var islandTriangles = new List<int>();
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int t = 0; t < islandOfTriangle.Length; t++)
            {
                if (islandOfTriangle[t] != island || triangleSubmesh[t] != submesh) continue;
                for (int k = 0; k < 3; k++)
                {
                    int index = triangles[t * 3 + k];
                    islandTriangles.Add(index);
                    min = Vector2.Min(min, uvs[index]);
                    max = Vector2.Max(max, uvs[index]);
                }
            }

            uvBounds = islandTriangles.Count > 0 ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;

            var mesh = new Mesh { name = "UV Island", hideFlags = HideFlags.HideAndDontSave };
            if (vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetTriangles(islandTriangles, 0, false);
            return mesh;
        }

        public void Dispose()
        {
            if (ownsMesh && Mesh != null) Object.DestroyImmediate(Mesh);
        }

        private void EnsureIslands()
        {
            if (islandOfTriangle == null)
                islandOfTriangle = UVIslands.Compute(triangles, vertices, uvs, out _);
        }

        // Möller–Trumbore, two-sided.
        private bool IntersectTriangle(Ray ray, int triangle, out float distance, out float u, out float v)
        {
            distance = u = v = 0f;
            Vector3 a = vertices[triangles[triangle * 3]];
            Vector3 b = vertices[triangles[triangle * 3 + 1]];
            Vector3 c = vertices[triangles[triangle * 3 + 2]];

            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 p = Vector3.Cross(ray.direction, edge2);
            float det = Vector3.Dot(edge1, p);
            if (Mathf.Abs(det) < 1e-12f) return false;

            float invDet = 1f / det;
            Vector3 s = ray.origin - a;
            u = Vector3.Dot(s, p) * invDet;
            if (u < 0f || u > 1f) return false;

            Vector3 q = Vector3.Cross(s, edge1);
            v = Vector3.Dot(ray.direction, q) * invDet;
            if (v < 0f || u + v > 1f) return false;

            distance = Vector3.Dot(edge2, q) * invDet;
            return distance >= 0f;
        }
    }
}
