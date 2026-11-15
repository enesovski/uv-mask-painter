using System.Collections.Generic;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Painting
{
    public readonly struct Dab
    {
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly float Pressure;

        public Dab(Vector3 position, Vector3 normal, float pressure)
        {
            Position = position;
            Normal = normal;
            Pressure = pressure;
        }
    }

    /// <summary>Evenly spaced world-space dabs regardless of mouse speed. The first point always dabs (click paints).</summary>
    public sealed class StrokeSampler
    {
        private readonly float spacing;
        private Dab last;
        private bool hasLast;
        private float distanceSinceDab;

        public StrokeSampler(float spacing)
        {
            this.spacing = Mathf.Max(spacing, 1e-5f);
        }

        public void Reset() => hasLast = false;

        public void AddPoint(Vector3 position, Vector3 normal, float pressure, List<Dab> output)
        {
            var current = new Dab(position, normal, pressure);
            if (!hasLast)
            {
                output.Add(current);
                last = current;
                hasLast = true;
                distanceSinceDab = 0f;
                return;
            }

            float length = Vector3.Distance(last.Position, position);
            if (length <= 0f) return;

            float next = spacing - distanceSinceDab;
            while (next <= length)
            {
                float t = next / length;
                output.Add(new Dab(
                    Vector3.Lerp(last.Position, position, t),
                    Vector3.Slerp(last.Normal, normal, t),
                    Mathf.Lerp(last.Pressure, pressure, t)));
                next += spacing;
            }

            distanceSinceDab = length - (next - spacing);
            last = current;
        }
    }
}
