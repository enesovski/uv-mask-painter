#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Artifika.MaskPainter
{
    public sealed class MaskPainterEditorWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private MaskPaintable selectedPaintable;
        private MaskAsset selectedMask;
        
        // Brush settings
        private float brushRadius = 3f;
        private float brushStrength = 0.5f;
        private float brushHardness = 0.5f;
        
        // Channel selection
        private int selectedChannel = 0;
        private string[] channelNames = { "Red", "Green", "Blue", "Alpha" };
        
        // Tool selection
        private int selectedTool = 0;
        private string[] toolNames = { "Paint", "Erase", "Smooth", "Fill" };
        
        // UI state
        private bool showBrushSettings = true;
        private bool showChannelSettings = true;
        private bool showToolSettings = true;

        [MenuItem("Tools/Mask Painter/Mask Painter Window")]
        public static void OpenWindow()
        {
            var window = GetWindow<MaskPainterEditorWindow>("Mask Painter");
            window.minSize = new Vector2(300, 400);
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            MaskPainterManager.SetEditorWindow(this);
            OnSelectionChanged();
            
            MaskPainterManager.SyncBrushSettings(brushRadius, brushStrength, brushHardness, GetChannelColor());
            MaskPainterManager.SyncToolSettings(selectedTool, selectedChannel);
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            MaskPainterManager.SetEditorWindow(null);
        }

        private void OnSelectionChanged()
        {
            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                selectedPaintable = selected.GetComponent<MaskPaintable>();
                if (selectedPaintable != null)
                {
                    selectedMask = selectedPaintable.mask;
                    MaskPainterManager.SetCurrentPaintable(selectedPaintable);
                }
                else
                {
                    selectedPaintable = null;
                    selectedMask = null;
                    MaskPainterManager.SetCurrentPaintable(null);
                }
            }
            else
            {
                selectedPaintable = null;
                selectedMask = null;
                MaskPainterManager.SetCurrentPaintable(null);
            }
            Repaint();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawSelectionInfo();
            EditorGUILayout.Space(10);
            
            DrawToolSelection();
            EditorGUILayout.Space(10);
            
            DrawBrushSettings();
            EditorGUILayout.Space(10);
            
            DrawChannelSettings();
            EditorGUILayout.Space(10);
                        
            DrawActionButtons();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Mask Painter", EditorStyles.largeLabel);
        }

        private void DrawSelectionInfo()
        {
            EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
            
            if (selectedPaintable == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with MaskPaintable component to start painting.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Object:", selectedPaintable.name);
            
            if (selectedMask == null)
            {
                EditorGUILayout.HelpBox("No mask asset assigned. Create or assign a mask asset to start painting.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Mask:", selectedMask.name);
            EditorGUILayout.LabelField("Resolution:", $"{selectedMask.width} x {selectedMask.height}");
            EditorGUILayout.LabelField("Channels:", selectedMask.channels.ToString());
        }

        private void DrawToolSelection()
        {
            showToolSettings = EditorGUILayout.Foldout(showToolSettings, "Tools", true);
            if (!showToolSettings) return;

            EditorGUILayout.Space(5);
            
            int newTool = GUILayout.Toolbar(selectedTool, toolNames);
            if (newTool != selectedTool)
            {
                selectedTool = newTool;
                if (MaskPainterManager.IsPaintingToolActive())
                {
                    MaskPainterManager.SyncToolSettings(selectedTool, selectedChannel);
                    MaskPainterManager.SyncBrushSettings(brushRadius, brushStrength, brushHardness, GetChannelColor());
                }
            }
            
            EditorGUILayout.Space(5);
            
            switch (selectedTool)
            {
                case 0: // Paint
                    EditorGUILayout.HelpBox("Paint: Apply brush color to the selected channel.", MessageType.Info);
                    break;
                case 1: // Erase
                    EditorGUILayout.HelpBox("Erase: Remove paint from the selected channel.", MessageType.Info);
                    break;
                case 2: // Smooth
                    EditorGUILayout.HelpBox("Smooth: Blur the selected channel.", MessageType.Info);
                    break;
                case 3: // Fill
                    EditorGUILayout.HelpBox("Fill: Fill the entire texture with the brush color.", MessageType.Info);
                    break;
            }
        }

        private void DrawBrushSettings()
        {
            showBrushSettings = EditorGUILayout.Foldout(showBrushSettings, "Brush Settings", true);
            if (!showBrushSettings) return;

            EditorGUILayout.Space(5);
            
            float newRadius = EditorGUILayout.Slider("Radius", brushRadius, 0.01f, 10f);
            float newStrength = EditorGUILayout.Slider("Strength", brushStrength, 0f, 1f);
            float newHardness = EditorGUILayout.Slider("Hardness", brushHardness, 0f, 1f);
            
            // Sync changes with the painting tool
            if (newRadius != brushRadius || newStrength != brushStrength || 
                newHardness != brushHardness)
            {
                brushRadius = newRadius;
                brushStrength = newStrength;
                brushHardness = newHardness;
                
                MaskPainterManager.SyncBrushSettings(brushRadius, brushStrength, brushHardness, GetChannelColor());
            }
        }

        private void DrawChannelSettings()
        {
            showChannelSettings = EditorGUILayout.Foldout(showChannelSettings, "Channel Settings", true);
            if (!showChannelSettings) return;

            EditorGUILayout.Space(5);
            
            if (selectedMask != null)
            {
                int maxChannels = (int)selectedMask.channels;
                string[] availableChannels = new string[maxChannels];
                
                for (int i = 0; i < maxChannels; i++)
                {
                    availableChannels[i] = channelNames[i];
                }
                
                int newChannel = EditorGUILayout.Popup("Target Channel", selectedChannel, availableChannels);
                if (newChannel != selectedChannel)
                {
                    selectedChannel = newChannel;
                    MaskPainterManager.SyncToolSettings(selectedTool, selectedChannel);
                    MaskPainterManager.SyncBrushSettings(brushRadius, brushStrength, brushHardness, GetChannelColor());
                }
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Channel Preview:");
                var rect = GUILayoutUtility.GetRect(200, 100, GUILayout.ExpandWidth(false));
                DrawChannelPreview(rect);
            }
            else
            {
                EditorGUILayout.HelpBox("No mask selected. Channel settings will be available when a mask is assigned.", MessageType.Info);
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space(10);
            
            using (new EditorGUI.DisabledScope(selectedPaintable == null || selectedMask == null))
            {
                bool isActive = MaskPainterManager.IsPaintingToolActive();
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.backgroundColor = isActive ? Color.red : Color.green;
                    if (GUILayout.Button(isActive ? "Stop Painting" : "Start Painting", GUILayout.Height(30)))
                    {
                        if (isActive)
                            StopPainting();
                        else
                            StartPainting();
                    }
                    GUI.backgroundColor = Color.white;
                    
                    if (isActive)
                    {
                        EditorGUILayout.LabelField("Tool Active", EditorStyles.boldLabel);
                    }
                }
                
                EditorGUILayout.Space(5);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Clear Channel", GUILayout.Height(25)))
                    {
                        ClearChannel();
                    }
                    
                    if (GUILayout.Button("Invert Channel", GUILayout.Height(25)))
                    {
                        InvertChannel();
                    }
                }
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Export Mask", GUILayout.Height(25)))
                {
                    ExportMask();
                }
            }
        }

        private Color GetChannelColor()
        {
            switch (selectedChannel)
            {
                case 0: return Color.red;      // Red channel
                case 1: return Color.green;    // Green channel
                case 2: return Color.blue;     // Blue channel
                case 3: return Color.white;    // Alpha channel
                default: return Color.white;
            }
        }

        private void DrawChannelPreview(Rect rect)
        {
            if (selectedMask?.Texture == null) return;
            
            Texture2D previewTexture = CreateChannelPreviewTexture(selectedMask.Texture, selectedChannel);
            if (previewTexture != null)
            {
                EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit);
                DestroyImmediate(previewTexture);
            }
        }

        private Texture2D CreateChannelPreviewTexture(Texture2D sourceTexture, int channel)
        {
            if (sourceTexture == null) return null;
            
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            Texture2D previewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            
            Color[] sourcePixels = sourceTexture.GetPixels();
            Color[] previewPixels = new Color[sourcePixels.Length];
            
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                float channelValue = GetChannelValue(sourcePixels[i], channel);
                
                switch (channel)
                {
                    case 0: // Red
                        previewPixels[i] = new Color(channelValue, 0, 0, 1);
                        break;
                    case 1: // Green
                        previewPixels[i] = new Color(0, channelValue, 0, 1);
                        break;
                    case 2: // Blue
                        previewPixels[i] = new Color(0, 0, channelValue, 1);
                        break;
                    case 3: // Alpha
                        previewPixels[i] = new Color(channelValue, channelValue, channelValue, 1);
                        break;
                }
            }
            
            previewTexture.SetPixels(previewPixels);
            previewTexture.Apply(false, false);
            return previewTexture;
        }

        private void StartPainting()
        {
            if (selectedPaintable == null || selectedMask == null) return;

            MaskPainterManager.SetCurrentPaintable(selectedPaintable);

            MaskPainterManager.ActivatePaintingTool();

            MaskPainterManager.SyncToolSettings(selectedTool, selectedChannel);
            MaskPainterManager.SyncBrushSettings(brushRadius, brushStrength, brushHardness, GetChannelColor());

            MaskPainterManager.ShowNotification("Mask Painter Tool Activated - Use mouse to paint");
        }


        private void StopPainting()
        {
            MaskPainterManager.DeactivatePaintingTool();
            MaskPainterManager.ClearNotification();
        }

        private void ClearChannel()
        {
            if (selectedMask == null) return;
            
            if (EditorUtility.DisplayDialog("Clear Channel", 
                $"Are you sure you want to clear the {channelNames[selectedChannel]} channel?", 
                "Clear", "Cancel"))
            {
                Undo.RecordObject(selectedMask, "Clear Channel");
                
                Color[] pixels = selectedMask.Texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = SetChannelValue(pixels[i], selectedChannel, 0f);
                }
                
                selectedMask.Texture.SetPixels(pixels);
                selectedMask.Texture.Apply(false, false);
                EditorUtility.SetDirty(selectedMask);
            }
        }

        private void InvertChannel()
        {
            if (selectedMask == null) return;
            
            if (EditorUtility.DisplayDialog("Invert Channel", 
                $"Are you sure you want to invert the {channelNames[selectedChannel]} channel?", 
                "Invert", "Cancel"))
            {
                Undo.RecordObject(selectedMask, "Invert Channel");
                
                Color[] pixels = selectedMask.Texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    float currentValue = GetChannelValue(pixels[i], selectedChannel);
                    pixels[i] = SetChannelValue(pixels[i], selectedChannel, 1f - currentValue);
                }
                
                selectedMask.Texture.SetPixels(pixels);
                selectedMask.Texture.Apply(false, false);
                EditorUtility.SetDirty(selectedMask);
            }
        }

        private float GetChannelValue(Color color, int channel)
        {
            switch (channel)
            {
                case 0: return color.r;
                case 1: return color.g;
                case 2: return color.b;
                case 3: return color.a;
                default: return color.r;
            }
        }

        private Color SetChannelValue(Color color, int channel, float value)
        {
            Color newColor = color;
            switch (channel)
            {
                case 0: newColor.r = value; break;
                case 1: newColor.g = value; break;
                case 2: newColor.b = value; break;
                case 3: newColor.a = value; break;
            }
            return newColor;
        }

        private void ExportMask()
        {
            if (selectedMask?.Texture == null) return;
            
            var path = EditorUtility.SaveFilePanelInProject(
                "Export Mask", "MaskExport.png", "png", "Choose location for the exported mask");
            
            if (!string.IsNullOrEmpty(path))
            {
                byte[] pngData = selectedMask.Texture.EncodeToPNG();
                System.IO.File.WriteAllBytes(path, pngData);
                AssetDatabase.ImportAsset(path);
            }
        }
    }
}
#endif
