using Archura.UVMaskPainter.Editor.Document;
using Archura.UVMaskPainter.Editor.Targeting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Tests
{
    public class MaskLibraryTests
    {
        private const string ShaderName = "Hidden/Archura/UVMaskPainter/Tests/MaskTarget";
        private MeshRenderer renderer;
        private Material material;

        [SetUp]
        public void SetUp()
        {
            TestUtility.CreateTempFolder();
            material = new Material(Shader.Find(ShaderName));
            AssetDatabase.CreateAsset(material, $"{TestUtility.TempFolder}/LibraryTest.mat");
            renderer = TestUtility.CreateQuad();
            renderer.sharedMaterial = material;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(renderer.gameObject);
            TestUtility.DeleteTempFolder();
        }

        [Test]
        public void ListsVisible2DTextureProperties()
        {
            CollectionAssert.AreEqual(new[] { "_BaseMap", "_SplatControl", "_Wear" }, MaskBindings.GetTextureProperties(material));
        }

        [Test]
        public void SuggestsOnlyEmptyArchuraMaskProperties()
        {
            Assert.AreEqual("_Wear", MaskBindings.SuggestFor(renderer).PropertyName);

            material.SetTexture("_Wear", Texture2D.blackTexture);
            Assert.IsNull(MaskBindings.SuggestFor(renderer));
        }

        [Test]
        public void CreatedMask_StoresMetadataAndImportsByChannelCount()
        {
            Texture2D mask = MaskFactory.Create(renderer, "Wetness", 2, 64);

            Assert.AreEqual($"{TestUtility.TempFolder}/Wetness.png", AssetDatabase.GetAssetPath(mask));
            Assert.IsTrue(MaskInfo.TryRead(mask, out MaskInfo info));
            Assert.AreEqual(2, info.ChannelCount);
            Assert.AreEqual(MaskChannels.R | MaskChannels.G, info.Channels);
            Assert.AreEqual(MaskLibrary.OwnerKey(renderer), info.Owner);
            CollectionAssert.Contains(AssetDatabase.GetLabels(mask), MaskInfo.Label);

            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mask));
            Assert.IsFalse(importer.sRGBTexture);
            Assert.AreEqual(TextureImporterAlphaSource.None, importer.alphaSource);

            Texture2D single = MaskFactory.Create(renderer, "Damage", 1, 64);
            var singleImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(single));
            Assert.AreEqual(TextureImporterType.SingleChannel, singleImporter.textureType);
        }

        [Test]
        public void FindsOwnedMasksAndMasksUsedByTheMaterials()
        {
            Texture2D owned = MaskFactory.Create(renderer, "Owned", 1, 64);
            MeshRenderer other = TestUtility.CreateQuad();
            other.sharedMaterial = material;
            Texture2D foreign = MaskFactory.Create(other, "Foreign", 1, 64);
            Object.DestroyImmediate(other.gameObject);

            var entries = MaskLibrary.FindFor(renderer);
            Assert.AreEqual(1, entries.Count, "A mask made for another object is not listed");
            Assert.AreSame(owned, entries[0].Texture);
            Assert.IsEmpty(entries[0].Bindings);

            MaskBindings.Bind(new MaskBinding(renderer, 0, "_SplatControl"), foreign);
            entries = MaskLibrary.FindFor(renderer);
            Assert.AreEqual(2, entries.Count, "A mask used by the material is listed");
            MaskEntry bound = entries.Find(e => e.Texture == foreign);
            Assert.AreEqual("_SplatControl", bound.Bindings[0].PropertyName);
        }

        [Test]
        public void IgnoresOrdinaryTextures()
        {
            string path = $"{TestUtility.TempFolder}/Specular.png";
            Texture2D plain = MaskFactory.CreateMaskFile(path, 8, 8, Color.white);
            material.SetTexture("_BaseMap", plain);

            Assert.IsEmpty(MaskLibrary.FindFor(renderer));
        }

        [Test]
        public void ChannelNamesDefaultToRGBA()
        {
            var info = new MaskInfo(3);
            info.SetChannelName(1, "Moss");
            Assert.AreEqual("R", info.GetChannelName(0));
            Assert.AreEqual("Moss", info.GetChannelName(1));
            Assert.IsTrue(info.HasCustomName(1));
            Assert.AreEqual("RGB", MaskInfo.ChannelSetName(3));
        }

        [Test]
        public void CanBind_RejectsReadOnlyMaterials()
        {
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            Assert.IsFalse(MaskBindings.CanBind(new MaskBinding(renderer, 0, "_MainTex"), out string reason));
            StringAssert.Contains("read-only", reason);
        }
    }
}
