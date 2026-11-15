using System;
using UnityEngine;

namespace Archura.UVMaskPainter
{
    /// <summary>
    /// Optional runtime access to a painted mask, for gameplay code: "how wet is it where I stepped?"
    /// Add it from the painter (⋯ ▸ Add Runtime Sampler) so the mask, UV set and channel names are filled in.
    /// One component per mask; an object can have several.
    /// </summary>
    [AddComponentMenu("Archura/Mask Surface")]
    public sealed class MaskSurface : MonoBehaviour
    {
        [SerializeField] private Texture2D mask;
        [SerializeField, Range(0, 3)] private int uvChannel;
        [Tooltip("Names given to the channels in the painter. Look channels up by these names in code.")]
        [SerializeField] private string[] channelNames = { "R", "G", "B", "A" };
        [Tooltip("Downsample the CPU copy to save memory. 0 keeps the full resolution.")]
        [SerializeField, Min(0)] private int maxResolution;

        private MaskSampler sampler;

        public Texture2D Mask
        {
            get => mask;
            set
            {
                mask = value;
                sampler = null;
            }
        }

        public int UVChannel => uvChannel;
        public string[] ChannelNames => channelNames;

        /// <summary>Built on first use; call <see cref="Prepare"/> early to avoid a hitch.</summary>
        public MaskSampler Sampler
        {
            get
            {
                if (sampler == null && mask != null) sampler = new MaskSampler(mask, maxResolution);
                return sampler;
            }
        }

        private void Awake() => Prepare();

        /// <summary>Copies the mask to the CPU now instead of on the first sample.</summary>
        public void Prepare() => _ = Sampler;

        /// <summary>Index of a channel by its painter name (case-insensitive), or -1.</summary>
        public int ChannelIndex(string channelName)
        {
            for (int i = 0; i < channelNames.Length; i++)
                if (string.Equals(channelNames[i], channelName, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public Color Sample(Vector2 uv) => Sampler != null ? Sampler.Sample(uv) : Color.clear;

        /// <summary>Mask value at a raycast hit on this object (MeshCollider or TerrainCollider).</summary>
        public bool TrySample(RaycastHit hit, out Color value)
        {
            value = Color.clear;
            if (Sampler == null || !MaskSampler.TryGetUV(hit, uvChannel, out Vector2 uv)) return false;
            value = Sampler.Sample(uv);
            return true;
        }

        public bool TrySample(RaycastHit hit, string channelName, out float value)
        {
            value = 0f;
            int channel = ChannelIndex(channelName);
            if (channel < 0 || !TrySample(hit, out Color color)) return false;
            value = color[channel];
            return true;
        }

        /// <summary>Terrains only: mask value under a world position, no raycast needed.</summary>
        public bool TrySample(Vector3 worldPosition, out Color value)
        {
            value = Color.clear;
            if (Sampler == null || !TryGetComponent(out Terrain terrain)) return false;
            value = Sampler.Sample(MaskSampler.TerrainUV(terrain, worldPosition));
            return true;
        }

        /// <summary>
        /// Finds a MaskSurface on the hit object (or its parents) that has the named channel and samples it.
        /// Handy for one-liners such as footsteps: <c>MaskSurface.TrySampleAt(hit, "Wet", out float wet)</c>.
        /// </summary>
        public static bool TrySampleAt(RaycastHit hit, string channelName, out float value)
        {
            value = 0f;
            if (hit.collider == null) return false;
            foreach (MaskSurface surface in hit.collider.GetComponentsInParent<MaskSurface>())
                if (surface.ChannelIndex(channelName) >= 0)
                    return surface.TrySample(hit, channelName, out value);
            return false;
        }

        /// <summary>Editor hook: keeps the component in step with the mask's settings in the painter.</summary>
        public void Configure(Texture2D maskTexture, int uv, string[] names)
        {
            Mask = maskTexture;
            uvChannel = Mathf.Clamp(uv, 0, 3);
            channelNames = names ?? channelNames;
        }
    }
}
