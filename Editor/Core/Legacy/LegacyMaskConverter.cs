using System.IO;
using System.Linq;
using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Legacy;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Legacy
{
    /// <summary>Converts v1 MaskAssets into regular PNG masks that v2 paints directly.</summary>
    internal static class LegacyMaskConverter
    {
        private const string MenuPath = "Assets/Archura/UV Mask Painter/Convert Legacy Mask to PNG";

        [MenuItem(MenuPath, true)]
        private static bool CanConvertSelection() => Selection.objects.Any(o => o is MaskAsset);

        [MenuItem(MenuPath)]
        private static void ConvertSelection()
        {
            foreach (MaskAsset asset in Selection.objects.OfType<MaskAsset>())
            {
                string path = Convert(asset);
                if (path != null)
                    Debug.Log($"[UV Mask Painter] Converted '{asset.name}' to {path}. " +
                              "Assign it to your material in place of the old mask texture.", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
        }

        public static string Convert(MaskAsset asset)
        {
            Texture2D source = asset.Texture;
            if (source == null)
            {
                Debug.LogWarning($"[UV Mask Painter] '{asset.name}' has no mask texture to convert.", asset);
                return null;
            }

            var pixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, linear: true);
            pixels.SetPixels32(source.GetPixels32());

            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset)).Replace('\\', '/');
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{asset.name}.png");
            TextureIO.Write(path, pixels);
            Object.DestroyImmediate(pixels);

            TextureIO.ApplyMaskImportSettings(path);
            if (!asset.LinearColor && AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
            return path;
        }
    }
}
