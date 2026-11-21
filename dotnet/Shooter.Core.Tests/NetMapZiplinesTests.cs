using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMapZiplinesTests
    {
        [Test]
        public void WrittenZiplinesReadBackInTheSameOrder()
        {
            var ziplines = new NetMapZiplines(new[]
            {
                new MapZipline(new Vector3(0f, 6f, 0f), new Vector3(0f, 2f, 20f), ZiplineSpec.Default),
                new MapZipline(new Vector3(-4f, 3.5f, 1f), new Vector3(8f, 3.5f, 1f), new ZiplineSpec(5f, 12f, 0f, 0.25f)),
            });

            var text = NetMapZiplines.Write(ziplines);
            var read = NetMapZiplines.Parse(text, "test");

            Assert.That(read.Ziplines, Has.Count.EqualTo(2));
            Assert.That(read.Ziplines[0].Start, Is.EqualTo(new Vector3(0f, 6f, 0f)));
            Assert.That(read.Ziplines[0].End, Is.EqualTo(new Vector3(0f, 2f, 20f)));
            Assert.That(read.Ziplines[1].Start, Is.EqualTo(new Vector3(-4f, 3.5f, 1f)));
            Assert.That(read.Ziplines[1].Spec.MinSpeed, Is.EqualTo(5f));
            Assert.That(read.Ziplines[1].Spec.MaxSpeed, Is.EqualTo(12f));
            Assert.That(read.Ziplines[1].Spec.Gravity, Is.EqualTo(0f));
            Assert.That(read.Ziplines[1].Spec.EndMargin, Is.EqualTo(0.25f));
            Assert.That(NetMapZiplines.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void AnEmptyListIsWrittenAndRead()
        {
            var text = NetMapZiplines.Write(NetMapZiplines.None);

            Assert.That(NetMapZiplines.Parse(text, "test").Ziplines, Is.Empty);
        }

        [Test]
        public void MissingTuningUsesTheZiplineDefaults()
        {
            var read = NetMapZiplines.Parse("{ \"ziplines\": [ { \"start\": [0, 6, 0], \"end\": [0, 2, 20] } ] }", "test");

            var spec = read.Ziplines[0].Spec;
            Assert.That(spec.MinSpeed, Is.EqualTo(ZiplineSpec.DefaultMinSpeed));
            Assert.That(spec.MaxSpeed, Is.EqualTo(ZiplineSpec.DefaultMaxSpeed));
            Assert.That(spec.Gravity, Is.EqualTo(ZiplineSpec.DefaultGravity));
            Assert.That(spec.EndMargin, Is.EqualTo(ZiplineSpec.DefaultEndMargin));
        }

        // 長さ 1 m は、両端の余白 (0.5 m ずつ) と最短の滑り (0.05 m) を足した 1.05 m に届かない
        [TestCase("{ \"ziplines\": [ { \"start\": [0, 6, 0], \"end\": [0, 6, 1] } ] }", "ziplines[0].end")]
        [TestCase("{ \"ziplines\": [ { \"start\": [0, 6, 0] } ] }", "ziplines[0].end")]
        [TestCase("{ \"ziplines\": [ { \"start\": [0, 6], \"end\": [0, 2, 20] } ] }", "ziplines[0].start")]
        [TestCase("{ \"ziplines\": [ { \"start\": [0, 6, 0], \"end\": [0, 2, 20], \"minSpeed\": 0 } ] }", "ziplines[0].minSpeed")]
        [TestCase("{ \"ziplines\": [ { \"start\": [0, 6, 0], \"end\": [0, 2, 20], \"maxSpeed\": 1 } ] }", "ziplines[0].maxSpeed")]
        [TestCase("{ \"ziplines\": [ { \"start\": [0, 6, 0], \"end\": [0, 2, 20], \"gravity\": -1 } ] }", "ziplines[0].gravity")]
        [TestCase("{ \"ziplines\": [ { \"start\": [0, 6, 0], \"end\": [0, 2, 20], \"endMargin\": -1 } ] }", "ziplines[0].endMargin")]
        [TestCase("{ \"ziplines\": [ { \"start\": [0, 6, 0], \"end\": [0, 2, 20], \"name\": \"a\" } ] }", "name")]
        public void BadZiplinesNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMapZiplines.Parse(text, "map.ziplines.json"));
            Assert.That(e.Message, Does.Contain(item));
        }

        [Test]
        public void AShortLineIsRefusedWhenBuilt()
        {
            Assert.Throws<ArgumentException>(() => new MapZipline(Vector3.Zero, new Vector3(0f, 0f, 1f), ZiplineSpec.Default));
            Assert.Throws<ArgumentNullException>(() => new MapZipline(Vector3.Zero, new Vector3(0f, 0f, 10f), null));
            Assert.Throws<ArgumentException>(() => new NetMapZiplines(new MapZipline[] { null }));
        }

        [Test]
        public void FindNearestPicksTheClosestLineInReach()
        {
            var ziplines = new NetMapZiplines(new[]
            {
                new MapZipline(new Vector3(0f, 4f, 0f), new Vector3(0f, 4f, 20f), ZiplineSpec.Default),
                new MapZipline(new Vector3(1f, 3f, 0f), new Vector3(1f, 3f, 20f), ZiplineSpec.Default),
            });

            Assert.That(ziplines.FindNearest(new Vector3(1f, 2.5f, 5f), 1.5f), Is.EqualTo(1));
            Assert.That(ziplines.FindNearest(new Vector3(0f, 3.5f, 5f), 1.5f), Is.EqualTo(0));
            Assert.That(ziplines.FindNearest(new Vector3(0f, 0f, 5f), 1.5f), Is.EqualTo(-1), "both lines are out of reach");
            Assert.That(ziplines.FindNearest(new Vector3(0f, 4f, -1f), 0.9f), Is.EqualTo(-1), "the distance is to the end, not the endless line");
            Assert.That(NetMapZiplines.None.FindNearest(Vector3.Zero, 100f), Is.EqualTo(-1));
        }

        [Test]
        public void LinesAtTheSameDistanceGoToTheFirst()
        {
            var ziplines = new NetMapZiplines(new[]
            {
                new MapZipline(new Vector3(-1f, 3f, 0f), new Vector3(-1f, 3f, 20f), ZiplineSpec.Default),
                new MapZipline(new Vector3(1f, 3f, 0f), new Vector3(1f, 3f, 20f), ZiplineSpec.Default),
            });

            Assert.That(ziplines.FindNearest(new Vector3(0f, 3f, 5f), 1.5f), Is.EqualTo(0));
        }

        [Test]
        public void TheMotionFromAMapLineSlidesLikeTheSceneLine()
        {
            var line = new MapZipline(new Vector3(0f, 6f, 0f), new Vector3(0f, 2f, 20f), ZiplineSpec.Default);

            var motion = line.CreateMotion();

            Assert.That(motion.Start, Is.EqualTo(line.Start));
            Assert.That(motion.End, Is.EqualTo(line.End));
            Assert.That(motion.Spec, Is.SameAs(line.Spec));
            Assert.That(line.CreateMotion(), Is.Not.SameAs(motion), "every rider gets their own motion");
        }
    }
}
