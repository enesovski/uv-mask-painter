#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Artifika.MaskPainter
{
    public static class MaskPainterManager
    {
        private static MaskPainterEditorWindow editorWindow;

        public static MaskPainterTool GetOrCreateTool()
        {
            return MaskPainterTool.Active; 
        }

        public static void SetEditorWindow(MaskPainterEditorWindow window)
        {
            editorWindow = window;
        }

        public static void SyncBrushSettings(float radius, float strength, float hardness, Color color)
        {
            var tool = GetOrCreateTool();
            if (tool != null)
            {
                tool.SetBrushSettings(radius, strength, hardness, color);
                SceneView.RepaintAll();
            }
        }

        public static void SyncToolSettings(int tool, int channel)
        {
            var paintingTool = GetOrCreateTool();
            if (paintingTool != null)
            {
                paintingTool.SetToolSettings(tool, channel);
            }
        }

        public static void SetCurrentPaintable(MaskPaintable paintable)
        {
            var tool = GetOrCreateTool();
            if (tool != null)
            {
                tool.SetCurrentPaintable(paintable);
            }
            
            if (paintable == null && IsPaintingToolActive())
            {
                DeactivatePaintingTool();
            }
        }

        public static void ActivatePaintingTool()
        {
            ToolManager.SetActiveTool<MaskPainterTool>();
            SceneView.RepaintAll();
        }

        public static void DeactivatePaintingTool()
        {
            ToolManager.RestorePreviousTool();
        }

        public static bool IsPaintingToolActive()
        {
            return ToolManager.activeToolType == typeof(MaskPainterTool);
        }

        public static void RefreshEditorWindow()
        {
            if (editorWindow != null)
            {
                editorWindow.Repaint();
            }
        }

        public static void ShowNotification(string message)
        {
            if (editorWindow != null)
            {
                editorWindow.ShowNotification(new GUIContent(message));
            }
        }

        public static void ClearNotification()
        {
            if (editorWindow != null)
            {
                editorWindow.RemoveNotification();
            }
        }
    }
}
#endif
