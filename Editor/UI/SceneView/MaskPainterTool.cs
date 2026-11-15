using Archura.UVMaskPainter.Editor.Geometry;
using Archura.UVMaskPainter.Editor.Painting;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>
    /// Scene view paint mode. Appears in the Tools overlay for any selected renderer (or press P).
    /// LMB paints · Ctrl erases · Shift smooths · hold S/A/D and move to adjust · double-click another mesh to switch.
    /// </summary>
    // Unity matches component tools by exact type, so each supported renderer gets its own entry.
    [EditorTool("UV Mask Painter", typeof(MeshRenderer))]
    internal sealed class MeshMaskPainterTool : MaskPainterTool { }

    [EditorTool("UV Mask Painter", typeof(SkinnedMeshRenderer))]
    internal sealed class SkinnedMaskPainterTool : MaskPainterTool { }

    internal abstract class MaskPainterTool : EditorTool
    {
        public static bool IsActive => ToolManager.activeToolType != null && typeof(MaskPainterTool).IsAssignableFrom(ToolManager.activeToolType);

        /// <summary>Enters paint mode for the selected renderer. Returns false if nothing paintable is selected.</summary>
        public static bool Activate()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) return false;
            if (selected.GetComponent<MeshRenderer>() != null) ToolManager.SetActiveTool<MeshMaskPainterTool>();
            else if (selected.GetComponent<SkinnedMeshRenderer>() != null) ToolManager.SetActiveTool<SkinnedMaskPainterTool>();
            else return false;
            return true;
        }

        private static GUIContent icon;
        private readonly PaintModeShortcutContext shortcutContext = new PaintModeShortcutContext();
        private MaskStroke stroke;
        private SurfaceHit hover;
        private bool hasHover;
        private bool adjusting;
        private Vector2 adjustMouse;

        public override GUIContent toolbarIcon =>
            icon ?? (icon = new GUIContent(EditorGUIUtility.IconContent("Grid.PaintTool").image, "UV Mask Painter (P)"));

        private static MaskPainterSettings Settings => MaskPainterSettings.instance;

        public override void OnActivated()
        {
            ShortcutManager.RegisterContext(shortcutContext);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            PainterSession.Begin(target as Renderer);
            SceneView.RepaintAll();
        }

        public override void OnWillBeDeactivated()
        {
            EndStroke();
            ShortcutManager.UnregisterContext(shortcutContext);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            PainterSession.End();
            SceneView.RepaintAll();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView view)) return;

            // Selection changed to another renderer while the tool stays active.
            var renderer = target as Renderer;
            if (PainterSession.Active == null || PainterSession.Active.Renderer != renderer)
            {
                EndStroke();
                PainterSession.Begin(renderer);
            }

            PainterSession session = PainterSession.Active;
            if (session == null || !session.CanPaint) return;

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);

            HandleInput(e, view, session, controlId);

            if (e.type == EventType.Repaint)
            {
                SceneDrawing.DrawMaskOverlay(session, Settings);
                if (hasHover)
                {
                    // While adjusting, the ring stays where the adjustment started.
                    Vector2 labelPosition = BrushAdjust.IsActive ? adjustMouse : e.mousePosition;
                    SceneDrawing.DrawBrushCursor(hover, Settings.Brush, ModeFor(e), Settings.Tool, labelPosition, BrushAdjust.Label());
                }
            }
        }

        private void HandleInput(Event e, SceneView view, PainterSession session, int controlId)
        {
            if (BrushAdjust.IsActive && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) && GUIUtility.hotControl != controlId)
            {
                if (!adjusting) adjustMouse = e.mousePosition;
                adjusting = true;
                BrushAdjust.Track(e.mousePosition);
                view.Repaint();
                e.Use();
                return;
            }
            if (!BrushAdjust.IsActive) adjusting = false;

            switch (e.type)
            {
                case EventType.MouseMove:
                    UpdateHover(e, session);
                    view.Repaint();
                    break;

                case EventType.MouseDown when e.button == 0 && !e.alt:
                    if (e.clickCount == 2 && TrySwitchTarget(e.mousePosition, session))
                    {
                        e.Use();
                        break;
                    }
                    if (!UpdateHover(e, session) || IsOccluded(e.mousePosition, session.Renderer)) break;

                    GUIUtility.hotControl = controlId;
                    BrushSettings brush = Settings.Brush;
                    if (Settings.Tool == PaintTool.Fill)
                    {
                        float value = e.control ? 0f : brush.Value;
                        if (e.shift) session.FillAll(brush.Channels, value);
                        else session.FillIsland(hover, brush.Channels, value);
                    }
                    else
                    {
                        BrushSettings strokeBrush = brush.Clone();
                        strokeBrush.Mode = ModeFor(e);
                        stroke = session.BeginStroke(strokeBrush);
                        stroke?.AddPoint(hover, Pressure(e));
                    }
                    e.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                    if (stroke != null)
                    {
                        if (UpdateHover(e, session)) stroke.AddPoint(hover, Pressure(e));
                        else stroke.Lift();
                    }
                    e.Use();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    EndStroke();
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;

                case EventType.ScrollWheel when e.control:
                    BrushSettings b = Settings.Brush;
                    float step = e.delta.y > 0 ? -1f : 1f;
                    if (e.shift) b.Hardness += step * 0.05f;
                    else b.Radius *= step > 0 ? 1.1f : 1f / 1.1f;
                    Settings.NotifyChanged();
                    e.Use();
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.Escape:
                    ToolManager.RestorePreviousTool();
                    e.Use();
                    break;

                case EventType.KeyDown:
                case EventType.KeyUp:
                    if (e.keyCode == KeyCode.LeftControl || e.keyCode == KeyCode.RightControl ||
                        e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift)
                        view.Repaint();   // cursor shows the modifier mode
                    break;
            }
        }

        private bool UpdateHover(Event e, PainterSession session)
        {
            hasHover = session.Surface.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out hover);
            return hasHover;
        }

        // Something else in front of the target: don't paint through it.
        private static bool IsOccluded(Vector2 mousePosition, Renderer renderer)
        {
            GameObject picked = HandleUtility.PickGameObject(mousePosition, false);
            return picked != null && picked != renderer.gameObject;
        }

        private static bool TrySwitchTarget(Vector2 mousePosition, PainterSession session)
        {
            GameObject picked = HandleUtility.PickGameObject(mousePosition, false);
            if (picked == null || picked == session.Renderer.gameObject || picked.GetComponent<Renderer>() == null) return false;
            Selection.activeGameObject = picked;
            return true;
        }

        private static BrushMode ModeFor(Event e) =>
            e.control ? BrushMode.Erase : e.shift ? BrushMode.Smooth : BrushMode.Paint;

        private static float Pressure(Event e) => e.pointerType == PointerType.Pen ? e.pressure : 1f;

        private void EndStroke()
        {
            if (stroke == null) return;
            PainterSession.Active?.EndStroke(stroke);
            if (stroke.IsActive) stroke.End();
            stroke = null;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) ToolManager.RestorePreviousTool();
        }
    }
}
