using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Document
{
    internal static class GpuReadback
    {
        // Caller destroys the result.
        public static Texture2D Read(RenderTexture source, RectInt region, bool hdr)
        {
            var pixels = new Texture2D(region.width, region.height, hdr ? TextureFormat.RGBAHalf : TextureFormat.RGBA32, false, linear: true)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            pixels.ReadPixels(new Rect(region.x, region.y, region.width, region.height), 0, 0, false);
            RenderTexture.active = previous;
            return pixels;
        }

        // Blit leaves its target active; unbind before releasing.
        public static void Unbind(RenderTexture texture)
        {
            if (texture != null && RenderTexture.active == texture)
                RenderTexture.active = null;
        }
    }
}
