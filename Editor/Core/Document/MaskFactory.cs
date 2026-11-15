using System.IO;
using Archura.UVMaskPainter.Editor.Targeting;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Document
{
    /// <summary>Creates masks with no dialogs: main-texture resolution, saved next to the material, auto-assigned.</summary>
    public static class MaskFactory
    {
        public const int DefaultResolution = 1024;
        public static readonly Color DefaultFill = new Color(0f, 0f, 0f, 0f);

        public static int SuggestResolution(Material material)
        {
            Texture main = material != null ? material.mainTexture : null;
            int size = main != null ? Mathf.Max(main.width, main.height) : DefaultResolution;
            return Mathf.Clamp(Mathf.ClosestPowerOfTwo(size), 256, 4096);
        }

        public static string SuggestPath(PaintTarget target)
        {
            Material material = target.Material;
            string materialPath = AssetDatabase.GetAssetPath(material);
            string folder = string.IsNullOrEmpty(materialPath) || !materialPath.StartsWith("Assets")
                ? "Assets"
                : Path.GetDirectoryName(materialPath).Replace('\\', '/');

            string baseName = material != null ? material.name : target.Renderer.name;
            string fileName = $"{baseName}_{target.PropertyName.TrimStart('_')}.png";
            return AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}");
        }

        public static bool CanAssign(PaintTarget target, out string reason)
        {
            reason = null;
            Material material = target?.Material;
            if (material == null)
            {
                reason = "The material slot is empty.";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path)) return true; // scene-owned material instance

            if (path.StartsWith("Packages/") || !path.EndsWith(".mat"))
            {
                reason = $"'{material.name}' is read-only (it comes from {Path.GetFileName(path)}). " +
                         "Extract or duplicate the material to paint on it.";
                return false;
            }
            return true;
        }

        public static Texture2D CreateMaskFile(string assetPath, int width, int height, Color fill)
        {
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false, linear: true) { hideFlags = HideFlags.HideAndDontSave };
            var colors = new Color32[width * height];
            Color32 value = fill;
            for (int i = 0; i < colors.Length; i++) colors[i] = value;
            pixels.SetPixels32(colors);

            TextureIO.Write(assetPath, pixels);
            Object.DestroyImmediate(pixels);
            TextureIO.ApplyMaskImportSettings(assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        public static bool TryCreateFor(PaintTarget target, out Texture2D texture, out string error)
        {
            texture = null;
            if (!CanAssign(target, out error)) return false;

            int size = SuggestResolution(target.Material);
            texture = CreateMaskFile(SuggestPath(target), size, size, DefaultFill);
            Assign(target, texture);
            TargetResolver.Remember(target);
            return true;
        }

        public static void Assign(PaintTarget target, Texture2D texture)
        {
            Material material = target.Material;
            Undo.RecordObject(material, "Assign Mask");
            material.SetTexture(target.PropertyName, texture);
            EditorUtility.SetDirty(material);
        }
    }
}
