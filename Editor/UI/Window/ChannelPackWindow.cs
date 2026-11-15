using System.Collections.Generic;
using System.IO;
using System.Linq;
using Archura.UVMaskPainter.Editor.Document;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>Assets ▸ Archura ▸ Pack Channels: combine channels of several textures into one PNG.</summary>
    internal sealed class ChannelPackWindow : EditorWindow
    {
        private const string MenuPath = "Assets/Archura/Pack Channels…";
        private static readonly List<string> SourceChannels = new List<string> { "R", "G", "B", "A", "Black", "White" };

        private readonly ObjectField[] textures = new ObjectField[4];
        private readonly DropdownField[] channels = new DropdownField[4];
        private Texture2D[] pending;

        [MenuItem(MenuPath, priority = 2000)]
        private static void Open()
        {
            var window = GetWindow<ChannelPackWindow>(true, "Pack Channels");
            window.minSize = new Vector2(320, 170);
            window.pending = Selection.objects.OfType<Texture2D>().Take(4).ToArray();
            if (window.textures[0] != null) window.Prefill();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = 8;
            root.Add(new Label("Pick a source channel for each output channel.") { style = { opacity = 0.7f, marginBottom = 4, whiteSpace = WhiteSpace.Normal } });

            for (int i = 0; i < 4; i++)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                row.Add(new Label("RGBA".Substring(i, 1)) { style = { width = 16, unityFontStyleAndWeight = FontStyle.Bold } });
                textures[i] = new ObjectField { objectType = typeof(Texture2D), allowSceneObjects = false, style = { flexGrow = 1 } };
                channels[i] = new DropdownField(SourceChannels, i) { style = { width = 64 } };
                row.Add(textures[i]);
                row.Add(channels[i]);
                root.Add(row);
            }
            root.Add(new Button(Export) { text = "Export PNG…", style = { marginTop = 6, height = 24 } });
            Prefill();
        }

        // One selected texture fills all outputs from its own channels; several go one per output (R from the first…).
        private void Prefill()
        {
            Texture2D[] selected = pending;
            pending = null;
            if (selected == null || selected.Length == 0) return;
            for (int i = 0; i < 4; i++)
            {
                textures[i].value = selected.Length == 1 ? selected[0] : i < selected.Length ? selected[i] : null;
                channels[i].index = selected.Length == 1 ? i : i < selected.Length ? 0 : 4;
            }
        }

        private void Export()
        {
            var sources = new ChannelSource[4];
            Texture2D first = null;
            for (int i = 0; i < 4; i++)
            {
                var texture = textures[i].value as Texture2D;
                int channel = channels[i].index;
                sources[i] = channel >= 4 || texture == null
                    ? ChannelSource.Value(channel == 5 ? 1f : 0f)
                    : ChannelSource.From(texture, channel);
                if (first == null) first = texture;

                // Pack the latest paint, not what's on disk.
                MaskDocument open = MaskDocument.FindOpen(texture);
                if (open != null && open.IsDirty) open.Save();
            }

            string folder = first != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(first)) : "Assets";
            string defaultName = first != null ? first.name + "_Packed" : "Packed";
            string path = EditorUtility.SaveFilePanelInProject("Export Packed Texture", defaultName, "png", string.Empty, folder);
            if (string.IsNullOrEmpty(path)) return;

            EditorGUIUtility.PingObject(ChannelPacker.Pack(sources, path));
        }
    }
}
