#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Artifika.MaskPainter
{
    [CustomEditor(typeof(MaskPaintable))]
    public sealed class MaskPaintableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var t = (MaskPaintable)target;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("mask"));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create & Assign Mask Asset", GUILayout.Height(22)))
                {
                    MaskPainterWindow.OpenCreate(newAsset =>
                    {
                        if (newAsset == null) return;
                        Undo.RecordObject(t, "Assign Mask Asset");
                        t.mask = newAsset;
                        EditorUtility.SetDirty(t);
                    });
                }

                if (GUILayout.Button("Ping Mask", GUILayout.Height(22)))
                {
                    if (t.mask != null) EditorGUIUtility.PingObject(t.mask);
                }
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("targetCollider"));

            EditorGUILayout.Space();

            Collider col = t.GetCollider();
            if (!(col is MeshCollider))
            {
                EditorGUILayout.HelpBox("UV painting requires a MeshCollider. Other collider types will not provide RaycastHit.textureCoord.", MessageType.Info);
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Mask Painter", GUILayout.Height(26)))
                {
                    MaskPainterEditorWindow.OpenWindow();
                }

                if (GUILayout.Button("Clear Mask", GUILayout.Height(26)))
                {
                    if (t.mask != null && t.mask.Texture != null)
                    {
                        if (EditorUtility.DisplayDialog("Clear Mask", 
                            "Are you sure you want to clear the mask texture? This action cannot be undone.", 
                            "Clear", "Cancel"))
                        {
                            Undo.RecordObject(t.mask, "Clear Mask Texture");
                            t.mask.ReinitializeTexture();
                            t.mask.EnsurePersistence();
                            EditorUtility.SetDirty(t.mask);
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
