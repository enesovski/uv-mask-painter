using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Document
{
    /// <summary>Creates mask files with no dialogs: linear PNG, saved next to the object's material, tagged as a mask.</summary>
    public static class MaskFactory
    {
        public const int DefaultResolution = 1024;
        public static readonly Color DefaultFill = new Color(0f, 0f, 0f, 0f);

        public static string DefaultName(Renderer owner) => owner != null ? $"{owner.name} Mask" : "Mask";

        /// <summary>Largest main texture of the renderer's materials (power of two, 256–4096), else 1024.</summary>
        public static int SuggestResolution(Renderer owner)
        {
            int size = 0;
            if (owner != null)
                foreach (Material material in owner.sharedMaterials)
                {
                    Texture main = material != null ? material.mainTexture : null;
                    if (main != null) size = Mathf.Max(size, main.width, main.height);
                }
            return size == 0 ? DefaultResolution : Mathf.Clamp(Mathf.ClosestPowerOfTwo(size), 256, 4096);
        }

        /// <summary>Next to the first editable material, else next to the scene, else Assets/.</summary>
        public static string SuggestFolder(Renderer owner)
        {
            if (owner != null)
                foreach (Material material in owner.sharedMaterials)
                {
                    string path = material != null ? AssetDatabase.GetAssetPath(material) : null;
                    if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/") && path.EndsWith(".mat"))
                        return Path.GetDirectoryName(path).Replace('\\', '/');
                }

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            string context = stage != null ? stage.assetPath : owner != null ? owner.gameObject.scene.path : null;
            return !string.IsNullOrEmpty(context) && context.StartsWith("Assets/")
                ? Path.GetDirectoryName(context).Replace('\\', '/')
                : "Assets";
        }

        public static Texture2D Create(Renderer owner, string name, int channelCount, int resolution = 0)
        {
            if (resolution <= 0) resolution = SuggestResolution(owner);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{SuggestFolder(owner)}/{SanitizeFileName(name, owner)}.png");
            CreateMaskFile(path, resolution, resolution, DefaultFill);
            new MaskInfo(channelCount, MaskLibrary.OwnerKey(owner)).Write(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);   // reimport can replace the object
        }

        /// <summary>
        /// Turns an existing texture into a mask of <paramref name="owner"/>. Formats that can't be
        /// written back (JPG, TGA…) are converted to a PNG copy, which is returned instead.
        /// </summary>
        public static Texture2D Adopt(Texture2D texture, Renderer owner, int channelCount)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (!TextureIO.CanWrite(path) || !TextureIO.CanReadLossless(path))
            {
                Texture2D pixels = TextureIO.Load(path);
                path = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, ".png"));
                TextureIO.Write(path, pixels);
                Object.DestroyImmediate(pixels);
                AssetDatabase.ImportAsset(path);
            }
            new MaskInfo(channelCount, MaskLibrary.OwnerKey(owner)).Write(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Writes a solid-color linear PNG with mask import settings (4 channels, no metadata).</summary>
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

        public static string SanitizeFileName(string name, Renderer owner = null)
        {
            if (string.IsNullOrWhiteSpace(name)) name = DefaultName(owner);
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
