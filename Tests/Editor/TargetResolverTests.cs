using Archura.UVMaskPainter.Editor.Targeting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Tests
{
    public class TargetResolverTests
    {
        private const string ShaderName = "Hidden/Archura/UVMaskPainter/Tests/MaskTarget";
        private MeshRenderer renderer;
        private Material material;

        [SetUp]
        public void SetUp()
        {
            TestUtility.CreateTempFolder();
            material = new Material(Shader.Find(ShaderName));
            AssetDatabase.CreateAsset(material, $"{TestUtility.TempFolder}/ResolverTest.mat");
            renderer = TestUtility.CreateQuad();
            renderer.sharedMaterial = material;
        }

        [TearDown]
        public void TearDown()
        {
            TargetResolver.Forget(material);
            Object.DestroyImmediate(renderer.gameObject);
            TestUtility.DeleteTempFolder();
        }

        [Test]
        public void ListsVisible2DTextureProperties()
        {
            CollectionAssert.AreEqual(new[] { "_BaseMap", "_SplatControl", "_Wear" }, TargetResolver.GetTextureProperties(material));
        }

        [Test]
        public void PrefersPropertiesTaggedArchuraMask()
        {
            PaintTarget target = TargetResolver.Resolve(renderer);
            Assert.AreEqual("_Wear", target.PropertyName);
            Assert.AreEqual(0, target.MaterialIndex);
        }

        [Test]
        public void RemembersTheLastChoicePerMaterial()
        {
            TargetResolver.Remember(new PaintTarget(renderer, 0, "_SplatControl", uvChannel: 1));
            PaintTarget target = TargetResolver.Resolve(renderer);

            Assert.AreEqual("_SplatControl", target.PropertyName);
            Assert.AreEqual(1, target.UVChannel);
        }

        [Test]
        public void ReturnsNullWhenNothingLooksLikeAMask()
        {
            renderer.sharedMaterial = new Material(Shader.Find("Unlit/Texture"));
            Assert.IsNull(TargetResolver.Resolve(renderer));
            Object.DestroyImmediate(renderer.sharedMaterial);
        }

        [TestCase("_MaskMap", true)]
        [TestCase("_SplatControl", true)]
        [TestCase("_BlendWeights", true)]
        [TestCase("_BaseMap", false)]
        [TestCase("_BumpMap", false)]
        public void NameHeuristic(string property, bool expected)
        {
            Assert.AreEqual(expected, TargetResolver.LooksLikeMask(property));
        }
    }
}
