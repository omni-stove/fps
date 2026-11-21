using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Core.Navigation;

namespace Shooter.Server.Tests
{
    // Bot のテストで使うマップ。床の上面は y=0 で、体の原点 (カプセルの中心、高さ 2) は足元から 1m 上
    internal static class BotTestMaps
    {
        public const float FloorY = 0f;
        public const float PlatformY = 3f;
        public const float LadderZ = 10f;

        public static readonly Vector3 LowerSpawn = new Vector3(0f, 1.1f, -6f);
        public static readonly Vector3 UpperSpawn = new Vector3(0f, PlatformY + 1.1f, 16f);

        // 床は x・z が -30〜30 の平らな四角
        public static ServerMapData Flat(
            NetMapPoints points, NetMapStructures structures = null, IEnumerable<WorldCollider> extra = null, NetMapVehicles vehicles = null)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 0f), new Vector3(30f, 0.5f, 30f), Quaternion.Identity),
            };
            if (extra != null)
            {
                colliders.AddRange(extra);
            }

            var navMesh = new NavMeshFile(Quads((-29.5f, -29.5f, 29.5f, 29.5f, FloorY)), Triangles(1));
            return new ServerMapData("BotFlat", new WorldMap(colliders), navMesh, "memory", points, structures, vehicles: vehicles);
        }

        // z<10 の床から、z=10 の面に +Z を向いて立てたはしご (幅 0.8、高さ 3) で、z>=10 の高さ 3 の足場へ上る。
        // NavMesh は床と足場の 2 枚で、はしごのリンクだけがつなぐ (Unity の Ladder.Link と同じ、面から半径 + 0.3 離した端)
        public static ServerMapData Ladder(NetMapPoints points, NetMapStructures structures = null, IEnumerable<WorldCollider> extra = null)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(0f, -0.5f, 5f), new Vector3(10f, 0.5f, 15f), Quaternion.Identity),
                WorldCollider.Box(2, 0, WorldCollider.StaticGroup, new Vector3(0f, PlatformY / 2f, 15f), new Vector3(10f, PlatformY / 2f, 5f), Quaternion.Identity),
            };
            if (extra != null)
            {
                colliders.AddRange(extra);
            }

            var span = LadderSpan();
            var clearance = ServerMotorSettings.DefaultRadius + 0.3f;
            var navMesh = new NavMeshFile(
                Quads((-9.5f, -9.5f, 9.5f, LadderZ - 0.5f, FloorY), (-9.5f, LadderZ + 0.5f, 9.5f, 19.5f, PlatformY)),
                Triangles(2),
                null,
                new[] { new NavLink(span.LinkBottom(clearance), span.LinkTop(clearance), true, 0, NavLinkKind.Ladder) });
            var ladders = new NetMapLadders(new[] { new MapLadder(span, LadderSpec.Default) });
            return new ServerMapData("BotLadder", new WorldMap(colliders), navMesh, "memory", points, structures, ladders);
        }

        public static LadderSpan LadderSpan()
        {
            return new LadderSpan(new Vector3(0f, FloorY, LadderZ), Vector3.UnitZ, PlatformY, 0.8f);
        }

        public static NetMapPoints Points(Vector3 alpha, Vector3 bravo)
        {
            return new NetMapPoints(new[] { new SpawnPoint(alpha, Team.Alpha), new SpawnPoint(bravo, Team.Bravo) }, System.Array.Empty<ControlPointArea>());
        }

        private static List<Vector3> Quads(params (float X0, float Z0, float X1, float Z1, float Y)[] quads)
        {
            var vertices = new List<Vector3>();
            foreach (var quad in quads)
            {
                vertices.Add(new Vector3(quad.X0, quad.Y, quad.Z0));
                vertices.Add(new Vector3(quad.X1, quad.Y, quad.Z0));
                vertices.Add(new Vector3(quad.X1, quad.Y, quad.Z1));
                vertices.Add(new Vector3(quad.X0, quad.Y, quad.Z1));
            }

            return vertices;
        }

        private static List<NavTriangle> Triangles(int quads)
        {
            var triangles = new List<NavTriangle>();
            for (var q = 0; q < quads; q++)
            {
                var i = q * 4;
                triangles.Add(new NavTriangle(i, i + 1, i + 2, 0));
                triangles.Add(new NavTriangle(i, i + 2, i + 3, 0));
            }

            return triangles;
        }
    }
}
