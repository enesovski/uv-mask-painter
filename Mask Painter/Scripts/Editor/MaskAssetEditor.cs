#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Artifika.MaskPainter
{
    [CustomEditor(typeof(MaskAsset))]
    public sealed class MaskAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (MaskAsset)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();

            Texture2D tex = asset.Texture;
            using (new EditorGUI.DisabledScope(tex == null))
            {
                if (tex == null)
                {
                    EditorGUILayout.HelpBox("No mask texture found. Click 'Reinitialize' (or create a mask) first.", MessageType.Info);
                }
                else
                {
                    Rect rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawPreviewTexture(rect, tex, null, ScaleMode.ScaleToFit);

                    EditorGUILayout.Space();

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Extract Mask as PNG...", GUILayout.Height(24)))
                        {
                            ExtractMaskPNG(asset);
                        }

                        if (GUILayout.Button("Ping Texture", GUILayout.Height(24)))
                        {
                            EditorGUIUtility.PingObject(tex);
                        }
                    }
                }
            }
        }

        private static void ExtractMaskPNG(MaskAsset asset)
        {
            Texture2D tex = asset.Texture;
            if (tex == null)
            {
                EditorUtility.DisplayDialog("Extract Mask", "No mask texture on this asset.", "OK");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(asset);
            string dir = string.IsNullOrEmpty(assetPath) ? "Assets" : Path.GetDirectoryName(assetPath);
            string defaultName = asset.name + "_Mask.png";

            string savePath = EditorUtility.SaveFilePanelInProject("Extract Mask as PNG",
                defaultName, "png", "Choose where to save the extracted mask PNG.", dir);

            if (string.IsNullOrEmpty(savePath))
                return;

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(savePath, bytes);
            AssetDatabase.ImportAsset(savePath);

            TextureImporter ti = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (ti != null)
            {
                ti.sRGBTexture = !asset.linearColor;               // linearColor=true => sRGB off
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Point;
                ti.alphaIsTransparency = false;
                ti.SaveAndReimport();
            }

            Texture2D extracted = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
            EditorGUIUtility.PingObject(extracted);
            EditorUtility.DisplayDialog("Extract Mask", $"Saved: {savePath}", "OK");
        }
    }
}
#endif
