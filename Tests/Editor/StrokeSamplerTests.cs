using System.Collections.Generic;
using Archura.UVMaskPainter.Editor.Painting;
using NUnit.Framework;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Tests
{
    public class StrokeSamplerTests
    {
        private readonly List<Dab> dabs = new List<Dab>();

        [SetUp]
        public void SetUp() => dabs.Clear();

        [Test]
        public void FirstPoint_ProducesDab_SoAClickPaints()
        {
            var sampler = new StrokeSampler(0.1f);
            sampler.AddPoint(Vector3.zero, Vector3.up, 1f, dabs);
            Assert.AreEqual(1, dabs.Count);
        }

        [Test]
        public void Dabs_AreEvenlySpaced_RegardlessOfMouseSpeed()
        {
            var sampler = new StrokeSampler(0.1f);
            sampler.AddPoint(Vector3.zero, Vector3.up, 1f, dabs);
            sampler.AddPoint(new Vector3(0.35f, 0, 0), Vector3.up, 1f, dabs);

            Assert.AreEqual(4, dabs.Count);
            for (int i = 0; i < dabs.Count; i++)
                Assert.AreEqual(i * 0.1f, dabs[i].Position.x, 1e-5f);
        }

        [Test]
        public void Distance_CarriesOverBetweenSmallMoves()
        {
            var sampler = new StrokeSampler(0.1f);
            sampler.AddPoint(Vector3.zero, Vector3.up, 1f, dabs);
            sampler.AddPoint(new Vector3(0.05f, 0, 0), Vector3.up, 1f, dabs);
            sampler.AddPoint(new Vector3(0.12f, 0, 0), Vector3.up, 1f, dabs);

            Assert.AreEqual(2, dabs.Count);
            Assert.AreEqual(0.1f, dabs[1].Position.x, 1e-5f);
        }

        [Test]
        public void Reset_StartsANewSegment()
        {
            var sampler = new StrokeSampler(0.1f);
            sampler.AddPoint(Vector3.zero, Vector3.up, 1f, dabs);
            sampler.Reset();
            sampler.AddPoint(new Vector3(5f, 0, 0), Vector3.up, 1f, dabs);

            Assert.AreEqual(2, dabs.Count);
            Assert.AreEqual(5f, dabs[1].Position.x, 1e-5f);
        }

        [Test]
        public void Pressure_IsInterpolatedAlongTheSegment()
        {
            var sampler = new StrokeSampler(0.5f);
            sampler.AddPoint(Vector3.zero, Vector3.up, 0f, dabs);
            sampler.AddPoint(new Vector3(1f, 0, 0), Vector3.up, 1f, dabs);

            Assert.AreEqual(0.5f, dabs[1].Pressure, 1e-5f);
        }
    }
}
