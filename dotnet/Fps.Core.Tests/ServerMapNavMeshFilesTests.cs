using System.IO;
using System.Numerics;
using Fps.Core.Navigation;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ServerMapNavMeshFilesTests
    {
        private const string _mapsFolder = "server-data/maps";

        [Test]
        public void EveryCommittedMapLoadsAndRoutesAcrossItsOwnMesh()
        {
            var directory = MapsDirectory();
            var files = directory == null ? System.Array.Empty<string>() : Directory.GetFiles(directory, "*" + NavMeshFile.Extension);
            // 書き出しには Unity が要るので、まだコミットされていない間は確かめるものが無い。
            if (files.Length == 0)
            {
                Assert.Ignore("no " + NavMeshFile.Extension + " under " + _mapsFolder + " yet; run make export-server-data and commit them");
            }

            foreach (var path in files)
            {
                NavMeshFile file;
                using (var stream = File.OpenRead(path))
                {
                    file = NavMeshFile.Read(stream);
                }

                Assert.That(file.Triangles, Is.Not.Empty, path);
                var navigation = new MeshNavigation(file);
                var from = Centroid(file, file.Triangles[0]);
                var to = Centroid(file, file.Triangles[file.Triangles.Count - 1]);
                var navPath = new NavPath();
                Assert.That(navigation.CalculatePath(from, to, ~0, navPath), Is.True, path);
                Assert.That(navPath.Points, Is.Not.Empty, path);
            }
        }

        // テストはビルド先 (bin/...) から走るので、リポジトリのルートまで上ってマップのフォルダを探す。
        private static string MapsDirectory()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, _mapsFolder);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static Vector3 Centroid(NavMeshFile file, NavTriangle triangle)
        {
            return (file.Vertices[triangle.A] + file.Vertices[triangle.B] + file.Vertices[triangle.C]) / 3f;
        }
    }
}
