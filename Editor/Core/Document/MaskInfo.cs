using System;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Document
{
    /// <summary>
    /// Mask metadata, stored in the texture's .meta (importer userData) and tagged with an asset label.
    /// A texture carrying it is a mask; nothing is added to the scene.
    /// </summary>
    [Serializable]
    public sealed class MaskInfo
    {
        public const string Label = "ArchuraMask";
        public const int MaxChannels = 4;
        private const int CurrentVersion = 1;
        private static readonly string[] DefaultNames = { "R", "G", "B", "A" };

        [SerializeField] private int version;
        [SerializeField] private int channels = 1;
        [SerializeField] private string[] channelNames = new string[MaxChannels];
        [SerializeField] private string owner = string.Empty;
        [SerializeField] private int uvChannel;

        public int ChannelCount { get => channels; set => channels = Mathf.Clamp(value, 1, MaxChannels); }
        /// <summary>Mesh (or scene object) the mask was made for. See <see cref="MaskLibrary.OwnerKey"/>.</summary>
        public string Owner { get => owner; set => owner = value ?? string.Empty; }
        public int UVChannel { get => uvChannel; set => uvChannel = Mathf.Clamp(value, 0, 3); }
        /// <summary>The channels this mask has, e.g. R|G for a two-channel mask.</summary>
        public MaskChannels Channels => (MaskChannels)((1 << channels) - 1);

        public MaskInfo(int channelCount = 1, string owner = null, int uvChannel = 0)
        {
            ChannelCount = channelCount;
            Owner = owner;
            UVChannel = uvChannel;
        }

        public string GetChannelName(int index)
        {
            string name = channelNames != null && index < channelNames.Length ? channelNames[index] : null;
            return string.IsNullOrWhiteSpace(name) ? DefaultNames[index] : name;
        }

        public bool HasCustomName(int index) => GetChannelName(index) != DefaultNames[index];

        public void SetChannelName(int index, string name)
        {
            if (channelNames == null || channelNames.Length < MaxChannels) Array.Resize(ref channelNames, MaxChannels);
            channelNames[index] = name?.Trim() ?? string.Empty;
        }

        public static string ChannelSetName(int channelCount) => "RGBA".Substring(0, Mathf.Clamp(channelCount, 1, MaxChannels));

        // ---------- Storage ----------

        [Serializable]
        private sealed class Envelope
        {
            public MaskInfo archuraMask;
        }

        public static bool TryRead(Texture texture, out MaskInfo info)
        {
            info = null;
            return texture != null && TryRead(AssetDatabase.GetAssetPath(texture), out info);
        }

        public static bool TryRead(string assetPath, out MaskInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(assetPath) || !(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) return false;
            if (string.IsNullOrEmpty(importer.userData)) return false;
            try
            {
                info = JsonUtility.FromJson<Envelope>(importer.userData)?.archuraMask;
            }
            catch (ArgumentException)
            {
                info = null;   // userData written by another tool
            }
            if (info != null && info.version < CurrentVersion) info = null;
            return info != null;
        }

        /// <summary>Stores the metadata, labels the asset and applies import settings for the channel count.</summary>
        public void Write(string assetPath)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) return;
            version = CurrentVersion;
            importer.userData = JsonUtility.ToJson(new Envelope { archuraMask = this });
            TextureIO.ConfigureMaskImporter(importer, channels);
            importer.SaveAndReimport();

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            string[] labels = AssetDatabase.GetLabels(texture);
            if (Array.IndexOf(labels, Label) < 0)
            {
                Array.Resize(ref labels, labels.Length + 1);
                labels[labels.Length - 1] = Label;
                AssetDatabase.SetLabels(texture, labels);
            }
            MaskLibrary.Invalidate();
        }

        public MaskInfo Clone()
        {
            var clone = (MaskInfo)MemberwiseClone();
            clone.channelNames = (string[])channelNames?.Clone() ?? new string[MaxChannels];
            return clone;
        }
    }
}
