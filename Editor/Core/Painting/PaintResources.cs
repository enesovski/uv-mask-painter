using UnityEngine;
using Object = UnityEngine.Object;

namespace Archura.UVMaskPainter.Editor.Painting
{
    /// <summary>Shared material and shader ids for Hidden/Archura/UVMaskPainter/Paint.</summary>
    internal static class PaintResources
    {
        public const string ShaderName = "Hidden/Archura/UVMaskPainter/Paint";

        public const int CoveragePass = 0;
        public const int OccupancyPass = 1;
        public const int DilatePass = 2;
        public const int CompositePass = 3;
        public const int CompositeSmoothPass = 4;
        public const int CompositeInvertPass = 5;

        public static readonly int CoverageTex = Shader.PropertyToID("_CoverageTex");
        public static readonly int OccupancyTex = Shader.PropertyToID("_OccupancyTex");
        public static readonly int TexelSize = Shader.PropertyToID("_TexelSize");
        public static readonly int BrushCenter = Shader.PropertyToID("_BrushCenter");
        public static readonly int BrushNormal = Shader.PropertyToID("_BrushNormal");
        public static readonly int BrushRadius = Shader.PropertyToID("_BrushRadius");
        public static readonly int BrushHardness = Shader.PropertyToID("_BrushHardness");
        public static readonly int BrushPressure = Shader.PropertyToID("_BrushPressure");
        public static readonly int Value = Shader.PropertyToID("_Value");
        public static readonly int Strength = Shader.PropertyToID("_Strength");
        public static readonly int ColorMask = Shader.PropertyToID("_ColorMask");

        private static readonly string[] UVKeywords = { null, "_UV1", "_UV2", "_UV3" };

        public static readonly int MainTex = Shader.PropertyToID("_MainTex");

        private static Material material;
        private static Mesh quad;

        public static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "UV Mask Painter Quad", hideFlags = HideFlags.HideAndDontSave };
                quad.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, new Vector3(1f, 1f, 0f) };
                quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                return quad;
            }
        }

        public static Material Material
        {
            get
            {
                if (material != null) return material;

                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                    throw new MissingReferenceException($"Shader '{ShaderName}' is missing. Reimport the UV Mask Painter package.");

                material = new Material(shader) { name = "UV Mask Painter", hideFlags = HideFlags.HideAndDontSave };
                return material;
            }
        }

        public static void SetUVChannel(int channel)
        {
            Material mat = Material;
            for (int i = 1; i < UVKeywords.Length; i++)
            {
                if (i == channel) mat.EnableKeyword(UVKeywords[i]);
                else mat.DisableKeyword(UVKeywords[i]);
            }
        }

        public static void Release()
        {
            if (material != null) Object.DestroyImmediate(material);
            if (quad != null) Object.DestroyImmediate(quad);
            material = null;
            quad = null;
        }
    }
}
