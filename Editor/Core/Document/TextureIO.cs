using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Archura.UVMaskPainter.Editor.Document
{
    /// <summary>Mask file IO. Pixels are read from the source file, never the (compressed) import, so saves are lossless.</summary>
    public static class TextureIO
    {
        public static bool CanReadLossless(string assetPath)
        {
            string ext = Extension(assetPath);
            return ext == ".png" || ext == ".exr" || ext == ".jpg" || ext == ".jpeg";
        }

        public static bool CanWrite(string assetPath)
        {
            string ext = Extension(assetPath);
            return ext == ".png" || ext == ".exr";
        }

        public static bool IsHdr(string assetPath) => Extension(assetPath) == ".exr";

        /// <summary>Raw linear pixels, or null if the format can't be read losslessly.</summary>
        public static Texture2D Load(string assetPath)
        {
            if (!CanReadLossless(assetPath)) return null;
            string fullPath = ToFullPath(assetPath);
            if (!File.Exists(fullPath)) return null;

            var format = IsHdr(assetPath) ? TextureFormat.RGBAHalf : TextureFormat.RGBA32;
            var texture = new Texture2D(2, 2, format, false, linear: true) { hideFlags = HideFlags.HideAndDontSave };
            if (!texture.LoadImage(File.ReadAllBytes(fullPath), markNonReadable: false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            if (texture.format == TextureFormat.R8 || texture.format == TextureFormat.R16)
                texture = ExpandGray(texture);

            return texture;
        }

        public static void Write(string assetPath, Texture2D pixels)
        {
            byte[] bytes = IsHdr(assetPath)
                ? pixels.EncodeToEXR(Texture2D.EXRFlags.CompressZIP)
                : pixels.EncodeToPNG();

            WriteAtomic(ToFullPath(assetPath), bytes, TempDirectory);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        // Thread-safe.
        public static byte[] EncodeRaw(byte[] data, GraphicsFormat format, int width, int height, bool hdr)
        {
            return hdr
                ? ImageConversion.EncodeArrayToEXR(data, format, (uint)width, (uint)height, 0, Texture2D.EXRFlags.CompressZIP)
                : ImageConversion.EncodeArrayToPNG(data, format, (uint)width, (uint)height);
        }

        /// <summary>Temp file + atomic replace: an interrupted save never corrupts the mask. Thread-safe.</summary>
        public static void WriteAtomic(string fullPath, byte[] bytes, string tempDirectory)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            Directory.CreateDirectory(tempDirectory);
            string temp = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + ".uvmask");
            File.WriteAllBytes(temp, bytes);

            if (File.Exists(fullPath)) File.Replace(temp, fullPath, null);
            else File.Move(temp, fullPath);
        }

        // Outside Assets so half-written files are never imported. Main thread only.
        public static string TempDirectory => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "UVMaskPainter");

        public static void ApplyMaskImportSettings(string assetPath, int channelCount = 4)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) return;
            ConfigureMaskImporter(importer, channelCount);
            importer.SaveAndReimport();
        }

        /// <summary>Linear data texture. One channel imports as single-channel (R8/BC4); alpha only with four.</summary>
        public static void ConfigureMaskImporter(TextureImporter importer, int channelCount)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = channelCount == 1 ? TextureImporterType.SingleChannel : TextureImporterType.Default;
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            settings.sRGBTexture = false;
            settings.alphaSource = channelCount >= 4 ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            settings.alphaIsTransparency = false;
            settings.mipmapEnabled = true;
            settings.filterMode = FilterMode.Bilinear;
            importer.SetTextureSettings(settings);
        }

        public static bool IsImportedAsSrgb(string assetPath) =>
            AssetImporter.GetAtPath(assetPath) is TextureImporter importer && importer.sRGBTexture;

        public static string ToFullPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath));

        private static string Extension(string path) =>
            string.IsNullOrEmpty(path) ? string.Empty : Path.GetExtension(path).ToLowerInvariant();

        private static Texture2D ExpandGray(Texture2D gray)
        {
            Color[] pixels = gray.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(pixels[i].r, pixels[i].r, pixels[i].r, 1f);

            var rgba = new Texture2D(gray.width, gray.height, TextureFormat.RGBA32, false, linear: true) { hideFlags = HideFlags.HideAndDontSave };
            rgba.SetPixels(pixels);
            rgba.Apply(false);
            UnityEngine.Object.DestroyImmediate(gray);
            return rgba;
        }
    }
}
