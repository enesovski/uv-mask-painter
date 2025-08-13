using UnityEngine;

namespace Artifika.MaskPainter
{
    [DisallowMultipleComponent]
    public sealed class MaskPaintable : MonoBehaviour
    {
        [Header("Mask")]
        public MaskAsset mask;

        [Header("Target")]
        [Tooltip("If null, will use a MeshCollider on this GameObject.")]
        public Collider targetCollider;
        public Collider GetCollider()
        {
            if (targetCollider != null) return targetCollider;
            return GetComponent<Collider>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // MeshCollider is required to get UVs from raycasts.
            // (Sphere/Box/Capsule colliders do NOT provide textureCoord.)
            // See docs: RaycastHit.textureCoord requires MeshCollider. 
            // https://docs.unity3d.com/ScriptReference/RaycastHit-textureCoord.html
            Collider col = GetCollider();
            MeshCollider meshCol = col as MeshCollider;
            if (col != null && meshCol == null)
            {
                Debug.LogWarning($"[MaskPaintable] '{name}' uses {col.GetType().Name}. " +
                                 $"UV painting requires a MeshCollider. " +
                                 $"Texture coords will be (0,0).", this);
            }
        }
#endif
    }
}
