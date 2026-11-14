using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class NetMapLaddersTests
    {
        [Test]
        public void WrittenLaddersReadBackInTheSameOrder()
        {
            var ladders = new NetMapLadders(new[]
            {
                new MapLadder(new LadderSpan(new Vector3(1f, 0f, 2f), new Vector3(0f, 0f, 2f), 3f, 0.8f), LadderSpec.Default),
                new MapLadder(new LadderSpan(new Vector3(-4f, 1.5f, 0f), Vector3.UnitX, 5.5f, 1.2f), new LadderSpec(3f, 0.25f, 5f, 0.1f)),
            });

            var text = NetMapLadders.Write(ladders);
            var read = NetMapLadders.Parse(text, "test");

            Assert.That(read.Ladders, Has.Count.EqualTo(2));
            Assert.That(read.Ladders[0].Span.Bottom, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(read.Ladders[0].Span.Facing, Is.EqualTo(Vector3.UnitZ));
            Assert.That(read.Ladders[1].Span.Height, Is.EqualTo(5.5f));
            Assert.That(read.Ladders[1].Span.Width, Is.EqualTo(1.2f));
            Assert.That(read.Ladders[1].Spec.ClimbSpeed, Is.EqualTo(3f));
            Assert.That(read.Ladders[1].Spec.GrabDistance, Is.EqualTo(0.25f));
            Assert.That(read.Ladders[1].Spec.JumpOffSpeed, Is.EqualTo(5f));
            Assert.That(read.Ladders[1].Spec.RegrabSeconds, Is.EqualTo(0.1f));
            Assert.That(NetMapLadders.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void MissingTuningUsesTheLadderDefaults()
        {
            var read = NetMapLadders.Parse(
                "{ \"ladders\": [ { \"bottom\": [0, 0, 0], \"facing\": [0, 0, 1], \"height\": 3, \"width\": 1 } ] }", "test");

            Assert.That(read.Ladders[0].Spec.ClimbSpeed, Is.EqualTo(LadderSpec.DefaultClimbSpeed));
            Assert.That(read.Ladders[0].Spec.RegrabSeconds, Is.EqualTo(LadderSpec.DefaultRegrabSeconds));
        }

        [TestCase("{ \"ladders\": [ { \"bottom\": [0, 0, 0], \"facing\": [0, 1, 0], \"height\": 3, \"width\": 1 } ] }", "ladders[0].facing")]
        [TestCase("{ \"ladders\": [ { \"bottom\": [0, 0, 0], \"facing\": [0, 0, 1], \"height\": -1, \"width\": 1 } ] }", "ladders[0].height")]
        [TestCase("{ \"ladders\": [ { \"bottom\": [0, 0, 0], \"facing\": [0, 0, 1], \"height\": 3 } ] }", "ladders[0].width")]
        [TestCase("{ \"ladders\": [ { \"bottom\": [0, 0, 0], \"facing\": [0, 0, 1], \"height\": 3, \"width\": 1, \"climbSpeed\": 0 } ] }", "ladders[0].climbSpeed")]
        [TestCase("{ \"ladders\": [ { \"bottom\": [0, 0, 0], \"facing\": [0, 0, 1], \"height\": 3, \"width\": 1, \"rungs\": 4 } ] }", "rungs")]
        public void BadLaddersNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMapLadders.Parse(text, "map.ladders.json"));
            Assert.That(e.Message, Does.Contain(item));
        }

        [TestCaseSource(nameof(CommittedMaps))]
        public void EveryCommittedMapHasLaddersWrittenByTheExporter(string collisionPath)
        {
            var path = Path.ChangeExtension(collisionPath, null) + NetMapLadders.Extension;
            Assert.That(File.Exists(path), Is.True, "run make export-server-data and commit " + path);
            var text = File.ReadAllText(path);
            Assert.That(NetMapLadders.Write(NetMapLadders.Parse(text, path)), Is.EqualTo(text));
        }

        private static IEnumerable<string> CommittedMaps()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assets")))
            {
                directory = directory.Parent;
            }

            var maps = directory == null ? null : Path.Combine(directory.FullName, "server-data", "maps");
            return maps != null && Directory.Exists(maps)
                ? Directory.GetFiles(maps, "*.fpsmap").OrderBy(p => p, System.StringComparer.Ordinal)
                : Enumerable.Empty<string>();
        }
    }
}
