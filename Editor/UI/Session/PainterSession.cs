using System;
using System.Collections.Generic;
using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Editor.Geometry;
using Archura.UVMaskPainter.Editor.History;
using Archura.UVMaskPainter.Editor.Painting;
using Archura.UVMaskPainter.Editor.Targeting;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Archura.UVMaskPainter.Editor.UI
{
    /// <summary>
    /// Paint-mode state for one renderer: its masks, the active mask's document and the live preview.
    /// The single place that decides defaults so the UI stays dumb.
    /// </summary>
    public sealed class PainterSession : IDisposable
    {
        public static PainterSession Active { get; private set; }
        /// <summary>Raised when a session starts/ends or the active session changes state.</summary>
        public static event Action StateChanged;

        private const string LastMaskKeyPrefix = "Archura.UVMaskPainter.LastMask.";

        public Renderer Renderer { get; }
        public MeshSurface Surface { get; private set; }
        public string Error { get; private set; }
        public IReadOnlyList<MaskEntry> Masks => masks;
        /// <summary>Null until the user picks or creates a mask (the first stroke creates one).</summary>
        public MaskEntry ActiveMask { get; private set; }
        public MaskDocument Document { get; private set; }

        /// <summary>The active mask shows through this renderer's material.</summary>
        public bool HasLivePreview => boundMaterialIndex >= 0;
        /// <summary>Material slot to paint, or -1 for the whole mesh when the mask isn't used by a material.</summary>
        public int Submesh => PreviewBinding != null && Surface != null ? PreviewBinding.GetSubmesh(Surface.Mesh) : -1;
        public MaskChannels AvailableChannels => ActiveMask?.Info.Channels ?? (MaskChannels)((1 << Settings.NewMaskChannels) - 1);
        public bool CanPaint => Error == null && Surface != null;

        private static MaskPainterSettings Settings => MaskPainterSettings.instance;
        private MaskBinding PreviewBinding => ActiveMask != null && ActiveMask.Bindings.Count > 0 ? ActiveMask.Bindings[0] : null;

        private readonly MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock previewBlock = new MaterialPropertyBlock();
        private List<MaskEntry> masks = new List<MaskEntry>();
        private int boundMaterialIndex = -1;
        // Warn once per editor session, not on every selection.
        private static readonly HashSet<string> warned = new HashSet<string>();

        public static void Begin(Renderer renderer)
        {
            End();
            if (renderer == null) return;
            Active = new PainterSession(renderer);
            StateChanged?.Invoke();
        }

        public static void End()
        {
            if (Active == null) return;
            Active.Dispose();
            Active = null;
            StateChanged?.Invoke();
        }

        private PainterSession(Renderer renderer)
        {
            Renderer = renderer;
            PaintHistory.MemoryBudgetBytes = Settings.UndoMemoryMB * 1024L * 1024L;
            MaskLibrary.Changed += OnLibraryChanged;
            masks = MaskLibrary.FindFor(renderer);
            SelectMask(PickInitialMask());
            if (Surface == null && Error == null) BuildSurface(0);
            FitBrushToObject();
        }

        // ---------- Masks ----------

        public void SelectMask(MaskEntry entry)
        {
            if (entry != null && entry.Is(ActiveMask) && Document != null) return;

            CloseDocument();
            ActiveMask = entry;
            int uvChannel = entry?.Info.UVChannel ?? 0;
            if (Surface == null || Surface.UVChannel != uvChannel) BuildSurface(uvChannel);

            if (entry != null)
            {
                Document = MaskDocument.Open(entry.Texture);
                Bind();
                EditorPrefs.SetString(LastMaskKey, entry.Guid);
            }
            WarnAboutActiveMask();
            ClampBrushChannels();
            RaiseChanged();
        }

        /// <summary>True if the mesh has this UV set (masks can only be painted in UV sets the mesh has).</summary>
        public bool HasUV(int uvChannel)
        {
            Mesh mesh = Renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                      : Renderer != null && Renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            return mesh != null && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0 + uvChannel);
        }

        /// <summary>Creates a mask for this object (or adopts <paramref name="existing"/>) and makes it active.</summary>
        public MaskEntry CreateMask(string name, int channelCount, int resolution = 0, Texture2D existing = null)
        {
            Settings.NewMaskChannels = channelCount;
            Texture2D texture = existing != null
                ? MaskFactory.Adopt(existing, Renderer, channelCount)
                : MaskFactory.Create(Renderer, name, channelCount, resolution);

            // Shader authors can reserve a slot for masks; fill it without asking.
            MaskBinding suggestion = existing == null ? MaskBindings.SuggestFor(Renderer) : null;
            if (suggestion != null) MaskBindings.Bind(suggestion, texture);

            RefreshMasks();
            SelectMask(Find(texture));
            Log(suggestion != null ? $"Created mask '{texture.name}' and used it as {suggestion.ToString()}." : $"Created mask '{texture.name}'.", texture);
            return ActiveMask;
        }

        public string RenameMask(MaskEntry entry, string newName)
        {
            string error = AssetDatabase.RenameAsset(entry.Path, MaskFactory.SanitizeFileName(newName, Renderer));
            RefreshMasks();
            return string.IsNullOrEmpty(error) ? null : error;
        }

        /// <summary>Stops using the mask in this object's materials and moves the file to the OS trash.</summary>
        public void DeleteMask(MaskEntry entry)
        {
            if (entry == null) return;
            if (entry.Is(ActiveMask))
            {
                CloseDocument(save: false);
                ActiveMask = null;
            }
            MaskDocument.FindOpen(entry.Texture)?.Dispose();
            foreach (MaskBinding binding in entry.Bindings) MaskBindings.Bind(binding, null);

            string name = entry.Name;
            AssetDatabase.MoveAssetToTrash(entry.Path);
            RefreshMasks();
            if (ActiveMask == null) SelectMask(PickInitialMask());
            Log($"Moved mask '{name}' to the trash.");
        }

        public void DuplicateMask(MaskEntry entry)
        {
            MaskDocument open = MaskDocument.FindOpen(entry.Texture);
            if (open != null && open.IsDirty) open.Save();
            string path = AssetDatabase.GenerateUniqueAssetPath(entry.Path);
            if (!AssetDatabase.CopyAsset(entry.Path, path)) return;
            MaskLibrary.Invalidate();
            RefreshMasks();
            SelectMask(Find(AssetDatabase.LoadAssetAtPath<Texture2D>(path)));
        }

        public void SetChannelCount(MaskEntry entry, int channelCount) => EditInfo(entry, info => info.ChannelCount = channelCount);

        public void SetChannelName(MaskEntry entry, int channel, string name) => EditInfo(entry, info => info.SetChannelName(channel, name));

        public void SetUVChannel(MaskEntry entry, int uvChannel)
        {
            EditInfo(entry, info => info.UVChannel = uvChannel);
            if (entry.Is(ActiveMask) && Surface?.UVChannel != uvChannel)
            {
                BuildSurface(uvChannel);
                RaiseChanged();
            }
        }

        public void UseInMaterial(MaskEntry entry, MaskBinding binding)
        {
            MaskBindings.Bind(binding, entry.Texture);
            RefreshMasks();
            Log($"'{entry.Name}' is now used as {binding}.", entry.Texture);
        }

        public void RemoveFromMaterial(MaskBinding binding)
        {
            MaskBindings.Bind(binding, null);
            RefreshMasks();
        }

        // ---------- Painting ----------

        /// <summary>Starts a stroke, creating a mask on the very first one.</summary>
        public MaskStroke BeginStroke(BrushSettings brush)
        {
            if (!CanPaint || !EnsureDocument()) return null;
            return new MaskStroke(Document, Surface, Submesh, brush);
        }

        public void EndStroke(MaskStroke stroke)
        {
            if (stroke == null) return;
            stroke.End();
            AutoSave();
            RaiseChanged();
        }

        public void FillIsland(in SurfaceHit hit, MaskChannels channels, float value)
        {
            if (!CanPaint || !EnsureDocument()) return;
            MaskFill.FillIsland(Document, Surface, hit, channels, value);
            AutoSave();
            RaiseChanged();
        }

        public void FillAll(MaskChannels channels, float value)
        {
            if (!EnsureDocument()) return;
            MaskFill.FillAll(Document, channels, value);
            AutoSave();
            RaiseChanged();
        }

        public void Invert(MaskChannels channels)
        {
            if (!EnsureDocument()) return;
            MaskFill.Invert(Document, channels);
            AutoSave();
            RaiseChanged();
        }

        public void Resize(int size)
        {
            if (Document == null) return;
            Texture2D texture = Document.SourceTexture;
            Unbind();
            Document.ResizeFile(size, size);
            Document = MaskDocument.Open(texture);
            Bind();
            Log($"Resized '{texture.name}' to {size} × {size}.", texture);
            RaiseChanged();
        }

        public void Dispose()
        {
            MaskLibrary.Changed -= OnLibraryChanged;
            CloseDocument();
            Surface?.Dispose();
            Surface = null;
        }

        // ---------- Internals ----------

        private string LastMaskKey => LastMaskKeyPrefix + MaskLibrary.OwnerKey(Renderer);

        // Last mask used on this object, else one its material uses, else the first.
        private MaskEntry PickInitialMask()
        {
            string lastGuid = EditorPrefs.GetString(LastMaskKey, string.Empty);
            MaskEntry last = masks.Find(m => m.Guid == lastGuid);
            if (last != null) return last;
            MaskEntry bound = masks.Find(m => m.Bindings.Count > 0);
            return bound ?? (masks.Count > 0 ? masks[0] : null);
        }

        private MaskEntry Find(Texture2D texture)
        {
            string path = texture != null ? AssetDatabase.GetAssetPath(texture) : null;
            return Find(string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path));
        }

        private MaskEntry Find(string guid) => string.IsNullOrEmpty(guid) ? null : masks.Find(m => m.Guid == guid);

        private void OnLibraryChanged()
        {
            if (Active != this || Renderer == null) return;
            RefreshMasks();
        }

        // Re-reads the list; keeps the active mask (its bindings may have changed) or drops it if deleted.
        private void RefreshMasks()
        {
            masks = MaskLibrary.FindFor(Renderer);
            if (ActiveMask != null)
            {
                MaskEntry current = Find(ActiveMask.Guid);
                Unbind();
                if (current == null)
                {
                    CloseDocument(save: false);
                    ActiveMask = null;
                }
                else
                {
                    ActiveMask = current;
                    Bind();
                }
            }
            ClampBrushChannels();
            RaiseChanged();
        }

        private void EditInfo(MaskEntry entry, Action<MaskInfo> edit)
        {
            MaskInfo info = entry.Info.Clone();
            edit(info);
            info.Write(entry.Path);
            RefreshMasks();
        }

        private void BuildSurface(int uvChannel)
        {
            Surface?.Dispose();
            Error = MeshSurface.TryCreate(Renderer, uvChannel, out MeshSurface surface, out string error) ? null : error;
            Surface = surface;
        }

        private bool EnsureDocument()
        {
            if (Document != null) return true;
            CreateMask(MaskFactory.DefaultName(Renderer), Settings.NewMaskChannels);
            return Document != null;
        }

        private void CloseDocument(bool save = true)
        {
            Unbind();
            if (Document == null) return;

            if (save && Document.IsDirty) Document.SaveInBackground(); // stays open so Ctrl+Z keeps working
            else if (!save) Document.Dispose();
            Document = null;
        }

        private void AutoSave()
        {
            if (Settings.AutoSave && Document?.AssetPath != null) Document.SaveInBackground(_ => RaiseChanged());
        }

        // Live preview: show the working texture on this renderer without touching the material asset.
        private void Bind()
        {
            MaskBinding binding = PreviewBinding;
            if (binding == null || Document == null || Renderer == null) return;
            boundMaterialIndex = binding.MaterialIndex;
            Renderer.GetPropertyBlock(originalBlock, boundMaterialIndex);
            Renderer.GetPropertyBlock(previewBlock, boundMaterialIndex);
            previewBlock.SetTexture(binding.PropertyName, Document.Texture);
            Renderer.SetPropertyBlock(previewBlock, boundMaterialIndex);
        }

        private void Unbind()
        {
            if (boundMaterialIndex < 0) return;
            if (Renderer != null) Renderer.SetPropertyBlock(originalBlock.isEmpty ? null : originalBlock, boundMaterialIndex);
            boundMaterialIndex = -1;
        }

        // A one-channel mask can't be painted in G; keep the brush on channels the mask has.
        private void ClampBrushChannels()
        {
            BrushSettings brush = Settings.Brush;
            MaskChannels clamped = brush.Channels & AvailableChannels;
            if (clamped == brush.Channels) return;
            brush.Channels = clamped != MaskChannels.None ? clamped : MaskChannels.R;
            Settings.NotifyChanged();
        }

        // Problems that don't block painting go to the Console, once.
        private void WarnAboutActiveMask()
        {
            string path = Document?.AssetPath;
            if (path != null && TextureIO.IsImportedAsSrgb(path) && warned.Add("srgb:" + path))
                Debug.LogWarning($"[UV Mask Painter] '{ActiveMask.Name}' is imported as sRGB, so shaders read gamma-corrected values. " +
                                 "Untick sRGB in its import settings.", ActiveMask.Texture);

            Material material = PreviewBinding?.Material;
            if (material == null || !warned.Add("shared:" + material.GetInstanceID())) return;
            int users = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (Array.IndexOf(r.sharedMaterials, material) >= 0) users++;
            if (users > 1)
                Log($"Material '{material.name}' is shared by {users} objects; they all show '{ActiveMask.Name}'.", material);
        }

        // Default brush is meaningless on very small or very large objects; start at ~5% of the object.
        private void FitBrushToObject()
        {
            float size = Renderer.bounds.size.magnitude;
            if (size <= 0f) return;
            BrushSettings brush = Settings.Brush;
            if (brush.Radius > size * 0.5f || brush.Radius < size * 0.005f)
            {
                brush.Radius = size * 0.05f;
                Settings.NotifyChanged();
            }
        }

        private static void Log(string message, Object context = null) => Debug.Log($"[UV Mask Painter] {message}", context);

        private void RaiseChanged()
        {
            if (Active == this) StateChanged?.Invoke();
        }
    }
}
