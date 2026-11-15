using System;
using System.Collections.Generic;
using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Editor.Painting;
using Archura.UVMaskPainter.Editor.Targeting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>
    /// The Scene overlay's content: the object's masks, then what the brush paints and how.
    /// Reads/writes <see cref="MaskPainterSettings"/> and <see cref="PainterSession.Active"/>; holds no state of its own.
    /// Messages go to the Console, so the panel only ever shows controls (and a blocking error).
    /// </summary>
    internal sealed class PainterPanel : VisualElement
    {
        private static readonly Color[] ChannelColors =
        {
            new Color(0.85f, 0.3f, 0.28f), new Color(0.3f, 0.7f, 0.35f), new Color(0.3f, 0.5f, 0.9f), new Color(0.6f, 0.6f, 0.6f)
        };
        private static readonly Color Accent = new Color(0.24f, 0.45f, 0.75f);
        private static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.3f);

        private readonly Label title = new Label();
        private readonly Label statusLabel = new Label();
        private readonly VisualElement maskList = new VisualElement();
        private readonly Label errorLabel = new Label();
        private readonly NewMaskForm newMaskForm = new NewMaskForm();
        private readonly InlinePrompt prompt = new InlinePrompt();
        private readonly VisualElement body = new VisualElement();
        private readonly Button brushButton, fillButton;
        private readonly VisualElement channelRow = new VisualElement();
        private readonly Slider size = new Slider(-3f, 2f), value = new Slider(0f, 1f), opacity = new Slider(0f, 1f), hardness = new Slider(0f, 1f);
        private readonly Label sizeLabel = new Label(), valueLabel = new Label(), opacityLabel = new Label(), hardnessLabel = new Label();
        private readonly VisualElement brushOnly = new VisualElement();
        private readonly Toggle showMask = new Toggle("Mask View");
        private readonly Slider overlayOpacity = new Slider(0f, 1f);
        private readonly Label hint = new Label();

        private static MaskPainterSettings Settings => MaskPainterSettings.instance;

        public PainterPanel()
        {
            style.minWidth = 270;

            // Masks of the selected object
            var header = Row();
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            title.style.flexShrink = 1;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            header.Add(title);
            statusLabel.style.opacity = 0.6f;
            statusLabel.style.marginRight = 4;
            statusLabel.style.fontSize = 10;
            header.Add(statusLabel);
            header.Add(new Button(() => newMaskForm.Toggle()) { text = "+ New", tooltip = "Create a mask for this object" });
            Add(header);

            maskList.style.marginTop = 2;
            Add(maskList);
            errorLabel.style.whiteSpace = WhiteSpace.Normal;
            errorLabel.style.marginTop = 3;
            errorLabel.style.paddingLeft = 6;
            errorLabel.style.borderLeftWidth = 3;
            errorLabel.style.borderLeftColor = ErrorColor;
            Add(errorLabel);
            Add(newMaskForm);
            Add(prompt);

            // What to paint
            var toolRow = Row();
            toolRow.style.marginTop = 6;
            brushButton = Chip("Brush", () => Settings.Tool = PaintTool.Brush);
            fillButton = Chip("Fill", () => Settings.Tool = PaintTool.Fill);
            brushButton.tooltip = "Paint with the brush (B)";
            fillButton.tooltip = "Click fills one UV island · Shift-click fills the whole mask (G)";
            toolRow.Add(brushButton);
            toolRow.Add(fillButton);
            toolRow.Add(new VisualElement { style = { flexGrow = 1 } });
            channelRow.style.flexDirection = FlexDirection.Row;
            toolRow.Add(channelRow);
            body.Add(toolRow);

            // How to paint
            body.Add(SliderRow("Value", "The value painted into the selected channels (0 = black, 1 = white).\nHold D and move the mouse · X flips · - / = steps", value, valueLabel, v => Settings.Brush.Value = v));
            brushOnly.Add(SliderRow("Size", "Brush radius.\nHold S and move the mouse · [ / ]", size, sizeLabel, v => Settings.Brush.Radius = Mathf.Pow(10f, v)));
            brushOnly.Add(SliderRow("Opacity", "How far one stroke moves pixels toward Value.\nHold A and move the mouse", opacity, opacityLabel, v => Settings.Brush.Strength = v));
            brushOnly.Add(SliderRow("Hardness", "Soft (0) to hard (1) brush edge.\nShift+[ / Shift+]", hardness, hardnessLabel, v => Settings.Brush.Hardness = v));
            body.Add(brushOnly);

            // View
            var viewRow = Row();
            viewRow.style.marginTop = 4;
            showMask.RegisterValueChangedCallback(e => Settings.ShowMask = e.newValue);
            overlayOpacity.style.flexGrow = 1;
            overlayOpacity.tooltip = "Mask View opacity";
            overlayOpacity.RegisterValueChangedCallback(e => Settings.MaskOpacity = e.newValue);
            viewRow.Add(showMask);
            viewRow.Add(overlayOpacity);
            body.Add(viewRow);

            hint.style.fontSize = 10;
            hint.style.opacity = 0.55f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 4;
            body.Add(hint);
            Add(body);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                PainterSession.StateChanged += RefreshSession;
                MaskPainterSettings.Changed += RefreshSettings;
                RefreshSession();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                PainterSession.StateChanged -= RefreshSession;
                MaskPainterSettings.Changed -= RefreshSettings;
            });
        }

        // ---------- Refresh ----------

        private void RefreshSession()
        {
            PainterSession session = PainterSession.Active;
            SetEnabled(session != null);
            maskList.Clear();
            if (session == null) return;

            title.text = $"Masks · {session.Renderer.name}";
            title.tooltip = "Masks of the selected object: masks its materials use, plus masks made for its mesh.";

            foreach (MaskEntry entry in session.Masks)
                maskList.Add(MaskRow(session, entry, entry.Is(session.ActiveMask)));
            if (session.Masks.Count == 0)
                maskList.Add(new Label("No masks yet. Start painting to create one, or click + New.") { style = { opacity = 0.6f, whiteSpace = WhiteSpace.Normal } });

            // Saving runs in the background after every stroke, so only a pending save is worth showing.
            MaskDocument document = session.Document;
            bool unsaved = document != null && document.IsDirty && !document.IsSaving && !Settings.AutoSave;
            statusLabel.text = document == null ? string.Empty : unsaved ? "● unsaved" : "✓ saved";
            statusLabel.tooltip = document?.AssetPath;

            // A broken surface blocks painting but never the mask list, so another mask can be picked.
            bool blocked = session.Error != null;
            errorLabel.style.display = blocked ? DisplayStyle.Flex : DisplayStyle.None;
            if (blocked)
            {
                int uv = session.ActiveMask?.Info.UVChannel ?? 0;
                errorLabel.text = !session.HasUV(uv)
                    ? $"This mesh has no UV{uv}. Pick another mask, or change this one's UV set (⋯ ▸ UV Set)."
                    : session.Error;
            }
            body.SetEnabled(!blocked);

            RefreshSettings();
        }

        private void RefreshSettings()
        {
            PainterSession session = PainterSession.Active;
            BrushSettings brush = Settings.Brush;
            size.SetValueWithoutNotify(Mathf.Log10(brush.Radius));
            value.SetValueWithoutNotify(brush.Value);
            opacity.SetValueWithoutNotify(brush.Strength);
            hardness.SetValueWithoutNotify(brush.Hardness);
            sizeLabel.text = BrushAdjust.FormatDistance(brush.Radius);
            valueLabel.text = brush.Value.ToString("0.00");
            opacityLabel.text = brush.Strength.ToString("0.00");
            hardnessLabel.text = brush.Hardness.ToString("0.00");

            bool fill = Settings.Tool == PaintTool.Fill;
            brushOnly.style.display = fill ? DisplayStyle.None : DisplayStyle.Flex;
            SetChipActive(brushButton, !fill, Accent);
            SetChipActive(fillButton, fill, Accent);
            hint.text = fill
                ? "Click: fill island · Shift-click: whole mask · Ctrl: clear · B brush"
                : "Hold S size · A opacity · D value · Ctrl erase · Shift smooth · Esc exit";

            // A mask no material uses can only be seen through Mask View, so it's forced on.
            bool forced = session?.ActiveMask != null && !session.HasLivePreview;
            showMask.SetValueWithoutNotify(forced || Settings.ShowMask);
            showMask.SetEnabled(!forced);
            showMask.tooltip = forced
                ? "Always on: no material uses this mask yet (⋯ ▸ Use in Material)."
                : "Tint the mesh with the selected channels (V)";
            overlayOpacity.SetValueWithoutNotify(Settings.MaskOpacity);
            overlayOpacity.style.display = forced || Settings.ShowMask ? DisplayStyle.Flex : DisplayStyle.None;

            RebuildChannelChips(session);
        }

        // ---------- Channels ----------

        private void RebuildChannelChips(PainterSession session)
        {
            channelRow.Clear();
            MaskEntry mask = session?.ActiveMask;
            int count = mask?.Info.ChannelCount ?? Settings.NewMaskChannels;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var channel = (MaskChannels)(1 << i);
                string name = mask != null ? mask.Info.GetChannelName(i) : "RGBA".Substring(i, 1);
                Button chip = Chip(name.Length > 8 ? name.Substring(0, 7) + "…" : name, null);
                chip.tooltip = $"Paint {name} ({i + 1}) · Shift-click: paint several · right-click: rename or remove";
                chip.RegisterCallback<ClickEvent>(e => OnChannelClicked(channel, e.shiftKey));
                chip.RegisterCallback<MouseDownEvent>(e =>
                {
                    if (e.button != 1) return;
                    ShowChannelMenu(chip, session, mask, index, count);
                    e.StopPropagation();
                });
                SetChipActive(chip, (Settings.Brush.Channels & channel) != 0, ChannelColors[i]);
                channelRow.Add(chip);
            }

            if (count < MaskInfo.MaxChannels)
            {
                var add = Chip("+", () => SetChannelCount(session, mask, count + 1));
                add.tooltip = mask != null ? $"Add channel {"RGBA"[count]} to {mask.Name}" : "Channels for the next mask";
                add.style.opacity = 0.7f;
                channelRow.Add(add);
            }
        }

        private void ShowChannelMenu(VisualElement anchor, PainterSession session, MaskEntry mask, int index, int count)
        {
            var menu = new GenericMenu();
            if (mask != null) menu.AddItem(new GUIContent("Rename…"), false, () => RenameChannel(session, mask, index));
            else menu.AddDisabledItem(new GUIContent("Rename… (create a mask first)"));
            // Channels are a count (R, RG, RGB, RGBA), so only the last one can go.
            if (index == count - 1 && count > 1) menu.AddItem(new GUIContent($"Remove {"RGBA"[index]}"), false, () => SetChannelCount(session, mask, count - 1));
            else menu.AddDisabledItem(new GUIContent(count > 1 ? $"Remove (only {"RGBA"[count - 1]} can be removed)" : "Remove (a mask needs one channel)"));
            menu.DropDown(anchor.worldBound);
        }

        private static void SetChannelCount(PainterSession session, MaskEntry mask, int count)
        {
            if (mask != null) session.SetChannelCount(mask, count);
            else Settings.NewMaskChannels = count;
        }

        private static void OnChannelClicked(MaskChannels channel, bool additive)
        {
            BrushSettings brush = Settings.Brush;
            MaskChannels next = additive ? brush.Channels ^ channel : channel;
            if (next == MaskChannels.None) return;
            brush.Channels = next;
            Settings.NotifyChanged();
        }

        // ---------- Mask rows ----------

        private VisualElement MaskRow(PainterSession session, MaskEntry entry, bool active)
        {
            var row = Row();
            row.style.paddingLeft = 4;
            row.style.minHeight = 20;
            row.style.borderTopLeftRadius = row.style.borderTopRightRadius = row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 3;
            if (active) row.style.backgroundColor = new Color(Accent.r, Accent.g, Accent.b, 0.45f);
            row.tooltip = $"{entry.Path}\nClick to paint · double-click to rename · right-click for options";

            var name = new Label(entry.Name) { style = { flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis } };
            if (active) name.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(name);

            row.Add(SmallLabel(MaskInfo.ChannelSetName(entry.Info.ChannelCount), $"{entry.Info.ChannelCount} channel(s)"));
            if (entry.Bindings.Count > 0)
            {
                MaskBinding binding = entry.Bindings[0];
                row.Add(SmallLabel("→ " + binding.DisplayName, $"Used by {binding.Material.name} as {binding.DisplayName}"));
            }

            // Actions show on the active or hovered row only, to keep the list calm.
            var actions = Row();
            var ping = new Button(() => EditorGUIUtility.PingObject(entry.Texture)) { tooltip = "Show in Project" };
            ping.style.width = 20;
            ping.style.paddingLeft = ping.style.paddingRight = 0;
            Texture pingIcon = EditorGUIUtility.IconContent("d_Search Icon").image;
            if (pingIcon != null) ping.Add(new Image { image = pingIcon, style = { width = 14, height = 14, alignSelf = Align.Center } });
            else ping.text = "◎";
            actions.Add(ping);
            var more = new Button { text = "⋯", tooltip = "Mask options" };
            more.clicked += () => ShowMaskMenu(more, session, entry);
            actions.Add(more);
            actions.style.visibility = active ? Visibility.Visible : Visibility.Hidden;
            row.Add(actions);
            if (!active)
            {
                row.RegisterCallback<PointerEnterEvent>(_ => actions.style.visibility = Visibility.Visible);
                row.RegisterCallback<PointerLeaveEvent>(_ => actions.style.visibility = Visibility.Hidden);
            }

            row.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.target is Button || (e.target as VisualElement)?.parent is Button) return;
                if (e.button == 0 && e.clickCount == 2) StartRename(session, entry);
                else if (e.button == 0) session.SelectMask(entry);
                else if (e.button == 1) ShowMaskMenu(more, session, entry);
                e.StopPropagation();
            });
            return row;
        }

        private void ShowMaskMenu(VisualElement anchor, PainterSession session, MaskEntry entry)
        {
            var menu = new GenericMenu();
            MaskInfo info = entry.Info;

            menu.AddItem(new GUIContent("Rename…"), false, () => StartRename(session, entry));
            for (int count = 1; count <= MaskInfo.MaxChannels; count++)
            {
                int c = count;
                menu.AddItem(new GUIContent($"Channels/{MaskInfo.ChannelSetName(c)}"), info.ChannelCount == c, () => session.SetChannelCount(entry, c));
            }
            for (int i = 0; i < info.ChannelCount; i++)
            {
                int index = i;
                menu.AddItem(new GUIContent($"Rename Channel/{"RGBA"[i]}  ({info.GetChannelName(i)})…"), false, () => RenameChannel(session, entry, index));
            }
            for (int uv = 0; uv < 4; uv++)
            {
                int u = uv;
                if (session.HasUV(u)) menu.AddItem(new GUIContent($"UV Set/UV{u}"), info.UVChannel == u, () => session.SetUVChannel(entry, u));
                else menu.AddDisabledItem(new GUIContent($"UV Set/UV{u} (not on this mesh)"), info.UVChannel == u);
            }
            AddUseInMaterialItems(menu, session, entry);
            menu.AddSeparator(string.Empty);

            if (entry.Is(session.ActiveMask) && session.Error == null)
            {
                string channels = ChannelList(info, Settings.Brush.Channels);
                menu.AddItem(new GUIContent($"Fill {channels} with {Settings.Brush.Value:0.##}"), false, () => session.FillAll(Settings.Brush.Channels, Settings.Brush.Value));
                menu.AddItem(new GUIContent($"Clear {channels}"), false, () => session.FillAll(Settings.Brush.Channels, 0f));
                menu.AddItem(new GUIContent($"Invert {channels}"), false, () => session.Invert(Settings.Brush.Channels));
                foreach (int resolution in new[] { 256, 512, 1024, 2048, 4096 })
                {
                    int r = resolution;
                    menu.AddItem(new GUIContent($"Resize/{r} × {r}"), session.Document?.Width == r, () => ConfirmResize(session, r));
                }
                menu.AddSeparator(string.Empty);
            }
            menu.AddItem(new GUIContent("Duplicate"), false, () => session.DuplicateMask(entry));
            menu.AddItem(new GUIContent("Show in Project"), false, () => EditorGUIUtility.PingObject(entry.Texture));
            menu.AddItem(new GUIContent("Delete…"), false, () => ConfirmDelete(session, entry));
            menu.DropDown(anchor.worldBound);
        }

        private static void AddUseInMaterialItems(GenericMenu menu, PainterSession session, MaskEntry entry)
        {
            const string prefix = "Use in Material/";
            List<MaskBinding> options = MaskBindings.GetOptions(session.Renderer);
            if (options.Count == 0) menu.AddDisabledItem(new GUIContent(prefix + "No texture properties"));
            foreach (MaskBinding option in options)
            {
                bool used = entry.Bindings.Contains(option);
                Texture2D current = option.Texture;
                string replaces = current != null && !used ? $"  (replaces {current.name})" : string.Empty;
                var label = new GUIContent($"{prefix}{option.Material.name}/{option.DisplayName}{replaces}");
                if (!MaskBindings.CanBind(option, out _)) menu.AddDisabledItem(new GUIContent(label.text + "  (read-only material)"));
                else if (used) menu.AddItem(label, true, () => session.RemoveFromMaterial(option));
                else menu.AddItem(label, false, () => session.UseInMaterial(entry, option));
            }
        }

        private void StartRename(PainterSession session, MaskEntry entry) =>
            prompt.Show("Mask name", entry.Name, name =>
            {
                if (name.Length == 0 || name == entry.Name) return;
                string error = session.RenameMask(entry, name);
                if (error != null) Debug.LogWarning($"[UV Mask Painter] {error}");
            });

        private void RenameChannel(PainterSession session, MaskEntry entry, int channel) =>
            prompt.Show($"Channel {"RGBA"[channel]} name", entry.Info.HasCustomName(channel) ? entry.Info.GetChannelName(channel) : string.Empty,
                name => session.SetChannelName(entry, channel, name));

        private static void ConfirmResize(PainterSession session, int size)
        {
            if (EditorUtility.DisplayDialog("Resize Mask", $"Resample {session.Document.DisplayName} to {size} × {size}?\nThis clears the mask's undo history.", "Resize", "Cancel"))
                session.Resize(size);
        }

        private static void ConfirmDelete(PainterSession session, MaskEntry entry)
        {
            string usage = entry.Bindings.Count > 0 ? "\nIt will also be removed from this object's material." : string.Empty;
            if (EditorUtility.DisplayDialog("Delete Mask", $"Move {entry.Name} to the trash?{usage}", "Delete", "Cancel"))
                session.DeleteMask(entry);
        }

        private static string ChannelList(MaskInfo info, MaskChannels channels)
        {
            var names = new List<string>();
            for (int i = 0; i < info.ChannelCount; i++)
                if ((channels & (MaskChannels)(1 << i)) != 0) names.Add(info.GetChannelName(i));
            return string.Join(" + ", names);
        }

        // ---------- Small builders ----------

        internal static VisualElement Row() => new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

        private static Label SmallLabel(string text, string tooltip) => new Label(text)
        {
            tooltip = tooltip,
            style = { fontSize = 10, opacity = 0.7f, marginLeft = 4, marginRight = 2, maxWidth = 90, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis }
        };

        private static VisualElement SliderRow(string label, string tooltip, Slider slider, Label valueText, Action<float> apply)
        {
            var row = Row();
            row.tooltip = tooltip;
            var name = new Label(label) { style = { width = 62 } };
            slider.style.flexGrow = 1;
            slider.RegisterValueChangedCallback(e =>
            {
                apply(e.newValue);
                Settings.NotifyChanged();
            });
            valueText.style.width = 52;
            valueText.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(name);
            row.Add(slider);
            row.Add(valueText);
            return row;
        }

        internal static Button Chip(string text, Action onClick)
        {
            var button = onClick != null ? new Button(onClick) { text = text } : new Button { text = text };
            button.style.minWidth = 26;
            button.style.marginLeft = 1;
            button.style.marginRight = 1;
            return button;
        }

        internal static void SetChipActive(Button chip, bool active, Color color)
        {
            chip.style.backgroundColor = active ? color : new StyleColor(StyleKeyword.Null);
            chip.style.color = active ? Color.white : new StyleColor(StyleKeyword.Null);
        }
    }
}
