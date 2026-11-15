using System.Collections.Generic;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Geometry
{
    /// <summary>Triangle to UV island id. Welds by position + UV: hard-edge splits join, stacked UVs stay apart.</summary>
    internal static class UVIslands
    {
        private const float UVPrecision = 1e5f;
        private const float PositionPrecision = 1e4f;

        private readonly struct WeldKey
        {
            private readonly Vector2Int uv;
            private readonly Vector3Int position;

            public WeldKey(Vector2 uv, Vector3 position)
            {
                this.uv = Vector2Int.RoundToInt(uv * UVPrecision);
                this.position = Vector3Int.RoundToInt(position * PositionPrecision);
            }

            public override int GetHashCode() => uv.GetHashCode() * 397 ^ position.GetHashCode();
            public override bool Equals(object obj) => obj is WeldKey other && other.uv == uv && other.position == position;
        }

        public static int[] Compute(int[] triangles, Vector3[] vertices, Vector2[] uvs, out int islandCount)
        {
            int triangleCount = triangles.Length / 3;

            var weldIds = new Dictionary<WeldKey, int>();
            var vertexToWeld = new int[uvs.Length];
            for (int v = 0; v < uvs.Length; v++)
            {
                var key = new WeldKey(uvs[v], vertices[v]);
                if (!weldIds.TryGetValue(key, out int id))
                {
                    id = weldIds.Count;
                    weldIds.Add(key, id);
                }
                vertexToWeld[v] = id;
            }

            var parent = new int[weldIds.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            for (int t = 0; t < triangleCount; t++)
            {
                int a = vertexToWeld[triangles[t * 3]];
                Union(parent, a, vertexToWeld[triangles[t * 3 + 1]]);
                Union(parent, a, vertexToWeld[triangles[t * 3 + 2]]);
            }

            var rootToIsland = new Dictionary<int, int>();
            var result = new int[triangleCount];
            for (int t = 0; t < triangleCount; t++)
            {
                int root = Find(parent, vertexToWeld[triangles[t * 3]]);
                if (!rootToIsland.TryGetValue(root, out int island))
                {
                    island = rootToIsland.Count;
                    rootToIsland.Add(root, island);
                }
                result[t] = island;
            }

            islandCount = rootToIsland.Count;
            return result;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int rootA = Find(parent, a);
            int rootB = Find(parent, b);
            if (rootA != rootB) parent[rootB] = rootA;
        }
    }
}
