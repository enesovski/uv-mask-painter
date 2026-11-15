using System;
using System.Collections.Generic;
using System.IO;
using Archura.UVMaskPainter.Editor.Targeting;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Document
{
    /// <summary>A mask texture as seen from one renderer: its metadata and where that renderer's materials use it.</summary>
    public sealed class MaskEntry
    {
        public Texture2D Texture { get; }
        public MaskInfo Info { get; }
        public List<MaskBinding> Bindings { get; } = new List<MaskBinding>();

        /// <summary>Stable identity; the texture object itself is replaced when its import settings change.</summary>
        public string Guid { get; }
        public string Path => AssetDatabase.GUIDToAssetPath(Guid);
        public string Name => Texture != null ? Texture.name : "<missing>";

        public MaskEntry(Texture2D texture, MaskInfo info)
        {
            Texture = texture;
            Info = info;
            Guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
        }

        public bool Is(MaskEntry other) => other != null && other.Guid == Guid;
    }

    /// <summary>Finds masks (textures labelled <see cref="MaskInfo.Label"/>). Cached until assets change.</summary>
    public static class MaskLibrary
    {
        /// <summary>Raised after mask or material assets change on disk.</summary>
        public static event Action Changed;

        private static Dictionary<string, MaskInfo> cache;

        public static void Invalidate() => cache = null;

        /// <summary>
        /// Identifies what a mask was painted for. Mesh assets share masks (same UVs); built-in and
        /// procedural meshes fall back to the scene object.
        /// </summary>
        public static string OwnerKey(Renderer renderer)
        {
            if (renderer == null) return string.Empty;
            Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                      : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;

            string meshPath = mesh != null ? AssetDatabase.GetAssetPath(mesh) : null;
            if (!string.IsNullOrEmpty(meshPath) && (meshPath.StartsWith("Assets/") || meshPath.StartsWith("Packages/")) &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId))
                return $"mesh:{guid}:{localId}";

            // Objects in a never-saved scene have no file id yet; the instance id lasts for this session.
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(renderer);
            return id.targetObjectId != 0 ? "object:" + id : "instance:" + renderer.GetInstanceID();
        }

        /// <summary>Masks used by the renderer's materials, then masks made for its mesh. Sorted by name.</summary>
        public static List<MaskEntry> FindFor(Renderer renderer)
        {
            var entries = new List<MaskEntry>();
            if (renderer == null) return entries;
            var byPath = new Dictionary<string, MaskEntry>();
            Dictionary<string, MaskInfo> all = All();

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;
                foreach (string property in MaskBindings.GetTextureProperties(materials[i]))
                {
                    var texture = materials[i].GetTexture(property) as Texture2D;
                    if (texture == null) continue;
                    string path = AssetDatabase.GetAssetPath(texture);
                    if (!all.TryGetValue(path, out MaskInfo info)) continue;
                    if (!byPath.TryGetValue(path, out MaskEntry entry))
                        byPath[path] = entry = new MaskEntry(texture, info);
                    entry.Bindings.Add(new MaskBinding(renderer, i, property));
                }
            }

            string owner = OwnerKey(renderer);
            foreach (KeyValuePair<string, MaskInfo> pair in all)
            {
                if (pair.Value.Owner != owner || byPath.ContainsKey(pair.Key)) continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pair.Key);
                if (texture != null) byPath[pair.Key] = new MaskEntry(texture, pair.Value);
            }

            entries.AddRange(byPath.Values);
            entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return entries;
        }

        private static Dictionary<string, MaskInfo> All()
        {
            if (cache != null) return cache;
            cache = new Dictionary<string, MaskInfo>();
            foreach (string guid in AssetDatabase.FindAssets($"l:{MaskInfo.Label}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (MaskInfo.TryRead(path, out MaskInfo info)) cache[path] = info;
            }
            return cache;
        }

        private sealed class Watcher : AssetPostprocessor
        {
            private static readonly HashSet<string> RelevantExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".exr", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".mat"
            };

            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (!Touches(imported) && !Touches(deleted) && !Touches(moved)) return;
                Invalidate();
                Changed?.Invoke();
            }

            private static bool Touches(string[] paths)
            {
                foreach (string path in paths)
                    if (RelevantExtensions.Contains(Path.GetExtension(path))) return true;
                return false;
            }
        }
    }
}
