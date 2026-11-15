using System;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Targeting
{
    /// <summary>Where a mask is used: renderer/terrain + material slot + texture property. Immutable.</summary>
    public sealed class MaskBinding : IEquatable<MaskBinding>
    {
        public PaintSubject Subject { get; }
        public int MaterialIndex { get; }
        public string PropertyName { get; }

        public MaskBinding(PaintSubject subject, int materialIndex, string propertyName)
        {
            Subject = subject;
            MaterialIndex = materialIndex;
            PropertyName = propertyName;
        }

        public Material Material
        {
            get
            {
                if (Subject == null || !Subject.IsAlive) return null;
                Material[] materials = Subject.SharedMaterials;
                return MaterialIndex >= 0 && MaterialIndex < materials.Length ? materials[MaterialIndex] : null;
            }
        }

        public Texture2D Texture => Material != null && Material.HasTexture(PropertyName)
            ? Material.GetTexture(PropertyName) as Texture2D
            : null;

        public bool IsValid => Material != null && Material.HasTexture(PropertyName);

        /// <summary>Inspector name of the property, e.g. "Wear".</summary>
        public string DisplayName
        {
            get
            {
                Material material = Material;
                if (material == null) return PropertyName;
                int index = material.shader.FindPropertyIndex(PropertyName);
                return index >= 0 ? material.shader.GetPropertyDescription(index) : PropertyName;
            }
        }

        // Extra material slots render the last submesh.
        public int GetSubmesh(Mesh mesh) => Mathf.Clamp(MaterialIndex, 0, Mathf.Max(0, mesh.subMeshCount - 1));

        public override string ToString() =>
            $"{(Subject != null ? Subject.Name : "<none>")} ▸ {(Material != null ? Material.name : "<none>")} ▸ {PropertyName}";

        public bool Equals(MaskBinding other) =>
            other != null && Subject == other.Subject && MaterialIndex == other.MaterialIndex && PropertyName == other.PropertyName;

        public override bool Equals(object obj) => Equals(obj as MaskBinding);

        public override int GetHashCode() => HashCode.Combine(Subject, MaterialIndex, PropertyName);
    }
}
