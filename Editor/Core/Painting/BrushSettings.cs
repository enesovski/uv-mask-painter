using System;
using Archura.UVMaskPainter.Editor.Document;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Painting
{
    public enum BrushMode
    {
        Paint,  // writes Value
        Erase,  // writes 0
        Smooth  // blurs
    }

    /// <summary>Radius is in world units. Strength is the max opacity of one stroke (no build-up).</summary>
    [Serializable]
    public sealed class BrushSettings
    {
        public const float MinRadius = 0.001f;
        public const float MaxRadius = 100f;

        [SerializeField] private BrushMode mode = BrushMode.Paint;
        [SerializeField] private MaskChannels channels = MaskChannels.R;
        [SerializeField] private float radius = 0.25f;
        [SerializeField] private float strength = 1f;
        [SerializeField] private float hardness = 0.5f;
        [SerializeField] private float value = 1f;
        [SerializeField] private float spacing = 0.15f;

        public BrushMode Mode { get => mode; set => mode = value; }
        public MaskChannels Channels { get => channels; set => channels = value; }
        public float Radius { get => radius; set => radius = Mathf.Clamp(value, MinRadius, MaxRadius); }
        public float Strength { get => strength; set => strength = Mathf.Clamp01(value); }
        public float Hardness { get => hardness; set => hardness = Mathf.Clamp01(value); }
        public float Value { get => value; set => this.value = Mathf.Clamp01(value); }
        // Fraction of the radius.
        public float Spacing { get => spacing; set => spacing = Mathf.Clamp(value, 0.02f, 1f); }

        public float TargetValue => mode == BrushMode.Erase ? 0f : value;

        public BrushSettings Clone() => (BrushSettings)MemberwiseClone();
    }
}
