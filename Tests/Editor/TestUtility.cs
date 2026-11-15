using System.Collections.Generic;
using Archura.UVMaskPainter.Editor.Document;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Tests
{
    internal static class TestUtility
    {
        public const string TempFolder = "Assets/__UVMaskPainterTests";

        public static void CreateTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "__UVMaskPainterTests");
        }

        public static void DeleteTempFolder()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }

        /// <summary>Unity's 1x1 quad (XY plane, facing -Z, UV 0..1) without a collider.</summary>
        public static MeshRenderer CreateQuad()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.GetComponent<MeshRenderer>();
        }

        public static MeshRenderer CreateRenderer(Mesh mesh, string name = "Test Mesh")
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            return go.AddComponent<MeshRenderer>();
        }

        /// <summary>
        /// Two quads facing -Z. Quad A: x 0..1 with UVs 0..0.4. Quad B: x 2..3 with UVs 0.6..1
        /// (or the same UVs as A when <paramref name="overlappingUVs"/> is true).
        /// </summary>
        public static Mesh CreateTwoQuadMesh(bool overlappingUVs = false)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            AddQuad(vertices, uvs, triangles, new Vector3(0, 0, 0), new Rect(0f, 0f, 0.4f, 0.4f));
            AddQuad(vertices, uvs, triangles, new Vector3(2, 0, 0), overlappingUVs ? new Rect(0f, 0f, 0.4f, 0.4f) : new Rect(0.6f, 0.6f, 0.4f, 0.4f));

            var mesh = new Mesh { name = "Two Quads", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>A unit quad whose two triangles use separate (duplicated) vertices, like a hard edge.</summary>
        public static Mesh CreateSplitQuadMesh()
        {
            var mesh = new Mesh { name = "Split Quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[]
            {
                new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0),
                new Vector3(0, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0)
            };
            mesh.uv = new[]
            {
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0)
            };
            mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            mesh.RecalculateNormals();
            return mesh;
        }

        public static Color Texel(MaskDocument document, float u, float v)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(u * document.Width), 0, document.Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * document.Height), 0, document.Height - 1);
            Texture2D pixel = document.ReadPixels(new RectInt(x, y, 1, 1));
            Color color = pixel.GetPixel(0, 0);
            Object.DestroyImmediate(pixel);
            return color;
        }

        private static void AddQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 origin, Rect uvRect)
        {
            int start = vertices.Count;
            vertices.Add(origin + new Vector3(0, 0, 0));
            vertices.Add(origin + new Vector3(1, 0, 0));
            vertices.Add(origin + new Vector3(0, 1, 0));
            vertices.Add(origin + new Vector3(1, 1, 0));
            uvs.Add(new Vector2(uvRect.xMin, uvRect.yMin));
            uvs.Add(new Vector2(uvRect.xMax, uvRect.yMin));
            uvs.Add(new Vector2(uvRect.xMin, uvRect.yMax));
            uvs.Add(new Vector2(uvRect.xMax, uvRect.yMax));
            // Same winding as Unity's quad (front face towards -Z).
            triangles.AddRange(new[] { start, start + 3, start + 1, start + 3, start, start + 2 });
        }
    }
}
