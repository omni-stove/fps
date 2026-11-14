using System.IO;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerMapDataTests
    {
        private TempDirectory _first;
        private TempDirectory _second;

        [SetUp]
        public void SetUp()
        {
            _first = new TempDirectory();
            _second = new TempDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            _first.Dispose();
            _second.Dispose();
        }

        [Test]
        public void LoadsTheCollisionAndNavMeshAsTheCoreInterfaces()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Directory, Is.EqualTo(Path.GetFullPath(directory)));
            Assert.That(data.ColliderCount, Is.EqualTo(1));
            Assert.That(data.World.Raycast(new Vector3(0f, 5f, 0f), -Vector3.UnitY, 10f, ~0, out var hit), Is.True);
            Assert.That(hit.Point.Y, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(data.Navigation.SamplePosition(new Vector3(0.2f, 0.5f, 0.2f), 1f, ~0, out _), Is.True);
            Assert.That(data.LogLine, Does.StartWith("[Server] map NetArena01 colliders=1 navTriangles=1 navLinks=0 spawnPoints=none controlPoints=none ladders=0 from="));
        }

        [Test]
        public void LoadsTheLaddersBesideTheCollision()
        {
            TestMaps.Write(_second.Path, "NetArena01", ladders: TestMaps.OneLadder());

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Ladders.Ladders, Has.Count.EqualTo(1));
            Assert.That(data.Ladders.Ladders[0].Span.Bottom, Is.EqualTo(new Vector3(0f, 0.5f, 3f)));
            Assert.That(data.LogLine, Does.Contain(" ladders=1 "));
        }

        [Test]
        public void BrokenLaddersAreAnError()
        {
            var directory = TestMaps.Write(_second.Path, "NetArena01");
            File.WriteAllText(Path.Combine(directory, "NetArena01" + NetMapLadders.Extension), "{ \"ladders\": [ { \"bottom\": [0, 0, 0] } ] }");

            Assert.That(ServerMapData.TryLoad("NetArena01", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("NetArena01.ladders.json"));
        }

        [Test]
        public void TheFirstRootWithTheMapWins()
        {
            TestMaps.Write(_first.Path, "NetTest");
            TestMaps.Write(_second.Path, "NetTest");

            Assert.That(ServerMapData.TryLoad("NetTest", Roots(), out var data, out _), Is.True);
            Assert.That(data.Directory, Does.StartWith(Path.GetFullPath(_first.Path)));
        }

        [Test]
        public void MissingCollisionNamesEveryPlaceLooked()
        {
            Assert.That(ServerMapData.TryLoad("NetTest", Roots(), out var data, out var error), Is.False);

            Assert.That(data, Is.Null);
            Assert.That(error, Does.Contain("NetTest.fpsmap"));
            Assert.That(error, Does.Contain(Path.GetFullPath(_first.Path)));
            Assert.That(error, Does.Contain(Path.GetFullPath(_second.Path)));
            Assert.That(error, Does.Contain("make export-server-data"));
        }

        [Test]
        public void MissingNavMeshIsAnError()
        {
            TestMaps.Write(_first.Path, "NetTest", navigation: false);

            Assert.That(ServerMapData.TryLoad("NetTest", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("NetTest.fpsnav"));
        }

        [Test]
        public void BrokenFileIsAnError()
        {
            var directory = TestMaps.Write(_first.Path, "NetTest");
            File.WriteAllBytes(Path.Combine(directory, "NetTest.fpsmap"), new byte[] { 1, 2, 3 });

            Assert.That(ServerMapData.TryLoad("NetTest", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.StartWith("cannot read "));
            Assert.That(error, Does.Contain("NetTest.fpsmap"));
        }

        [Test]
        public void NameThatIsNotAMapIsRejectedBeforeTouchingFiles()
        {
            Assert.That(ServerMapData.TryLoad("../NetTest", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("not a scene name"));
        }

        private string[] Roots()
        {
            return new[] { _first.Path, _second.Path };
        }
    }
}
