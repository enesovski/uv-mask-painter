using UnityEngine;
using UnityEngine.Rendering;

namespace Archura.UVMaskPainter.Editor.Geometry
{
    /// <summary>
    /// Builds a hidden stand-in mesh for a Terrain, so terrains go through the same UV-space painting as meshes.
    /// UV0 is the normalized position on the terrain (0..1 across its width and length), in terrain-local space.
    /// </summary>
    public static class TerrainMesh
    {
        /// <summary>Vertices per side. Picking and brush distances only need this much detail.</summary>
        public const int MaxResolution = 513;

        public static Mesh Build(Terrain terrain)
        {
            TerrainData data = terrain != null ? terrain.terrainData : null;
            if (data == null) return null;

            int resolution = Mathf.Clamp(data.heightmapResolution, 2, MaxResolution);
            Vector3 size = data.size;
            var vertices = new Vector3[resolution * resolution];
            var uvs = new Vector2[vertices.Length];
            float step = 1f / (resolution - 1);

            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float u = x * step, v = z * step;
                int i = z * resolution + x;
                vertices[i] = new Vector3(u * size.x, data.GetInterpolatedHeight(u, v), v * size.z);
                uvs[i] = new Vector2(u, v);
            }

            // Two triangles per cell, wound so the normal points up.
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
            int t = 0;
            for (int z = 0; z < resolution - 1; z++)
            for (int x = 0; x < resolution - 1; x++)
            {
                int a = z * resolution + x, b = a + resolution, c = a + 1, d = b + 1;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                triangles[t++] = c; triangles[t++] = b; triangles[t++] = d;
            }

            var mesh = new Mesh { name = terrain.name + " (Terrain Surface)", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Terrain UV (0..1) under a world position, ignoring height. Matches what the painter writes.</summary>
        public static Vector2 WorldToUV(Terrain terrain, Vector3 worldPosition)
        {
            Vector3 local = worldPosition - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return new Vector2(local.x / size.x, local.z / size.z);
        }
    }
}
