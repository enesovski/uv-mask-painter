using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Editor.Legacy;
using Archura.UVMaskPainter.Editor.Painting;
using Archura.UVMaskPainter.Editor.Targeting;
using Archura.UVMaskPainter.Legacy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Tests
{
    public class DocumentTests
    {
        private const float Tolerance = 0.01f;

        [SetUp]
        public void SetUp() => TestUtility.CreateTempFolder();

        [TearDown]
        public void TearDown()
        {
            MaskDocument.CloseAll();
            TestUtility.DeleteTempFolder();
        }

        [Test]
        public void CreateMaskFile_WritesALinearPng()
        {
            string path = $"{TestUtility.TempFolder}/Created.png";
            Texture2D texture = MaskFactory.CreateMaskFile(path, 32, 16, new Color(0.25f, 0f, 0f, 1f));

            Assert.NotNull(texture);
            Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(path)).sRGBTexture);
            Texture2D pixels = TextureIO.Load(path);
            Assert.AreEqual(32, pixels.width);
            Assert.AreEqual(0.25f, pixels.GetPixel(3, 3).r, Tolerance);
            Object.DestroyImmediate(pixels);
        }

        [Test]
        public void Open_SharesOneDocumentPerTexture()
        {
            Texture2D texture = MaskFactory.CreateMaskFile($"{TestUtility.TempFolder}/Shared.png", 8, 8, Color.clear);

            MaskDocument first = MaskDocument.Open(texture);
            Assert.AreSame(first, MaskDocument.Open(texture));

            first.Dispose();
            Assert.AreNotSame(first, MaskDocument.Open(texture));
        }

        [Test]
        public void Open_ReadsTheSourceFile_NotTheCompressedImport()
        {
            string path = $"{TestUtility.TempFolder}/Compressed.png";
            MaskFactory.CreateMaskFile(path, 64, 64, new Color(0.5f, 0.25f, 0.75f, 1f));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = 32;
            importer.textureCompression = TextureImporterCompression.CompressedLQ;
            importer.SaveAndReimport();

            MaskDocument document = MaskDocument.Open(AssetDatabase.LoadAssetAtPath<Texture2D>(path));

            Assert.AreEqual(64, document.Width, "Full source resolution");
            Color texel = TestUtility.Texel(document, 0.5f, 0.5f);
            Assert.AreEqual(0.5f, texel.r, Tolerance);
            Assert.AreEqual(0.25f, texel.g, Tolerance);
            Assert.AreEqual(0.75f, texel.b, Tolerance);
        }

        [Test]
        public void Save_WritesPaintedPixelsBackToTheFile()
        {
            string path = $"{TestUtility.TempFolder}/RoundTrip.png";
            MaskDocument document = MaskDocument.Open(MaskFactory.CreateMaskFile(path, 16, 16, Color.clear));

            MaskFill.FillAll(document, MaskChannels.R, 0.5f);
            Assert.IsTrue(document.IsDirty);
            Assert.AreEqual(path, document.Save());
            Assert.IsFalse(document.IsDirty);

            Texture2D pixels = TextureIO.Load(path);
            Assert.AreEqual(0.5f, pixels.GetPixel(8, 8).r, Tolerance);
            Object.DestroyImmediate(pixels);
        }

        [Test]
        public void Open_KeepsTheImageOrientation()
        {
            string path = $"{TestUtility.TempFolder}/TopHalf.png";
            var pixels = new Texture2D(16, 16, TextureFormat.RGBA32, false, true);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    pixels.SetPixel(x, y, y >= 8 ? Color.red : Color.black);
            TextureIO.Write(path, pixels);
            Object.DestroyImmediate(pixels);

            MaskDocument document = MaskDocument.Open(AssetDatabase.LoadAssetAtPath<Texture2D>(path));

            Assert.AreEqual(1f, TestUtility.Texel(document, 0.5f, 0.75f).r, Tolerance, "Top half");
            Assert.AreEqual(0f, TestUtility.Texel(document, 0.5f, 0.25f).r, Tolerance, "Bottom half");
        }

        [Test]
        public void Save_KeepsStrokesWhereTheyWerePainted()
        {
            string path = $"{TestUtility.TempFolder}/Stroke.png";
            MaskDocument document = MaskDocument.Open(MaskFactory.CreateMaskFile(path, 32, 32, Color.clear));
            MeshRenderer quad = TestUtility.CreateQuad();
            Geometry.MeshSurface.TryCreate(quad, 0, out Geometry.MeshSurface surface, out _);
            surface.Raycast(new Ray(new Vector3(-0.25f, 0.25f, -1f), Vector3.forward), out Geometry.SurfaceHit hit); // uv (0.25, 0.75)

            using (var stroke = new MaskStroke(document, surface, 0, new BrushSettings { Radius = 0.1f, Hardness = 1f }))
                stroke.AddPoint(hit);
            document.Save();

            Texture2D saved = TextureIO.Load(path);
            Assert.Greater(saved.GetPixel(8, 24).r, 0.9f, "Painted texel");
            Assert.Less(saved.GetPixel(8, 8).r, 0.01f, "Mirrored texel must stay empty");
            Object.DestroyImmediate(saved);
            Object.DestroyImmediate(quad.gameObject);
        }

        [Test]
        public void SaveAs_GivesAnUnsavedMaskAFile()
        {
            MaskDocument document = MaskDocument.CreateUnsaved(8, 8, new Color(0f, 1f, 0f, 0f));
            string path = $"{TestUtility.TempFolder}/Unsaved.png";

            document.SaveAs(path);

            Assert.AreEqual(path, document.AssetPath);
            Assert.AreSame(document, MaskDocument.Open(document.SourceTexture));
            document.Dispose();
        }

        [Test]
        public void TryCreateFor_CreatesAndAssignsAMaskNextToTheMaterial()
        {
            var material = new Material(Shader.Find("Hidden/Archura/UVMaskPainter/Tests/MaskTarget"));
            AssetDatabase.CreateAsset(material, $"{TestUtility.TempFolder}/Rock.mat");
            MeshRenderer renderer = TestUtility.CreateQuad();
            renderer.sharedMaterial = material;
            var target = new PaintTarget(renderer, 0, "_Wear");

            Assert.IsTrue(MaskFactory.TryCreateFor(target, out Texture2D texture, out string error), error);

            Assert.AreEqual($"{TestUtility.TempFolder}/Rock_Wear.png", AssetDatabase.GetAssetPath(texture));
            Assert.AreSame(texture, material.GetTexture("_Wear"));
            Assert.AreEqual(MaskFactory.DefaultResolution, texture.width);

            TargetResolver.Forget(material);
            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void CanAssign_RejectsReadOnlyMaterials()
        {
            MeshRenderer renderer = TestUtility.CreateQuad();
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            Assert.IsFalse(MaskFactory.CanAssign(new PaintTarget(renderer, 0, "_MainTex"), out string reason));
            StringAssert.Contains("read-only", reason);
            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void LegacyMaskAsset_ConvertsToPng()
        {
            var asset = ScriptableObject.CreateInstance<MaskAsset>();
            AssetDatabase.CreateAsset(asset, $"{TestUtility.TempFolder}/Legacy.asset");
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = "Legacy_MaskTex" };
            var colors = new Color32[16];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(200, 0, 0, 255);
            texture.SetPixels32(colors);
            texture.Apply();
            AssetDatabase.AddObjectToAsset(texture, asset);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("maskTexture").objectReferenceValue = texture;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            string path = LegacyMaskConverter.Convert(asset);

            Texture2D pixels = TextureIO.Load(path);
            Assert.AreEqual(200f / 255f, pixels.GetPixel(1, 1).r, Tolerance);
            Object.DestroyImmediate(pixels);
        }
    }
}
