using UnityEngine;

namespace Archura.UVMaskPainter
{
    /// <summary>
    /// A CPU copy of a mask for fast lookups at runtime: footstep surfaces, spawn zones, AI costs, wetness…
    /// Works with any mask, readable or not (non-readable textures are copied once through the GPU).
    /// Create it once (e.g. in Awake) and sample as often as you like; sampling allocates nothing.
    /// </summary>
    public sealed class MaskSampler
    {
        public Texture2D Source { get; }
        public int Width { get; }
        public int Height { get; }

        private readonly Color32[] pixels;

        /// <param name="mask">The mask texture.</param>
        /// <param name="maxResolution">Downsample larger masks to this size to save memory (0 keeps the full size).</param>
        public MaskSampler(Texture2D mask, int maxResolution = 0)
        {
            Source = mask;
            int width = mask.width, height = mask.height;
            if (maxResolution > 0 && Mathf.Max(width, height) > maxResolution)
            {
                float scale = (float)maxResolution / Mathf.Max(width, height);
                width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
                height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            }

            Width = width;
            Height = height;
            pixels = width == mask.width && height == mask.height && mask.isReadable
                ? mask.GetPixels32()
                : ReadThroughGpu(mask, width, height);
        }

        /// <summary>Bilinear sample at a UV (0..1, clamped). Channels the mask doesn't have read as 0.</summary>
        public Color Sample(Vector2 uv)
        {
            float x = Mathf.Clamp01(uv.x) * Width - 0.5f;
            float y = Mathf.Clamp01(uv.y) * Height - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, Width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, Height - 1);
            int x1 = Mathf.Min(x0 + 1, Width - 1), y1 = Mathf.Min(y0 + 1, Height - 1);
            float tx = Mathf.Clamp01(x - x0), ty = Mathf.Clamp01(y - y0);

            Color bottom = Color.Lerp(pixels[y0 * Width + x0], pixels[y0 * Width + x1], tx);
            Color top = Color.Lerp(pixels[y1 * Width + x0], pixels[y1 * Width + x1], tx);
            return Color.Lerp(bottom, top, ty);
        }

        /// <summary>Bilinear sample of one channel (0 = R … 3 = A).</summary>
        public float Sample(Vector2 uv, int channel) => Sample(uv)[Mathf.Clamp(channel, 0, 3)];

        /// <summary>Nearest-texel sample; use for hard region IDs where blending between texels makes no sense.</summary>
        public Color SamplePoint(Vector2 uv)
        {
            int x = Mathf.Clamp((int)(Mathf.Clamp01(uv.x) * Width), 0, Width - 1);
            int y = Mathf.Clamp((int)(Mathf.Clamp01(uv.y) * Height), 0, Height - 1);
            return pixels[y * Width + x];
        }

        /// <summary>
        /// The mask UV at a raycast hit. Works for MeshColliders (whose mesh has the UV set) and TerrainColliders.
        /// Other colliders carry no UVs, so this returns false for them.
        /// </summary>
        public static bool TryGetUV(RaycastHit hit, int uvChannel, out Vector2 uv)
        {
            uv = default;
            switch (hit.collider)
            {
                case TerrainCollider terrainCollider when terrainCollider.TryGetComponent(out Terrain terrain):
                    uv = TerrainUV(terrain, hit.point);
                    return true;
                case MeshCollider meshCollider when meshCollider.sharedMesh != null && !meshCollider.convex:
                    uv = uvChannel == 1 ? hit.textureCoord2 : hit.textureCoord;
                    return uvChannel <= 1;
                default:
                    return false;
            }
        }

        /// <summary>Terrain mask UV under a world position (height is ignored). Matches what the painter writes.</summary>
        public static Vector2 TerrainUV(Terrain terrain, Vector3 worldPosition)
        {
            Vector3 local = worldPosition - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return new Vector2(local.x / size.x, local.z / size.z);
        }

        private static Color32[] ReadThroughGpu(Texture2D mask, int width, int height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(mask, rt);
            RenderTexture.active = rt;

            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            Color32[] result = copy.GetPixels32();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            if (Application.isPlaying) Object.Destroy(copy);
            else Object.DestroyImmediate(copy);
            return result;
        }
    }
}
