using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Editor.Geometry;
using Archura.UVMaskPainter.Editor.Painting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>Scene view drawing: mask overlay on the mesh and the brush cursor.</summary>
    internal static class SceneDrawing
    {
        private const string PreviewShader = "Hidden/Archura/UVMaskPainter/Preview";
        private static readonly string[] UVKeywords = { null, "_UV1", "_UV2", "_UV3" };
        private static Material previewMaterial;

        public static Color ChannelColor(MaskChannels channels)
        {
            if (channels == MaskChannels.R) return new Color(1f, 0.35f, 0.3f);
            if (channels == MaskChannels.G) return new Color(0.4f, 1f, 0.45f);
            if (channels == MaskChannels.B) return new Color(0.4f, 0.65f, 1f);
            return Color.white;
        }

        public static void DrawMaskOverlay(PainterSession session, MaskPainterSettings settings)
        {
            // A mask no material uses has no other way to be seen.
            if ((!settings.ShowMask && session.HasLivePreview) || session.Document == null || session.Surface == null) return;

            if (previewMaterial == null)
                previewMaterial = new Material(Shader.Find(PreviewShader)) { hideFlags = HideFlags.HideAndDontSave };

            MaskChannels channels = settings.Brush.Channels;
            previewMaterial.SetTexture("_MaskTex", session.Document.Texture);
            previewMaterial.SetVector("_Channels", new Vector4(
                (channels & MaskChannels.R) != 0 ? 1 : 0, (channels & MaskChannels.G) != 0 ? 1 : 0,
                (channels & MaskChannels.B) != 0 ? 1 : 0, (channels & MaskChannels.A) != 0 ? 1 : 0));
            previewMaterial.SetFloat("_Opacity", settings.MaskOpacity);
            for (int i = 1; i < UVKeywords.Length; i++)
            {
                if (i == session.Surface.UVChannel) previewMaterial.EnableKeyword(UVKeywords[i]);
                else previewMaterial.DisableKeyword(UVKeywords[i]);
            }

            previewMaterial.SetPass(0);
            Mesh mesh = session.Surface.Mesh;
            if (session.Submesh >= 0) Graphics.DrawMeshNow(mesh, session.Surface.LocalToWorld, session.Submesh);
            else
                for (int s = 0; s < mesh.subMeshCount; s++)
                    Graphics.DrawMeshNow(mesh, session.Surface.LocalToWorld, s);
        }

        public static void DrawBrushCursor(in SurfaceHit hit, BrushSettings brush, BrushMode mode, PaintTool tool, Vector2 mousePosition, string adjustLabel = null)
        {
            Color color = mode == BrushMode.Erase ? new Color(0.85f, 0.85f, 0.85f)
                        : mode == BrushMode.Smooth ? new Color(0.5f, 0.9f, 1f)
                        : ChannelColor(brush.Channels);
            Vector3 lift = hit.Normal * (HandleUtility.GetHandleSize(hit.Point) * 0.002f);
            Vector3 center = hit.Point + lift;

            CompareFunction previousZTest = Handles.zTest;
            if (tool == PaintTool.Fill)
            {
                Handles.zTest = CompareFunction.Always;
                Handles.color = color;
                float size = HandleUtility.GetHandleSize(hit.Point) * 0.08f;
                Handles.DrawWireDisc(center, hit.Normal, size, 2f);
                Handles.DrawSolidDisc(center, hit.Normal, size * 0.25f);
            }
            else
            {
                // Visible part solid, hidden part faint.
                Handles.zTest = CompareFunction.LessEqual;
                Handles.color = new Color(color.r, color.g, color.b, 0.12f);
                Handles.DrawSolidDisc(center, hit.Normal, brush.Radius);
                Handles.color = color;
                Handles.DrawWireDisc(center, hit.Normal, brush.Radius, 2f);
                if (brush.Hardness < 0.98f)
                {
                    Handles.color = new Color(color.r, color.g, color.b, 0.5f);
                    Handles.DrawWireDisc(center, hit.Normal, brush.Radius * brush.Hardness);
                }
                Handles.zTest = CompareFunction.Greater;
                Handles.color = new Color(color.r, color.g, color.b, 0.25f);
                Handles.DrawWireDisc(center, hit.Normal, brush.Radius);
            }
            Handles.zTest = previousZTest;

            string label = adjustLabel != null ? adjustLabel
                         : tool == PaintTool.Fill ? (mode == BrushMode.Erase ? "Clear island" : "Fill island  ·  Shift: whole mask")
                         : mode == BrushMode.Erase ? "Erase"
                         : mode == BrushMode.Smooth ? "Smooth"
                         : null;
            if (label == null) return;
            Handles.BeginGUI();
            GUI.Label(new Rect(mousePosition.x + 18f, mousePosition.y + 10f, 220f, 18f), label, EditorStyles.whiteMiniLabel);
            Handles.EndGUI();
        }
    }
}
