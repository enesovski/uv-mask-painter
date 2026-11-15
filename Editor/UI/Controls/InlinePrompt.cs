using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>One-line text prompt shown inside the panel (rename mask, rename channel, name preset).</summary>
    internal sealed class InlinePrompt : VisualElement
    {
        private readonly Label title = new Label();
        private readonly TextField field = new TextField();
        private Action<string> commit;

        public InlinePrompt()
        {
            style.display = DisplayStyle.None;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginTop = 4;

            title.style.marginRight = 4;
            title.style.opacity = 0.8f;
            field.style.flexGrow = 1;
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Commit();
                else if (e.keyCode == KeyCode.Escape) Hide();
            });
            Add(title);
            Add(field);
            Add(new Button(Commit) { text = "OK" });
            Add(new Button(Hide) { text = "×" });
        }

        public void Show(string label, string initial, Action<string> onCommit)
        {
            title.text = label;
            field.value = initial ?? string.Empty;
            commit = onCommit;
            style.display = DisplayStyle.Flex;
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
        }

        private void Commit()
        {
            Action<string> action = commit;
            string text = field.value;
            Hide();
            action?.Invoke(text.Trim());   // empty is allowed (resets a channel name)
        }

        private void Hide()
        {
            commit = null;
            style.display = DisplayStyle.None;
        }
    }
}
