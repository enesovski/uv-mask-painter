using System;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Targeting
{
    /// <summary>Renderer + material slot + texture property + UV set. Immutable; nothing is added to the scene.</summary>
    public sealed class PaintTarget : IEquatable<PaintTarget>
    {
        public Renderer Renderer { get; }
        public int MaterialIndex { get; }
        public string PropertyName { get; }
        public int UVChannel { get; }

        public PaintTarget(Renderer renderer, int materialIndex, string propertyName, int uvChannel = 0)
        {
            Renderer = renderer;
            MaterialIndex = materialIndex;
            PropertyName = propertyName;
            UVChannel = uvChannel;
        }

        public Material Material
        {
            get
            {
                if (Renderer == null) return null;
                Material[] materials = Renderer.sharedMaterials;
                return MaterialIndex >= 0 && MaterialIndex < materials.Length ? materials[MaterialIndex] : null;
            }
        }

        public Texture2D Texture => Material != null && Material.HasTexture(PropertyName)
            ? Material.GetTexture(PropertyName) as Texture2D
            : null;

        // Extra material slots render the last submesh.
        public int GetSubmesh(Mesh mesh) => Mathf.Clamp(MaterialIndex, 0, Mathf.Max(0, mesh.subMeshCount - 1));

        public bool IsValid => Material != null && Material.HasTexture(PropertyName);

        public override string ToString() =>
            $"{(Renderer != null ? Renderer.name : "<none>")} ▸ {(Material != null ? Material.name : "<none>")} ▸ {PropertyName} (UV{UVChannel})";

        public bool Equals(PaintTarget other) =>
            other != null && Renderer == other.Renderer && MaterialIndex == other.MaterialIndex &&
            PropertyName == other.PropertyName && UVChannel == other.UVChannel;

        public override bool Equals(object obj) => Equals(obj as PaintTarget);

        public override int GetHashCode() => HashCode.Combine(Renderer, MaterialIndex, PropertyName, UVChannel);
    }
}
