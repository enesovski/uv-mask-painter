using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Geometry
{
    /// <summary>Result of a ray hitting a <see cref="MeshSurface"/>.</summary>
    public readonly struct SurfaceHit
    {
        public readonly Vector3 Point;     // world space
        public readonly Vector3 Normal;    // world space; interpolated mesh normal (face normal if the mesh has none)
        public readonly Vector2 UV;        // in the surface's UV channel
        public readonly float Distance;    // world units along the ray
        public readonly int Triangle;
        public readonly int Submesh;

        public SurfaceHit(Vector3 point, Vector3 normal, Vector2 uv, float distance, int triangle, int submesh)
        {
            Point = point;
            Normal = normal;
            UV = uv;
            Distance = distance;
            Triangle = triangle;
            Submesh = submesh;
        }
    }
}
