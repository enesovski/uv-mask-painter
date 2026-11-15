using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Archura.UVMaskPainter.Editor.Targeting
{
    /// <summary>
    /// The object being painted: a MeshRenderer, a SkinnedMeshRenderer or a Terrain.
    /// Hides their differences (materials, property blocks, bounds, UVs) from the rest of the engine.
    /// </summary>
    public sealed class PaintSubject : IEquatable<PaintSubject>
    {
        public Component Component { get; }
        public Renderer Renderer { get; }
        public Terrain Terrain { get; }

        private PaintSubject(Component component)
        {
            Component = component;
            Renderer = component as Renderer;
            Terrain = component as Terrain;
        }

        public static implicit operator PaintSubject(Renderer renderer) => From(renderer);
        public static implicit operator PaintSubject(Terrain terrain) => From(terrain);

        /// <summary>Null unless the component is a MeshRenderer, SkinnedMeshRenderer or Terrain.</summary>
        public static PaintSubject From(Component component)
        {
            if (component is MeshRenderer || component is SkinnedMeshRenderer || component is Terrain)
                return component != null ? new PaintSubject(component) : null;
            return null;
        }

        public static PaintSubject From(GameObject gameObject)
        {
            if (gameObject == null) return null;
            if (gameObject.TryGetComponent(out MeshRenderer mesh)) return new PaintSubject(mesh);
            if (gameObject.TryGetComponent(out SkinnedMeshRenderer skinned)) return new PaintSubject(skinned);
            if (gameObject.TryGetComponent(out Terrain terrain) && terrain.terrainData != null) return new PaintSubject(terrain);
            return null;
        }

        public bool IsAlive => Component != null;
        public GameObject GameObject => Component != null ? Component.gameObject : null;
        public string Name => Component != null ? Component.name : "<none>";

        public Material[] SharedMaterials
        {
            get
            {
                if (Renderer != null) return Renderer.sharedMaterials;
                if (Terrain != null) return new[] { Terrain.materialTemplate };
                return Array.Empty<Material>();
            }
        }

        public Bounds Bounds
        {
            get
            {
                if (Renderer != null) return Renderer.bounds;
                if (Terrain != null && Terrain.terrainData != null)
                {
                    Bounds local = Terrain.terrainData.bounds;
                    return new Bounds(Terrain.transform.position + local.center, local.size);
                }
                return default;
            }
        }

        /// <summary>The mesh asset the renderer draws; null for terrains.</summary>
        public Mesh SourceMesh
        {
            get
            {
                if (Renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
                return Renderer != null && Renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            }
        }

        /// <summary>Terrains have one UV set: the normalized position on the terrain.</summary>
        public bool HasUV(int uvChannel)
        {
            if (Terrain != null) return uvChannel == 0;
            Mesh mesh = SourceMesh;
            return mesh != null && mesh.HasVertexAttribute(VertexAttribute.TexCoord0 + uvChannel);
        }

        public void GetPropertyBlock(MaterialPropertyBlock block, int materialIndex)
        {
            if (Renderer != null) Renderer.GetPropertyBlock(block, materialIndex);
            else if (Terrain != null) Terrain.GetSplatMaterialPropertyBlock(block);
        }

        /// <summary>Null (or an empty block) restores the material's own values.</summary>
        public void SetPropertyBlock(MaterialPropertyBlock block, int materialIndex)
        {
            if (block != null && block.isEmpty) block = null;
            if (Renderer != null) Renderer.SetPropertyBlock(block, materialIndex);
            else if (Terrain != null) Terrain.SetSplatMaterialPropertyBlock(block ?? new MaterialPropertyBlock());
        }

        public override string ToString() => Name;

        public bool Equals(PaintSubject other) => other != null && Component == other.Component;
        public override bool Equals(object obj) => Equals(obj as PaintSubject);
        public override int GetHashCode() => Component != null ? Component.GetInstanceID() : 0;
        public static bool operator ==(PaintSubject a, PaintSubject b) => ReferenceEquals(a, b) || (!(a is null) && a.Equals(b));
        public static bool operator !=(PaintSubject a, PaintSubject b) => !(a == b);
    }
}
