using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Archura.UVMaskPainter.Editor.Document
{
    /// <summary>GPU working copy of one mask file. One document per texture asset, so shared masks never diverge.</summary>
    public sealed class MaskDocument : IDisposable
    {
        private static readonly Dictionary<string, MaskDocument> openByGuid = new Dictionary<string, MaskDocument>();
        private static readonly HashSet<MaskDocument> live = new HashSet<MaskDocument>();

        private string guid;   // null while the mask only exists in memory
        private int revision;  // incremented on every pixel change
        private bool isSaving;
        private bool saveQueued;
        private Action<string> queuedCallback;
        private Task backgroundWrite;
        private DateTime fileWriteTime;

        public RenderTexture Texture { get; }
        public int Width => Texture.width;
        public int Height => Texture.height;
        public bool IsHdr { get; }
        public bool IsDirty { get; private set; }
        public bool IsDisposed { get; private set; }

        // Resolved from the GUID, so it follows moved assets. Null while unsaved.
        public string AssetPath => guid == null ? null : AssetDatabase.GUIDToAssetPath(guid);
        public Texture2D SourceTexture => guid == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        public string DisplayName => guid == null ? "Unsaved Mask" : Path.GetFileName(AssetPath);

        public event Action<MaskDocument> Changed;

        static MaskDocument()
        {
            // Never lose paint: flush to disk before GPU resources are dropped.
            AssemblyReloadEvents.beforeAssemblyReload += SaveAndCloseAll;
            EditorApplication.quitting += SaveAndCloseAll;
        }

        private MaskDocument(int width, int height, bool hdr)
        {
            IsHdr = hdr;
            var descriptor = new RenderTextureDescriptor(width, height, hdr ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32, 0)
            {
                sRGB = false,
                useMipMap = false,
                msaaSamples = 1
            };
            Texture = new RenderTexture(descriptor)
            {
                name = "UV Mask Painter Document",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Texture.Create();
            live.Add(this);
        }

        public static MaskDocument Open(Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) throw new ArgumentException($"'{texture.name}' is not a project asset.", nameof(texture));

            string assetGuid = AssetDatabase.AssetPathToGUID(path);
            if (openByGuid.TryGetValue(assetGuid, out MaskDocument existing) && !existing.IsDisposed)
            {
                // Reload if the file was changed outside the painter and we have nothing unsaved.
                if (existing.IsDirty || existing.isSaving || existing.fileWriteTime == FileWriteTime(path))
                    return existing;
                existing.Dispose();
            }

            Texture2D pixels = TextureIO.Load(path);
            MaskDocument document;
            if (pixels != null)
            {
                document = new MaskDocument(pixels.width, pixels.height, TextureIO.IsHdr(path));
                Graphics.Blit(pixels, document.Texture);
                Object.DestroyImmediate(pixels);
            }
            else
            {
                Debug.LogWarning($"[UV Mask Painter] '{Path.GetFileName(path)}' can't be edited losslessly. " +
                                 "Painting starts from the imported texture and will be saved as a PNG copy.", texture);
                document = new MaskDocument(texture.width, texture.height, hdr: false);
                document.CopyFromImported(texture, TextureIO.IsImportedAsSrgb(path));
            }

            document.guid = assetGuid;
            document.fileWriteTime = FileWriteTime(path);
            openByGuid[assetGuid] = document;
            return document;
        }

        public static MaskDocument CreateUnsaved(int width, int height, Color fill)
        {
            var document = new MaskDocument(width, height, hdr: false);
            document.Fill(fill);
            return document;
        }

        /// <summary>The open document for this texture, or null.</summary>
        public static MaskDocument FindOpen(Texture2D texture)
        {
            string path = texture != null ? AssetDatabase.GetAssetPath(texture) : null;
            if (string.IsNullOrEmpty(path)) return null;
            return openByGuid.TryGetValue(AssetDatabase.AssetPathToGUID(path), out MaskDocument document) && !document.IsDisposed
                ? document
                : null;
        }

        public static void CloseAll()
        {
            foreach (MaskDocument document in new List<MaskDocument>(live))
                document.Dispose();
        }

        public static void SaveAndCloseAll()
        {
            foreach (MaskDocument document in new List<MaskDocument>(live))
            {
                try
                {
                    if (document.IsDirty && document.guid != null) document.Save();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UV Mask Painter] Could not save '{document.DisplayName}': {e.Message}");
                }
            }
            CloseAll();
        }

        /// <summary>Writes a resampled copy over the file and closes this document (reopen it to continue).</summary>
        public void ResizeFile(int width, int height)
        {
            ThrowIfDisposed();
            string path = ResolveSavePath();
            WaitForBackgroundSave();

            var resized = new RenderTexture(width, height, 0, Texture.format, RenderTextureReadWrite.Linear) { hideFlags = HideFlags.HideAndDontSave };
            resized.Create();
            Graphics.Blit(Texture, resized);
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(resized);
            request.WaitForCompletion();
            byte[] bytes = TextureIO.EncodeRaw(request.GetData<byte>().ToArray(), resized.graphicsFormat, width, height, IsHdr);
            GpuReadback.Unbind(resized);
            resized.Release();
            Object.DestroyImmediate(resized);

            TextureIO.WriteAtomic(TextureIO.ToFullPath(path), bytes, TextureIO.TempDirectory);
            Dispose();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Blocking save. Non-writable formats (TGA, PSD, JPG) are saved as a PNG copy.</summary>
        public string Save() => SaveAs(ResolveSavePath());

        public string SaveAs(string assetPath)
        {
            ThrowIfDisposed();
            WaitForBackgroundSave();

            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(Texture);
            request.WaitForCompletion();
            if (request.hasError) throw new IOException($"Could not read '{DisplayName}' back from the GPU.");

            int savedRevision = revision;
            byte[] bytes = TextureIO.EncodeRaw(request.GetData<byte>().ToArray(), Texture.graphicsFormat, Width, Height, IsHdr);
            TextureIO.WriteAtomic(TextureIO.ToFullPath(assetPath), bytes, TextureIO.TempDirectory);
            FinishSave(assetPath, savedRevision);
            return assetPath;
        }

        /// <summary>Non-blocking save (async readback + worker-thread encode). Overlapping calls coalesce.</summary>
        public void SaveInBackground(Action<string> completed = null)
        {
            ThrowIfDisposed();
            if (isSaving)
            {
                saveQueued = true;
                queuedCallback = completed ?? queuedCallback;
                return;
            }

            isSaving = true;
            string assetPath = ResolveSavePath();
            string fullPath = TextureIO.ToFullPath(assetPath);
            string tempDirectory = TextureIO.TempDirectory;
            GraphicsFormat format = Texture.graphicsFormat;
            int width = Width, height = Height, savedRevision = revision;
            bool hdr = IsHdr;

            // Polled from editor update so Unity APIs stay on the main thread.
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(Texture);
            Task write = null;
            void Poll()
            {
                if (write == null)
                {
                    request.Update();
                    if (!request.done) return;

                    if (request.hasError)
                    {
                        EditorApplication.update -= Poll;
                        Debug.LogError($"[UV Mask Painter] Could not read '{DisplayName}' back from the GPU; it was not saved.");
                        EndBackgroundSave();
                        return;
                    }

                    byte[] raw = request.GetData<byte>().ToArray();
                    write = backgroundWrite = Task.Run(() =>
                        TextureIO.WriteAtomic(fullPath, TextureIO.EncodeRaw(raw, format, width, height, hdr), tempDirectory));
                    return;
                }

                if (!write.IsCompleted) return;
                EditorApplication.update -= Poll;
                backgroundWrite = null;

                bool succeeded = !write.IsFaulted;
                if (succeeded) FinishSave(assetPath, savedRevision);
                else Debug.LogError($"[UV Mask Painter] Saving '{assetPath}' failed: {write.Exception?.GetBaseException().Message}");

                // Clear the saving flag first so the callback sees the finished state.
                EndBackgroundSave();
                if (succeeded) completed?.Invoke(assetPath);
            }
            EditorApplication.update += Poll;
        }

        public bool IsSaving => isSaving;

        public void Dispose()
        {
            if (IsDisposed) return;
            WaitForBackgroundSave();
            IsDisposed = true;
            if (guid != null && openByGuid.TryGetValue(guid, out MaskDocument open) && open == this)
                openByGuid.Remove(guid);
            live.Remove(this);
            if (Texture != null)
            {
                GpuReadback.Unbind(Texture);
                Texture.Release();
                Object.DestroyImmediate(Texture);
            }
        }

        internal void MarkDirty()
        {
            revision++;
            IsDirty = true;
            Changed?.Invoke(this);
        }

        private static DateTime FileWriteTime(string assetPath)
        {
            string full = TextureIO.ToFullPath(assetPath);
            return File.Exists(full) ? File.GetLastWriteTimeUtc(full) : default;
        }

        private string ResolveSavePath()
        {
            if (guid == null) throw new InvalidOperationException("This mask has no file yet. Use SaveAs.");
            string path = AssetPath;
            return TextureIO.CanWrite(path) ? path : AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, ".png"));
        }

        private void FinishSave(string assetPath, int savedRevision)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            if (IsDisposed) return;
            fileWriteTime = FileWriteTime(assetPath);

            if (assetPath != AssetPath)
            {
                TextureIO.ApplyMaskImportSettings(assetPath);
                if (guid != null && openByGuid.TryGetValue(guid, out MaskDocument open) && open == this) openByGuid.Remove(guid);
                guid = AssetDatabase.AssetPathToGUID(assetPath);
                openByGuid[guid] = this;
            }

            // Still dirty if the user kept painting while the file was being written.
            if (revision == savedRevision) IsDirty = false;
            Changed?.Invoke(this);
        }

        private void EndBackgroundSave()
        {
            isSaving = false;
            if (!saveQueued || IsDisposed) return;

            Action<string> callback = queuedCallback;
            saveQueued = false;
            queuedCallback = null;
            SaveInBackground(callback);
        }

        // Finish in-flight writes before a reload kills the worker thread.
        private void WaitForBackgroundSave()
        {
            try { backgroundWrite?.Wait(); }
            catch (AggregateException) { /* reported by the save's continuation */ }
        }

        internal Texture2D ReadPixels(RectInt region)
        {
            ThrowIfDisposed();
            return GpuReadback.Read(Texture, region, IsHdr);
        }

        private void Fill(Color value)
        {
            var swatch = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, linear: true) { hideFlags = HideFlags.HideAndDontSave };
            swatch.SetPixel(0, 0, value);
            swatch.Apply(false);
            Graphics.Blit(swatch, Texture);
            Object.DestroyImmediate(swatch);
        }

        private void CopyFromImported(Texture2D texture, bool importedAsSrgb)
        {
            if (!importedAsSrgb)
            {
                Graphics.Blit(texture, Texture);
                return;
            }

            // Keep stored values; don't let the sampler linearize them.
            var descriptor = Texture.descriptor;
            descriptor.sRGB = true;
            RenderTexture temp = RenderTexture.GetTemporary(descriptor);
            Graphics.Blit(texture, temp);
            Graphics.CopyTexture(temp, Texture);
            GpuReadback.Unbind(temp);
            RenderTexture.ReleaseTemporary(temp);
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(MaskDocument));
        }
    }
}
