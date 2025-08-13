using System;
using UnityEngine;

namespace Artifika.MaskPainter
{
    public enum MaskChannels
    {
        R = 1,
        RG = 2,
        RGBA = 4
    }

    [CreateAssetMenu(fileName = "MaskAsset", menuName = "Mask Painter/Mask Asset")]
    public sealed class MaskAsset : ScriptableObject
    {
        [Header("Mask Settings")]
        [Min(1)] 
        public int width = 1024;

        [Min(1)] 
        public int height = 1024;

        public MaskChannels channels = MaskChannels.R;

        [Tooltip("If true, the underlying Texture2D is created in Linear (non-sRGB).")]
        public bool linearColor = true;

        [Header("Default Values")]
        [Range(0, 1f)] public float defaultR = 0f;
        [Range(0, 1f)] public float defaultG = 0f;
        [Range(0, 1f)] public float defaultB = 0f;
        [Range(0, 1f)] public float defaultA = 1f;

        [Header("Generated")]
        [SerializeField, HideInInspector] private Texture2D maskTexture;
        public Texture2D Texture => maskTexture;

        public TextureFormat GetPreferredFormat()
        {
            switch (channels)
            {
                case MaskChannels.R: 
                    return TextureFormat.R8;     
                case MaskChannels.RG: 
                    return TextureFormat.RG16;   
                default: 
                    return TextureFormat.RGBA32; 
            }
        }

        public void ReinitializeTexture()
        {
            TextureFormat txtFormat = GetPreferredFormat();

            if (maskTexture == null || maskTexture.width != width || maskTexture.height != height || maskTexture.format != txtFormat)
            {
                maskTexture = new Texture2D(width, height, txtFormat, false, linearColor)
                {
                    name = $"{name}_MaskTex",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            //CPU-side buffer 
            Color fill = new Color(defaultR, defaultG, defaultB, defaultA);
            Color[] colors = new Color[width * height];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = fill;
            }

            maskTexture.SetPixels(colors);
            maskTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

#if UNITY_EDITOR
        public void EnsurePersistence()
        {
            if (maskTexture == null) 
                ReinitializeTexture();

            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(path)) 
                return; 

            string currentPath = UnityEditor.AssetDatabase.GetAssetPath(maskTexture);
            if (string.IsNullOrEmpty(currentPath))
            {
                UnityEditor.AssetDatabase.AddObjectToAsset(maskTexture, this);
                UnityEditor.AssetDatabase.ImportAsset(path);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(maskTexture);
        }
#endif
    }
}
