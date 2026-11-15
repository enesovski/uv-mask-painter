using UnityEngine;

namespace Archura.UVMaskPainter.Legacy
{
    /// <summary>v1 mask container, kept only so old projects load and can be converted to PNG.</summary>
    public sealed class MaskAsset : ScriptableObject
    {
        // Names must match v1 serialized data.
        [SerializeField] private int width = 1024;
        [SerializeField] private int height = 1024;
        [SerializeField] private int channels = 1;
        [SerializeField] private bool linearColor = true;
        [SerializeField, HideInInspector] private Texture2D maskTexture;

        public Texture2D Texture => maskTexture;
        public bool LinearColor => linearColor;
    }
}
