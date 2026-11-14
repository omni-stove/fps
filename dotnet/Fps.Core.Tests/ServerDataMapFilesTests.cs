using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // サーバーは Unity 無しでコミットされた .fpsmap を読むので、Unity の書き出したファイルがここでも読めることを確かめる
    public sealed class ServerDataMapFilesTests
    {
        private static IEnumerable<string> MapFiles()
        {
            var directory = Path.Combine(RepositoryRoot(), "server-data", "maps");
            return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.fpsmap").OrderBy(p => p, System.StringComparer.Ordinal) : Enumerable.Empty<string>();
        }

        [Test]
        public void ServerDataHasMaps()
        {
            Assert.That(MapFiles(), Is.Not.Empty, "run make export-server-data and commit server-data/maps");
        }

        [TestCaseSource(nameof(MapFiles))]
        public void MapLoadsAndHasAFloorUnderItsMiddle(string path)
        {
            WorldMap map;
            using (var stream = File.OpenRead(path))
            {
                map = FpsMapFile.Read(stream);
            }

            Assert.That(map.Colliders, Is.Not.Empty);
            var world = new BvhWorldCollision(map);
            Assert.That(world.PrimitiveCount, Is.GreaterThanOrEqualTo(map.Colliders.Count));
            Assert.That(world.Raycast(new Vector3(0f, 100f, 0f), -Vector3.UnitY, 200f, ~0, out var hit), Is.True, "nothing below the map origin");
            Assert.That(hit.Normal.Y, Is.GreaterThan(0.5f), "the ground faces up");
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assets")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "no Assets folder above " + TestContext.CurrentContext.TestDirectory);
            return directory.FullName;
        }
    }
}
