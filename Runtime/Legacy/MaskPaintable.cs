using UnityEngine;

namespace Archura.UVMaskPainter.Legacy
{
    /// <summary>v1 marker component; no longer needed. Safe to remove after converting its mask.</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class MaskPaintable : MonoBehaviour
    {
        public MaskAsset mask;
        public Collider targetCollider;
    }
}
