#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Artifika.MaskPainter
{
    public sealed class MaskPainterWindow : EditorWindow
    {
        private int width = 1024;
        private int height = 1024;
        private MaskChannels channels = MaskChannels.R;
        private bool linear = true;
        private float r = 0f, g = 0f, b = 0f, a = 1f;

        private MaskAsset previewAsset;
        private Vector2 scrollPosition;

        /* NEW: callback that the inspector can provide */
        private static Action<MaskAsset> s_onCreated;

        public static void OpenCreate(Action<MaskAsset> onCreated = null)
        {
            s_onCreated = onCreated;
            var w = GetWindow<MaskPainterWindow>("Mask Painter • Create");
            w.minSize = new Vector2(400, 500);
            w.Show();
        }

        [MenuItem("Tools/Mask Painter/Create Mask Asset")]
        public static void OpenMenu() => OpenCreate(null);

        private void OnEnable()
        {
            BuildPreview();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Create New Mask Asset", EditorStyles.largeLabel);
            EditorGUILayout.Space(10);

            // Texture Settings
            EditorGUILayout.LabelField("Texture Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                width = EditorGUILayout.IntField("Width", width);
                height = EditorGUILayout.IntField("Height", height);
            }

            // Ensure power of 2 for better performance
            if (!Mathf.IsPowerOfTwo(width) || !Mathf.IsPowerOfTwo(height))
            {
                EditorGUILayout.HelpBox("Non-power-of-2 textures may have performance implications.", MessageType.Warning);
            }

            channels = (MaskChannels)EditorGUILayout.EnumPopup("Channels", channels);
            linear = EditorGUILayout.Toggle("Linear Color Space", linear);

            EditorGUILayout.Space(10);

            // Default Values
            EditorGUILayout.LabelField("Default Values", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Channel-specific sliders based on selected channels
            switch (channels)
            {
                case MaskChannels.R:
                    r = EditorGUILayout.Slider("Red Channel", r, 0f, 1f);
                    break;
                case MaskChannels.RG:
                    r = EditorGUILayout.Slider("Red Channel", r, 0f, 1f);
                    g = EditorGUILayout.Slider("Green Channel", g, 0f, 1f);
                    break;
                case MaskChannels.RGBA:
                    r = EditorGUILayout.Slider("Red Channel", r, 0f, 1f);
                    g = EditorGUILayout.Slider("Green Channel", g, 0f, 1f);
                    b = EditorGUILayout.Slider("Blue Channel", b, 0f, 1f);
                    a = EditorGUILayout.Slider("Alpha Channel", a, 0f, 1f);
                    break;
            }

            EditorGUILayout.Space(10);

            // Preview
            if (previewAsset != null && previewAsset.Texture != null)
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                var rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(rect, previewAsset.Texture, null, ScaleMode.ScaleToFit);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Export as PNG", GUILayout.Height(25)))
                    {
                        ExportPreviewPNG();
                    }
                }
            }

            EditorGUILayout.Space(20);

            // Create Button
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create Mask Asset", GUILayout.Height(35), GUILayout.Width(200)))
                {
                    CreateMaskAsset();
                }
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndScrollView();

            // Auto-update preview when values change
            if (GUI.changed)
            {
                BuildPreview();
            }
        }

        private void BuildPreview()
        {
            if (previewAsset == null)
            {
                previewAsset = CreateInstance<MaskAsset>();
                previewAsset.name = "UnsavedMaskPreview";
            }

            previewAsset.width = width;
            previewAsset.height = height;
            previewAsset.channels = channels;
            previewAsset.linearColor = linear;
            previewAsset.defaultR = r;
            previewAsset.defaultG = g;
            previewAsset.defaultB = b;
            previewAsset.defaultA = a;

            previewAsset.ReinitializeTexture();
            Repaint();
        }

        private void ExportPreviewPNG()
        {
            if (previewAsset?.Texture == null) return;

            var path = EditorUtility.SaveFilePanelInProject(
                "Export Mask as PNG", "MaskExport.png", "png",
                "Choose location for the exported PNG");

            if (string.IsNullOrEmpty(path)) return;

            var bytes = ImageConversion.EncodeToPNG(previewAsset.Texture);
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path);
            EditorUtility.DisplayDialog("Export Complete", $"Saved PNG:\n{path}", "OK");
        }

        private void CreateMaskAsset()
        {
            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Create Mask Asset",
                "NewMaskAsset.asset",
                "asset",
                "Select a location for the new MaskAsset");

            if (string.IsNullOrEmpty(assetPath)) return;

            var asset = CreateInstance<MaskAsset>();
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            asset.width = width;
            asset.height = height;
            asset.channels = channels;
            asset.linearColor = linear;
            asset.defaultR = r;
            asset.defaultG = g;
            asset.defaultB = b;
            asset.defaultA = a;

            Undo.RegisterCreatedObjectUndo(asset, "Create MaskAsset");
            AssetDatabase.CreateAsset(asset, assetPath); // creates .asset on disk

            asset.ReinitializeTexture();
            asset.EnsurePersistence();                   // add sub-asset texture & mark dirty

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            // NEW: notify whoever opened us with a callback
            var cb = s_onCreated;
            s_onCreated = null;
            cb?.Invoke(asset);

            Close();
        }
    }
}
#endif
