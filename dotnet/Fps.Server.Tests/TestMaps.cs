using System.IO;
using System.Numerics;
using Fps.Core;
using Fps.Core.Navigation;

namespace Fps.Server.Tests
{
    internal static class TestMaps
    {
        public static WorldMap World()
        {
            return new WorldMap(new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(10f, 0.5f, 10f), Quaternion.Identity),
            });
        }

        public static NavMeshFile NavMesh()
        {
            return new NavMeshFile(
                new[] { new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.5f, 1f), new Vector3(1f, 0.5f, 0f) },
                new[] { new NavTriangle(0, 1, 2, 0) });
        }

        public static ServerMapData Data(string name = "NetTest", NetMapPoints points = null, NetMapLadders ladders = null)
        {
            return new ServerMapData(name, World(), NavMesh(), "memory", points, ladders);
        }

        /// <summary>床の上、原点から +Z へ 3m の所に、+Z を向いて立つ高さ 3m のはしご。</summary>
        public static NetMapLadders OneLadder()
        {
            return new NetMapLadders(new[]
            {
                new MapLadder(new LadderSpan(new Vector3(0f, 0.5f, 3f), Vector3.UnitZ, 3f, 0.8f), LadderSpec.Default),
            });
        }

        public static string Write(
            string root, string name, bool collision = true, bool navigation = true, NetMapPoints points = null, NetMapLadders ladders = null)
        {
            var directory = Path.Combine(root, ServerMapData.DataDirectoryName, ServerMapData.MapsDirectoryName);
            Directory.CreateDirectory(directory);
            if (collision)
            {
                using (var stream = File.Create(Path.Combine(directory, name + ServerMapData.CollisionExtension)))
                {
                    FpsMapFile.Write(stream, World());
                }
            }

            if (navigation)
            {
                using (var stream = File.Create(Path.Combine(directory, name + NavMeshFile.Extension)))
                {
                    NavMesh().Write(stream);
                }
            }

            if (points != null)
            {
                File.WriteAllText(Path.Combine(directory, name + NetMapPoints.Extension), NetMapPoints.Write(points));
            }

            if (ladders != null)
            {
                File.WriteAllText(Path.Combine(directory, name + NetMapLadders.Extension), NetMapLadders.Write(ladders));
            }

            return directory;
        }
    }
}
