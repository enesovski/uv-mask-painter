using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Document
{
    /// <summary>One output channel: a channel of a texture, or a constant when <see cref="Texture"/> is null.</summary>
    public struct ChannelSource
    {
        public Texture2D Texture;
        public int Channel;      // 0..3 = R, G, B, A
        public float Constant;

        public static ChannelSource From(Texture2D texture, int channel) => new ChannelSource { Texture = texture, Channel = channel };
        public static ChannelSource Value(float value) => new ChannelSource { Constant = value };
    }

    /// <summary>Packs channels from several masks into one PNG (e.g. wear in R, wetness in G).</summary>
    public static class ChannelPacker
    {
        public static Texture2D Pack(ChannelSource[] sources, string assetPath)
        {
            Debug.Assert(sources.Length == 4);
            var loaded = new Dictionary<Texture2D, Texture2D>();
            try
            {
                // Output size = largest source.
                int width = 0, height = 0;
                foreach (ChannelSource source in sources)
                {
                    if (source.Texture == null || loaded.ContainsKey(source.Texture)) continue;
                    Texture2D pixels = ReadPixels(source.Texture);
                    loaded[source.Texture] = pixels;
                    width = Mathf.Max(width, pixels.width);
                    height = Mathf.Max(height, pixels.height);
                }
                if (width == 0) width = height = MaskFactory.DefaultResolution;

                var output = new Color32[width * height];
                for (int c = 0; c < 4; c++)
                {
                    ChannelSource source = sources[c];
                    Texture2D pixels = source.Texture != null ? loaded[source.Texture] : null;
                    bool sameSize = pixels != null && pixels.width == width && pixels.height == height;
                    Color32[] direct = sameSize ? pixels.GetPixels32() : null;
                    byte constant = (byte)Mathf.RoundToInt(Mathf.Clamp01(source.Constant) * 255f);

                    for (int y = 0, i = 0; y < height; y++)
                    for (int x = 0; x < width; x++, i++)
                    {
                        byte value;
                        if (pixels == null) value = constant;
                        else if (direct != null) value = Get(direct[i], source.Channel);
                        else value = (byte)Mathf.RoundToInt(pixels.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height)[source.Channel] * 255f);
                        Set(ref output[i], c, value);
                    }
                }

                var result = new Texture2D(width, height, TextureFormat.RGBA32, false, linear: true) { hideFlags = HideFlags.HideAndDontSave };
                result.SetPixels32(output);
                TextureIO.Write(assetPath, result);
                Object.DestroyImmediate(result);
                TextureIO.ApplyMaskImportSettings(assetPath);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            finally
            {
                foreach (Texture2D pixels in loaded.Values) Object.DestroyImmediate(pixels);
            }
        }

        // Lossless from disk when possible; otherwise from the imported texture.
        private static Texture2D ReadPixels(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            Texture2D pixels = TextureIO.Load(path);
            if (pixels != null) return pixels;

            RenderTexture temp = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, temp);
            pixels = GpuReadback.Read(temp, new RectInt(0, 0, texture.width, texture.height), hdr: false);
            GpuReadback.Unbind(temp);
            RenderTexture.ReleaseTemporary(temp);
            return pixels;
        }

        private static byte Get(Color32 c, int channel) => channel == 0 ? c.r : channel == 1 ? c.g : channel == 2 ? c.b : c.a;

        private static void Set(ref Color32 c, int channel, byte value)
        {
            switch (channel)
            {
                case 0: c.r = value; break;
                case 1: c.g = value; break;
                case 2: c.b = value; break;
                default: c.a = value; break;
            }
        }
    }
}
