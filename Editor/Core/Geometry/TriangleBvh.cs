using System;
using System.Collections.Generic;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Geometry
{
    /// <summary>Local-space triangle BVH for picking and sphere queries.</summary>
    internal sealed class TriangleBvh
    {
        private const int LeafSize = 8;

        private struct Node
        {
            public Bounds Bounds;
            public int Start;      // leaf: first index into order[]; inner: index of left child
            public int Count;      // leaf: triangle count; inner: 0
        }

        private readonly List<Node> nodes = new List<Node>();
        private readonly int[] order;              // triangle indices, grouped by leaf
        private readonly Vector3[] triangleMin;
        private readonly Vector3[] triangleMax;
        private readonly Vector3[] centroids;
        private readonly Stack<int> stack = new Stack<int>();

        public TriangleBvh(Vector3[] vertices, int[] triangles)
        {
            int triangleCount = triangles.Length / 3;
            order = new int[triangleCount];
            triangleMin = new Vector3[triangleCount];
            triangleMax = new Vector3[triangleCount];
            centroids = new Vector3[triangleCount];

            for (int t = 0; t < triangleCount; t++)
            {
                Vector3 a = vertices[triangles[t * 3]];
                Vector3 b = vertices[triangles[t * 3 + 1]];
                Vector3 c = vertices[triangles[t * 3 + 2]];
                triangleMin[t] = Vector3.Min(a, Vector3.Min(b, c));
                triangleMax[t] = Vector3.Max(a, Vector3.Max(b, c));
                centroids[t] = (a + b + c) / 3f;
                order[t] = t;
            }

            if (triangleCount > 0)
            {
                nodes.Add(default);
                Build(0, 0, triangleCount);
            }
        }

        public void Query(Func<Bounds, bool> nodeTest, Action<int> visitTriangle)
        {
            if (nodes.Count == 0) return;

            stack.Clear();
            stack.Push(0);
            while (stack.Count > 0)
            {
                Node node = nodes[stack.Pop()];
                if (!nodeTest(node.Bounds)) continue;

                if (node.Count > 0)
                {
                    for (int i = node.Start; i < node.Start + node.Count; i++)
                        visitTriangle(order[i]);
                }
                else
                {
                    stack.Push(node.Start);
                    stack.Push(node.Start + 1);
                }
            }
        }

        public Bounds GetTriangleBounds(int triangle)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(triangleMin[triangle], triangleMax[triangle]);
            return bounds;
        }

        private void Build(int nodeIndex, int start, int count)
        {
            Vector3 min = triangleMin[order[start]];
            Vector3 max = triangleMax[order[start]];
            for (int i = start + 1; i < start + count; i++)
            {
                min = Vector3.Min(min, triangleMin[order[i]]);
                max = Vector3.Max(max, triangleMax[order[i]]);
            }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);

            if (count <= LeafSize)
            {
                nodes[nodeIndex] = new Node { Bounds = bounds, Start = start, Count = count };
                return;
            }

            // Median split on the longest axis (quickselect, no full sort).
            Vector3 size = max - min;
            int axis = size.x > size.y ? (size.x > size.z ? 0 : 2) : (size.y > size.z ? 1 : 2);
            int half = count / 2;
            SelectNth(start, start + count - 1, start + half, axis);

            // Children are stored side by side: left = Start, right = Start + 1.
            int left = nodes.Count;
            nodes.Add(default);
            nodes.Add(default);
            nodes[nodeIndex] = new Node { Bounds = bounds, Start = left, Count = 0 };

            Build(left, start, half);
            Build(left + 1, start + half, count - half);
        }

        private void SelectNth(int lo, int hi, int nth, int axis)
        {
            while (hi > lo)
            {
                float pivot = centroids[order[(lo + hi) / 2]][axis];
                int i = lo, j = hi;
                while (i <= j)
                {
                    while (centroids[order[i]][axis] < pivot) i++;
                    while (centroids[order[j]][axis] > pivot) j--;
                    if (i > j) break;
                    (order[i], order[j]) = (order[j], order[i]);
                    i++;
                    j--;
                }

                if (nth <= j) hi = j;
                else if (nth >= i) lo = i;
                else return;
            }
        }
    }
}
