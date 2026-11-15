using System.Collections.Generic;
using Archura.UVMaskPainter.Editor.Document;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>Inline "+ New Mask" form: name, channel count, resolution, or an existing texture to adopt.</summary>
    internal sealed class NewMaskForm : VisualElement
    {
        private static readonly List<string> Resolutions = new List<string> { "Auto", "256", "512", "1024", "2048", "4096" };

        private readonly TextField nameField = new TextField();
        private readonly List<Button> channelButtons = new List<Button>();
        private readonly DropdownField resolution = new DropdownField("Size", Resolutions, 0);
        private readonly ObjectField existing = new ObjectField("From Texture") { objectType = typeof(Texture2D), allowSceneObjects = false };
        private int channelCount;

        private static MaskPainterSettings Settings => MaskPainterSettings.instance;

        public NewMaskForm()
        {
            style.display = DisplayStyle.None;
            style.marginTop = 4;
            style.paddingLeft = 6;
            style.paddingTop = 4;
            style.paddingBottom = 4;
            style.borderLeftWidth = 3;
            style.borderLeftColor = new Color(0.24f, 0.45f, 0.75f);

            nameField.label = "Name";
            nameField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Create();
                else if (e.keyCode == KeyCode.Escape) Hide();
            });
            Add(nameField);

            var channels = PainterPanel.Row();
            channels.Add(new Label("Channels") { style = { width = 120, paddingLeft = 3 } });
            for (int count = 1; count <= MaskInfo.MaxChannels; count++)
            {
                int c = count;
                Button button = PainterPanel.Chip(MaskInfo.ChannelSetName(c), () => SetChannels(c));
                button.tooltip = c == 1 ? "One channel (smallest, imported as R8)" : $"{c} channels";
                channelButtons.Add(button);
                channels.Add(button);
            }
            Add(channels);

            resolution.tooltip = "Auto matches the material's main texture (1024 if there is none).";
            Add(resolution);
            existing.tooltip = "Optional: turn an existing texture into this object's mask instead of creating a new file.";
            existing.RegisterValueChangedCallback(e =>
            {
                if (e.newValue != null) nameField.value = e.newValue.name;
                resolution.SetEnabled(e.newValue == null);
            });
            Add(existing);

            var buttons = PainterPanel.Row();
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 2;
            buttons.Add(new Button(Hide) { text = "Cancel" });
            buttons.Add(new Button(Create) { text = "Create" });
            Add(buttons);
        }

        public void Toggle()
        {
            if (style.display == DisplayStyle.Flex) Hide();
            else Show();
        }

        private void Show()
        {
            PainterSession session = PainterSession.Active;
            if (session == null) return;
            nameField.value = MaskFactory.DefaultName(session.Subject);
            existing.value = null;
            resolution.index = 0;
            resolution.SetEnabled(true);
            SetChannels(Settings.NewMaskChannels);
            style.display = DisplayStyle.Flex;
            nameField.schedule.Execute(() =>
            {
                nameField.Focus();
                nameField.SelectAll();
            });
        }

        private void Hide() => style.display = DisplayStyle.None;

        private void SetChannels(int count)
        {
            channelCount = count;
            for (int i = 0; i < channelButtons.Count; i++)
                PainterPanel.SetChipActive(channelButtons[i], i + 1 == count, new Color(0.24f, 0.45f, 0.75f));
        }

        private void Create()
        {
            PainterSession session = PainterSession.Active;
            if (session == null) return;
            int size = resolution.index > 0 ? int.Parse(resolution.value) : 0;
            session.CreateMask(nameField.value, channelCount, size, existing.value as Texture2D);
            Hide();
        }
    }
}
