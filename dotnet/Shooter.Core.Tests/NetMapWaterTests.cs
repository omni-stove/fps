using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMapWaterTests
    {
        [Test]
        public void WrittenWaterReadsBackInTheSameOrder()
        {
            var water = new NetMapWater(new[]
            {
                new MapWater(new WaterBox(new Vector3(-2f, 0f, -3f), new Vector3(2f, 4f, 3f), 3.5f)),
                new MapWater(new WaterBox(new Vector3(10f, -1.25f, 0f), new Vector3(12f, 1f, 6f), 0.75f)),
            });

            var text = NetMapWater.Write(water);
            var read = NetMapWater.Parse(text, "test");

            Assert.That(read.Water, Has.Count.EqualTo(2));
            Assert.That(read.Water[0].Box, Is.EqualTo(water.Water[0].Box));
            Assert.That(read.Water[1].Box.Min, Is.EqualTo(new Vector3(10f, -1.25f, 0f)));
            Assert.That(read.Water[1].Box.Max, Is.EqualTo(new Vector3(12f, 1f, 6f)));
            Assert.That(read.Water[1].Box.SurfaceY, Is.EqualTo(0.75f));
            Assert.That(NetMapWater.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void AnEmptyFileHasNoWater()
        {
            var read = NetMapWater.Parse("{ \"water\": [] }", "test");

            Assert.That(read.Water, Is.Empty);
            Assert.That(read.SurfaceAt(Vector3.Zero), Is.Null);
            Assert.That(NetMapWater.None.SurfaceAt(Vector3.Zero), Is.Null);
        }

        [Test]
        public void TheSurfaceIsFoundOnlyInsideABox()
        {
            var water = new NetMapWater(new[] { new MapWater(new WaterBox(Vector3.Zero, new Vector3(4f, 3f, 4f), 2f)) });

            Assert.That(water.SurfaceAt(new Vector3(1f, 0.5f, 1f)), Is.EqualTo(2f));
            Assert.That(water.SurfaceAt(new Vector3(1f, 2.5f, 1f)), Is.EqualTo(2f), "above the surface but inside the box");
            Assert.That(water.SurfaceAt(new Vector3(5f, 0.5f, 1f)), Is.Null);
            Assert.That(water.SurfaceAt(new Vector3(1f, 3.5f, 1f)), Is.Null);
        }

        [Test]
        public void OverlappingWaterGivesTheHigherSurfaceWhicheverComesFirst()
        {
            var deep = new MapWater(new WaterBox(Vector3.Zero, new Vector3(10f, 6f, 10f), 5f));
            var shallow = new MapWater(new WaterBox(new Vector3(2f, 0f, 2f), new Vector3(4f, 2f, 4f), 1f));
            var inside = new Vector3(3f, 0.5f, 3f);

            Assert.That(new NetMapWater(new[] { deep, shallow }).SurfaceAt(inside), Is.EqualTo(5f));
            Assert.That(new NetMapWater(new[] { shallow, deep }).SurfaceAt(inside), Is.EqualTo(5f));
        }

        [Test]
        public void TheListIsCopied()
        {
            var list = new MapWater[] { new MapWater(new WaterBox(Vector3.Zero, Vector3.One, 0.5f)) };
            var water = new NetMapWater(list);

            list[0] = null;

            Assert.That(water.Water[0], Is.Not.Null);
            Assert.Throws<ArgumentNullException>(() => new NetMapWater(null));
            Assert.Throws<ArgumentException>(() => new NetMapWater(new MapWater[] { null }));
            Assert.Throws<ArgumentNullException>(() => NetMapWater.Write(null));
        }

        [TestCase("{ \"water\": [ { \"min\": [0, 0, 0], \"max\": [1, 1, 1], \"surfaceY\": 2 } ] }", "water[0].surfaceY")]
        [TestCase("{ \"water\": [ { \"min\": [2, 0, 0], \"max\": [1, 1, 1], \"surfaceY\": 0.5 } ] }", "water[0].min")]
        [TestCase("{ \"water\": [ { \"min\": [0, 0, 0], \"max\": [1, 1, 1] } ] }", "water[0].surfaceY")]
        [TestCase("{ \"water\": [ { \"min\": [0, 0, 0], \"surfaceY\": 0.5 } ] }", "water[0].max")]
        [TestCase("{ \"water\": [ { \"min\": [0, 0, 0], \"max\": [1, 1, 1], \"surfaceY\": 0.5, \"flow\": 1 } ] }", "flow")]
        [TestCase("{ \"pools\": [] }", "pools")]
        public void BadWaterNamesTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMapWater.Parse(text, "map.water.json"));
            Assert.That(e.Message, Does.Contain(item));
        }
    }
}
