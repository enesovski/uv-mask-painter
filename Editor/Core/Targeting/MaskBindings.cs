using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Archura.UVMaskPainter.Editor.Targeting
{
    /// <summary>
    /// Connecting masks to material properties. Binding is always explicit, except that a new mask
    /// fills an empty property tagged [ArchuraMask] by the shader author.
    /// </summary>
    public static class MaskBindings
    {
        // Shader usage: [ArchuraMask] _Wear ("Wear", 2D) = "black" {}
        public const string MaskAttribute = "ArchuraMask";

        /// <summary>Every visible 2D texture property of every material on the renderer (or the terrain's material).</summary>
        public static List<MaskBinding> GetOptions(PaintSubject subject)
        {
            var options = new List<MaskBinding>();
            if (subject == null || !subject.IsAlive) return options;
            Material[] materials = subject.SharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                foreach (string property in GetTextureProperties(materials[i]))
                    options.Add(new MaskBinding(subject, i, property));
            return options;
        }

        /// <summary>First empty [ArchuraMask] property on an editable material, or null.</summary>
        public static MaskBinding SuggestFor(PaintSubject subject)
        {
            foreach (MaskBinding option in GetOptions(subject))
                if (option.Texture == null && HasMaskAttribute(option.Material.shader, option.PropertyName) && CanBind(option, out _))
                    return option;
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

        public static bool HasMaskAttribute(Shader shader, string propertyName)
        {
            int index = shader.FindPropertyIndex(propertyName);
            if (index < 0) return false;
            foreach (string attribute in shader.GetPropertyAttributes(index))
                if (string.Equals(attribute, MaskAttribute, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static bool CanBind(MaskBinding binding, out string reason)
        {
            reason = null;
            Material material = binding?.Material;
            if (material == null)
            {
                reason = "The material slot is empty.";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path)) return true; // scene-owned material instance

            if (path.StartsWith("Packages/") || !path.EndsWith(".mat"))
            {
                reason = $"'{material.name}' is read-only (it comes from {Path.GetFileName(path)}). " +
                         "Extract or duplicate the material to use a mask in it.";
                return false;
            }
            return true;
        }

        /// <summary>Sets (or clears, with null) the property's texture. Undoable.</summary>
        public static void Bind(MaskBinding binding, Texture2D texture)
        {
            Material material = binding.Material;
            if (material == null) return;
            Undo.RecordObject(material, texture != null ? "Use Mask in Material" : "Remove Mask from Material");
            material.SetTexture(binding.PropertyName, texture);
            EditorUtility.SetDirty(material);
        }
    }
}
