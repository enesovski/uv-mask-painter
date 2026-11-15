using Archura.UVMaskPainter.Editor.Document;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>Adds MaskSurface components and keeps them in step with the mask's settings in the painter.</summary>
    internal static class MaskSurfaceSetup
    {
        /// <summary>Adds (or finds) the runtime sampler for this mask on the object, and selects it.</summary>
        public static MaskSurface Add(GameObject target, MaskEntry entry)
        {
            foreach (MaskSurface existing in target.GetComponents<MaskSurface>())
            {
                if (existing.Mask != entry.Texture) continue;
                Sync(existing, entry.Info);
                Selection.activeGameObject = target;
                return existing;
            }

            var surface = Undo.AddComponent<MaskSurface>(target);
            Sync(surface, entry.Info, entry.Texture);
            Debug.Log($"[UV Mask Painter] Added a Mask Surface for '{entry.Name}' to '{target.name}'. " +
                      "Read it in code with MaskSurface.TrySampleAt(hit, \"ChannelName\", out float value).", surface);
            return surface;
        }

        /// <summary>Updates every MaskSurface in the open scenes that uses this mask.</summary>
        public static void SyncAll(MaskEntry entry)
        {
            foreach (MaskSurface surface in Object.FindObjectsByType<MaskSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (surface.Mask == entry.Texture || (surface.Mask != null && AssetDatabase.GetAssetPath(surface.Mask) == entry.Path))
                    Sync(surface, entry.Info, AssetDatabase.LoadAssetAtPath<Texture2D>(entry.Path));
        }

        public static void Sync(MaskSurface surface, MaskInfo info, Texture2D texture = null)
        {
            string[] names = new string[info.ChannelCount];
            for (int i = 0; i < names.Length; i++) names[i] = info.GetChannelName(i);
            if (texture == null) texture = surface.Mask;
            if (surface.Mask == texture && surface.UVChannel == info.UVChannel && SameNames(surface.ChannelNames, names)) return;

            Undo.RecordObject(surface, "Sync Mask Surface");
            surface.Configure(texture, info.UVChannel, names);
            EditorUtility.SetDirty(surface);
        }

        private static bool SameNames(string[] a, string[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }

    [CustomEditor(typeof(MaskSurface)), CanEditMultipleObjects]
    internal sealed class MaskSurfaceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var surface = (MaskSurface)target;
            bool isMask = MaskInfo.TryRead(surface.Mask, out MaskInfo info);

            // The painter owns UV set and channel names; mirror them so code can look channels up by name.
            if (isMask && targets.Length == 1) MaskSurfaceSetup.Sync(surface, info);

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mask"));
            using (new EditorGUI.DisabledScope(isMask))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("uvChannel"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("channelNames"), true);
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxResolution"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.HelpBox(isMask
                ? "UV set and channel names come from the mask (change them in the painter). Sample in code with MaskSurface.TrySampleAt(hit, \"Name\", out float value)."
                : "Assign a mask made with UV Mask Painter to fill in its UV set and channel names.", MessageType.None);
        }
    }
}
