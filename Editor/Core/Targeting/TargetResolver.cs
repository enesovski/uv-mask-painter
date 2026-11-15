using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Archura.UVMaskPainter.Editor.Targeting
{
    /// <summary>
    /// Picks the property to paint without asking: remembered choice, then [ArchuraMask], then mask-like name.
    /// Null means nothing fits (the tool paints an unassigned preview mask).
    /// </summary>
    public static class TargetResolver
    {
        // Shader usage: [ArchuraMask] _Wear ("Wear", 2D) = "black" {}
        public const string MaskAttribute = "ArchuraMask";

        private const string MemoryKeyPrefix = "Archura.UVMaskPainter.LastTarget.";
        private static readonly string[] MaskNameHints = { "mask", "splat", "control", "blend", "weight" };

        public static PaintTarget Resolve(Renderer renderer)
        {
            if (renderer == null) return null;
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                if (TryRecall(materials[i], out string property, out int uvChannel))
                    return new PaintTarget(renderer, i, property, uvChannel);
            }

            for (int i = 0; i < materials.Length; i++)
            {
                foreach (string property in GetTextureProperties(materials[i]))
                    if (HasMaskAttribute(materials[i].shader, property))
                        return new PaintTarget(renderer, i, property);
            }

            for (int i = 0; i < materials.Length; i++)
            {
                foreach (string property in GetTextureProperties(materials[i]))
                    if (LooksLikeMask(property))
                        return new PaintTarget(renderer, i, property);
            }

            return null;
        }

        public static List<string> GetTextureProperties(Material material)
        {
            var result = new List<string>();
            if (material == null || material.shader == null) return result;

            Shader shader = material.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                if (shader.GetPropertyTextureDimension(i) != TextureDimension.Tex2D) continue;
                if ((shader.GetPropertyFlags(i) & ShaderPropertyFlags.HideInInspector) != 0) continue;
                result.Add(shader.GetPropertyName(i));
            }
            return result;
        }

        public static void Remember(PaintTarget target)
        {
            if (target == null || !TryGetMemoryKey(target.Material, out string key)) return;
            EditorPrefs.SetString(key, $"{target.PropertyName}|{target.UVChannel}");
        }

        public static void Forget(Material material)
        {
            if (TryGetMemoryKey(material, out string key)) EditorPrefs.DeleteKey(key);
        }

        internal static bool LooksLikeMask(string propertyName)
        {
            foreach (string hint in MaskNameHints)
                if (propertyName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        internal static bool HasMaskAttribute(Shader shader, string propertyName)
        {
            int index = shader.FindPropertyIndex(propertyName);
            if (index < 0) return false;
            foreach (string attribute in shader.GetPropertyAttributes(index))
                if (string.Equals(attribute, MaskAttribute, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool TryRecall(Material material, out string property, out int uvChannel)
        {
            property = null;
            uvChannel = 0;
            if (!TryGetMemoryKey(material, out string key)) return false;

            string[] parts = EditorPrefs.GetString(key, string.Empty).Split('|');
            if (parts.Length != 2 || !material.HasTexture(parts[0]) || !int.TryParse(parts[1], out uvChannel))
                return false;

            property = parts[0];
            return true;
        }

        private static bool TryGetMemoryKey(Material material, out string key)
        {
            key = null;
            if (material == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long localId))
                return false;
            key = $"{MemoryKeyPrefix}{guid}.{localId}";
            return true;
        }
    }
}
