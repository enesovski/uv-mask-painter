using Archura.UVMaskPainter.Editor.Document;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>Active only while painting in the Scene view, so these keys override Unity's defaults only then.</summary>
    internal sealed class PaintModeShortcutContext : IShortcutContext
    {
        public bool active => MaskPainterTool.IsActive && EditorWindow.focusedWindow is SceneView;
    }

    /// <summary>All rebindable in Edit ▸ Shortcuts ▸ Archura.</summary>
    internal static class MaskPainterShortcuts
    {
        private const string Root = "Archura/UV Mask Painter/";
        private const float ValueStep = 0.1f;
        private static MaskPainterSettings Settings => MaskPainterSettings.instance;

        [Shortcut(Root + "Toggle Paint Mode", KeyCode.P)]
        private static void TogglePaintMode()
        {
            if (MaskPainterTool.IsActive)
            {
                ToolManager.RestorePreviousTool();
                return;
            }
            if (!MaskPainterTool.Activate() && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Select a mesh to paint"), 1.5f);
        }

        // Hold and move the mouse sideways, like Terrain Tools (S size, A opacity).
        [ClutchShortcut(Root + "Adjust Size (hold)", typeof(PaintModeShortcutContext), KeyCode.S)]
        private static void AdjustSize(ShortcutArguments args) => Adjust(args, BrushProperty.Size);

        [ClutchShortcut(Root + "Adjust Opacity (hold)", typeof(PaintModeShortcutContext), KeyCode.A)]
        private static void AdjustOpacity(ShortcutArguments args) => Adjust(args, BrushProperty.Opacity);

        [ClutchShortcut(Root + "Adjust Value (hold)", typeof(PaintModeShortcutContext), KeyCode.D)]
        private static void AdjustValue(ShortcutArguments args) => Adjust(args, BrushProperty.Value);

        [Shortcut(Root + "Value Down", typeof(PaintModeShortcutContext), KeyCode.Minus)]
        private static void ValueDown() => Change(() => Settings.Brush.Value = Step(Settings.Brush.Value, -ValueStep));

        [Shortcut(Root + "Value Up", typeof(PaintModeShortcutContext), KeyCode.Equals)]
        private static void ValueUp() => Change(() => Settings.Brush.Value = Step(Settings.Brush.Value, ValueStep));

        [Shortcut(Root + "Flip Value", typeof(PaintModeShortcutContext), KeyCode.X)]
        private static void FlipValue() => Change(() => Settings.Brush.Value = 1f - Settings.Brush.Value);

        [Shortcut(Root + "Brush Smaller", typeof(PaintModeShortcutContext), KeyCode.LeftBracket)]
        private static void Smaller() => Change(() => Settings.Brush.Radius /= 1.15f);

        [Shortcut(Root + "Brush Larger", typeof(PaintModeShortcutContext), KeyCode.RightBracket)]
        private static void Larger() => Change(() => Settings.Brush.Radius *= 1.15f);

        [Shortcut(Root + "Brush Softer", typeof(PaintModeShortcutContext), KeyCode.LeftBracket, ShortcutModifiers.Shift)]
        private static void Softer() => Change(() => Settings.Brush.Hardness -= 0.1f);

        [Shortcut(Root + "Brush Harder", typeof(PaintModeShortcutContext), KeyCode.RightBracket, ShortcutModifiers.Shift)]
        private static void Harder() => Change(() => Settings.Brush.Hardness += 0.1f);

        [Shortcut(Root + "Channel 1", typeof(PaintModeShortcutContext), KeyCode.Alpha1)] private static void Channel1() => SelectChannel(MaskChannels.R);
        [Shortcut(Root + "Channel 2", typeof(PaintModeShortcutContext), KeyCode.Alpha2)] private static void Channel2() => SelectChannel(MaskChannels.G);
        [Shortcut(Root + "Channel 3", typeof(PaintModeShortcutContext), KeyCode.Alpha3)] private static void Channel3() => SelectChannel(MaskChannels.B);
        [Shortcut(Root + "Channel 4", typeof(PaintModeShortcutContext), KeyCode.Alpha4)] private static void Channel4() => SelectChannel(MaskChannels.A);

        [Shortcut(Root + "Toggle Channel 1", typeof(PaintModeShortcutContext), KeyCode.Alpha1, ShortcutModifiers.Shift)] private static void Toggle1() => ToggleChannel(MaskChannels.R);
        [Shortcut(Root + "Toggle Channel 2", typeof(PaintModeShortcutContext), KeyCode.Alpha2, ShortcutModifiers.Shift)] private static void Toggle2() => ToggleChannel(MaskChannels.G);
        [Shortcut(Root + "Toggle Channel 3", typeof(PaintModeShortcutContext), KeyCode.Alpha3, ShortcutModifiers.Shift)] private static void Toggle3() => ToggleChannel(MaskChannels.B);
        [Shortcut(Root + "Toggle Channel 4", typeof(PaintModeShortcutContext), KeyCode.Alpha4, ShortcutModifiers.Shift)] private static void Toggle4() => ToggleChannel(MaskChannels.A);

        [Shortcut(Root + "Toggle Mask View", typeof(PaintModeShortcutContext), KeyCode.V)]
        private static void ToggleMaskView() => Settings.ShowMask = !Settings.ShowMask;

        [Shortcut(Root + "Brush Tool", typeof(PaintModeShortcutContext), KeyCode.B)]
        private static void BrushTool() => Settings.Tool = PaintTool.Brush;

        [Shortcut(Root + "Fill Tool", typeof(PaintModeShortcutContext), KeyCode.G)]
        private static void FillTool() => Settings.Tool = PaintTool.Fill;

        private static void Adjust(ShortcutArguments args, BrushProperty property)
        {
            if (args.stage == ShortcutStage.Begin) BrushAdjust.Begin(property);
            else BrushAdjust.End(property);
        }

        // Snaps to the 0.1 grid so repeated steps land on round values.
        private static float Step(float value, float step) => Mathf.Round((value + step) / ValueStep) * ValueStep;

        private static MaskChannels Available => PainterSession.Active?.AvailableChannels ?? MaskChannels.RGBA;

        private static void SelectChannel(MaskChannels channel)
        {
            if ((Available & channel) != 0) Change(() => Settings.Brush.Channels = channel);
        }

        private static void ToggleChannel(MaskChannels channel)
        {
            if ((Available & channel) == 0) return;
            MaskChannels toggled = Settings.Brush.Channels ^ channel;
            if (toggled != MaskChannels.None) Change(() => Settings.Brush.Channels = toggled);
        }

        private static void Change(System.Action change)
        {
            change();
            Settings.NotifyChanged();
        }
    }
}
