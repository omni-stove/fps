using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net.Tests
{
    /// <summary>
    /// <see cref="ArenaSceneBuilder"/> の配置の値と、空のシーンへの組み立て。保存も NavMesh の焼き込みもしない。
    /// </summary>
    public sealed class ArenaSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;
        private const string BaseScenePath = "Temp/FpsTestBase.unity";

        private Scene _scene;
        private readonly List<GameObject> _created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _scene = NewAdditiveScene();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void SceneIsNamedAfterTheMapAndIsInTheBuilds()
        {
            Assert.That(Path.GetFileNameWithoutExtension(ArenaSceneBuilder.ScenePath), Is.EqualTo(ArenaSceneBuilder.MapName),
                "-map finds the scene by its file name");
            Assert.That(NetLaunchOptions.IsMapName(ArenaSceneBuilder.MapName), Is.True);
            Assert.That(ArenaSceneBuilder.ScenePath, Is.EqualTo(BuildSceneList.NetArena01Scene));
            Assert.That(BuildSceneList.Scenes, Has.Member(ArenaSceneBuilder.ScenePath), "clients load the map the server chose");
            Assert.That(ServerBuilder.ServerScenePaths(), Has.Member(ArenaSceneBuilder.ScenePath));
            Assert.That(NetBootstrap.TryFindMapScene(ArenaSceneBuilder.MapName, ServerBuilder.ServerScenePaths(), out var path), Is.True);
            Assert.That(path, Is.EqualTo(ArenaSceneBuilder.ScenePath));
        }

        [Test]
        public void EachTeamHasEightSpawnPointsMirroredAcrossTheMiddle()
        {
            var alpha = ArenaSceneBuilder.SpawnPoints.Where(p => p.Team == NetSpawnPoint.SpawnTeam.Alpha).ToArray();
            var bravo = ArenaSceneBuilder.SpawnPoints.Where(p => p.Team == NetSpawnPoint.SpawnTeam.Bravo).ToArray();

            Assert.That(alpha, Has.Length.EqualTo(ArenaSceneBuilder.SpawnsPerTeam));
            Assert.That(bravo, Has.Length.EqualTo(ArenaSceneBuilder.SpawnsPerTeam));
            Assert.That(ArenaSceneBuilder.SpawnPoints, Has.Count.EqualTo(ArenaSceneBuilder.SpawnsPerTeam * 2), "no spawn point is shared");
            Assert.That(ArenaSceneBuilder.SpawnPoints.Select(p => p.Name), Is.Unique);

            foreach (var point in alpha)
            {
                Assert.That(point.Position.z, Is.LessThan(0f), point.Name + " is on the Alpha side");
                Assert.That(point.Position.y, Is.EqualTo(0f), point.Name + " puts the capsule's centre at y = 0");
                Assert.That(bravo.Count(p => Near(p.Position, ArenaSceneBuilder.MirrorZ(point.Position))), Is.EqualTo(1), point.Name);
            }
        }

        [Test]
        public void SpawnPointsCanStartTeamDeathmatch()
        {
            var points = ArenaSceneBuilder.SpawnPoints
                .Select(p => new SpawnPoint(new System.Numerics.Vector3(p.Position.x, p.Position.y, p.Position.z), NetSpawnPoint.ToCoreTeam(p.Team)))
                .ToArray();

            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(10, 300f, 3f), 100, points, new SpawnSelector(), _ => System.Numerics.Vector3.Zero);

            Assert.That(session.Join(0), Is.EqualTo(Team.Alpha));
            Assert.That(session.Join(1), Is.EqualTo(Team.Bravo));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void LevelIsMirroredAcrossTheMiddle()
        {
            foreach (var (name, center, size) in LevelBoxes())
            {
                var mirrored = LevelBoxes().Count(b => Near(b.Center, ArenaSceneBuilder.MirrorZ(center)) && Near(b.Size, size));
                Assert.That(mirrored, Is.EqualTo(1), name + " has one counterpart on the other side");
            }

            foreach (var (name, center) in ArenaSceneBuilder.Buildings)
            {
                Assert.That(center.z, Is.EqualTo(0f), name + " straddles the middle");
            }

            var points = ArenaSceneBuilder.ControlPoints;
            Assert.That(points.Select(p => p.Name), Is.EqualTo(new[] { "Control Point A", "Control Point B", "Control Point C" }));
            Assert.That(points.Select(p => p.Index), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(points[0].Position.z, Is.LessThan(0f), "A is on the Alpha side");
            Assert.That(points[1].Position.z, Is.EqualTo(0f), "B is in the middle");
            Assert.That(Near(points[2].Position, ArenaSceneBuilder.MirrorZ(points[0].Position)), Is.True, "C mirrors A");
        }

        [Test]
        public void ControlPointAreasStayClearOfWallsStepsBuildingsAndSpawns()
        {
            Assert.That(ArenaSceneBuilder.ControlPointOverlaps(), Is.Empty);
            Assert.That(ArenaSceneBuilder.ControlPointClearance, Is.GreaterThanOrEqualTo(NetSceneBuilder.PlayerRadius),
                "a player standing on the edge of a point does not touch cover");

            foreach (var (name, position, _) in ArenaSceneBuilder.ControlPoints)
            {
                Assert.That(position.y, Is.EqualTo(ArenaSceneBuilder.GroundY), name + " sits on the ground");

                // 拠点の円の縁 (クリアランス込み) のどこにも、壁・段差の箱が掛からない
                var reach = ArenaSceneBuilder.ControlPointRadius + ArenaSceneBuilder.ControlPointClearance;
                for (var i = 0; i < 32; i++)
                {
                    var angle = i * Mathf.PI * 2f / 32;
                    var edge = position + new Vector3(Mathf.Cos(angle) * reach, 0.1f, Mathf.Sin(angle) * reach);
                    foreach (var (boxName, center, size) in LevelBoxes())
                    {
                        Assert.That(new Bounds(center, size).Contains(edge), Is.False, name + " reaches into " + boxName);
                    }
                }
            }
        }

        [Test]
        public void ControlPointsCanStartADominationMatch()
        {
            var points = ArenaSceneBuilder.ControlPoints
                .Select(p => ArenaSceneBuilder.CreateControlPoint(_scene, p.Name, p.Position, p.Index))
                .ToArray();

            Assert.That(NetControlPoint.TryBuildMap(points, out var map, out var byIndex, out var error), Is.True, error);
            Assert.That(byIndex.Select(p => p.name), Is.EqualTo(ArenaSceneBuilder.ControlPoints.Select(p => p.Name)));
            Assert.That(() => new DominationObjective(map, 10f, 1f, 200, 600f, 3), Throws.Nothing);
        }

        [Test]
        public void WallsAndStepsMatchTheMovementRules()
        {
            Assert.That(ArenaSceneBuilder.HighWalls, Is.Not.Empty);
            Assert.That(ArenaSceneBuilder.LowWalls, Is.Not.Empty);
            Assert.That(ArenaSceneBuilder.Steps, Is.Not.Empty);

            foreach (var (name, _, size) in ArenaSceneBuilder.HighWalls)
            {
                Assert.That(size.y, Is.GreaterThan(MotorSpec.DefaultVaultMaxHeight), name + " cannot be vaulted");
                Assert.That(size.y, Is.GreaterThanOrEqualTo(CoverProbe.LowWallHeight), name + " is high cover");
            }

            foreach (var (name, _, size) in ArenaSceneBuilder.LowWalls)
            {
                Assert.That(size.y, Is.LessThan(CoverProbe.LowWallHeight), name + " is low cover");
                AssertVaultable(name, size);
            }

            foreach (var (name, _, size) in ArenaSceneBuilder.Steps)
            {
                AssertVaultable(name, size);
            }

            foreach (var (name, center, size) in LevelBoxes())
            {
                Assert.That(center.y - (size.y / 2f), Is.EqualTo(ArenaSceneBuilder.GroundY).Within(Epsilon), name + " stands on the ground");
            }
        }

        [Test]
        public void EverythingIsInsideTheBoundaryAndSpawnPointsAreClear()
        {
            var limit = ArenaSceneBuilder.HalfExtent;
            foreach (var (name, center, size) in ArenaSceneBuilder.HighWalls.Concat(ArenaSceneBuilder.LowWalls).Concat(ArenaSceneBuilder.Steps))
            {
                Assert.That(Mathf.Abs(center.x) + (size.x / 2f), Is.LessThan(limit), name);
                Assert.That(Mathf.Abs(center.z) + (size.z / 2f), Is.LessThan(limit), name);
            }

            foreach (var (name, position, _) in ArenaSceneBuilder.SpawnPoints)
            {
                Assert.That(Mathf.Abs(position.x), Is.LessThan(limit - NetSceneBuilder.PlayerRadius), name);
                Assert.That(Mathf.Abs(position.z), Is.LessThan(limit - NetSceneBuilder.PlayerRadius), name);

                var feet = ArenaSceneBuilder.Feet(position);
                foreach (var (boxName, center, size) in LevelBoxes())
                {
                    var bounds = new Bounds(center, size + (Vector3.one * NetSceneBuilder.PlayerRadius * 2f));
                    Assert.That(bounds.Contains(feet), Is.False, name + " overlaps " + boxName);
                }

                foreach (var (buildingName, groundCenter) in ArenaSceneBuilder.Buildings)
                {
                    var footprint = new Vector3(ArenaSceneBuilder.BuildingWidth, 1f, ArenaSceneBuilder.BuildingDepth) * ArenaSceneBuilder.PieceSize;
                    var bounds = new Bounds(groundCenter, footprint + (Vector3.one * NetSceneBuilder.PlayerRadius * 2f));
                    Assert.That(bounds.Contains(feet), Is.False, name + " is inside " + buildingName);
                }
            }
        }

        [Test]
        public void LevelHasTheGroundAndEveryBoxUnderOneNavMeshSurface()
        {
            var surface = ArenaSceneBuilder.CreateLevel(_scene);

            Assert.That(_scene.rootCount, Is.EqualTo(1), "only the level is baked; the buildings are placed afterwards");
            Assert.That(surface.gameObject.name, Is.EqualTo(ArenaSceneBuilder.LevelName));
            Assert.That(surface.collectObjects, Is.EqualTo(CollectObjects.Children));

            var ground = surface.transform.Find(ArenaSceneBuilder.GroundName);
            Assert.That(ground, Is.Not.Null);
            Assert.That(ground.localScale, Is.EqualTo(ArenaSceneBuilder.GroundScale));
            Assert.That(ground.position.y, Is.EqualTo(ArenaSceneBuilder.GroundY));

            var boxes = surface.GetComponentsInChildren<BoxCollider>(true);
            Assert.That(boxes, Has.Length.EqualTo(LevelBoxes().Count() + ArenaSceneBuilder.BoundaryWalls.Count));
            Assert.That(boxes.All(b => b.gameObject.isStatic), Is.True);
        }

        [Test]
        public void LevelRefusesANonEmptyScene()
        {
            var stray = new GameObject("Stray");
            SceneManager.MoveGameObjectToScene(stray, _scene);

            Assert.Throws<System.InvalidOperationException>(() => ArenaSceneBuilder.CreateLevel(_scene));
        }

        [Test]
        public void PopulatedSceneHasTheBootstrapSpawnPointsAndControlPoints()
        {
            var player = Create("TestPlayer");
            var match = Create("TestMatch");

            var bootstrap = ArenaSceneBuilder.PopulateScene(_scene, player, match);

            Assert.That(bootstrap.gameObject.scene, Is.EqualTo(_scene));
            Assert.That(bootstrap.transform.parent, Is.Null, "NetworkManager needs a root GameObject");
            Assert.That(bootstrap.PlayerPrefab, Is.SameAs(player));
            Assert.That(bootstrap.MatchPrefab, Is.SameAs(match));
            Assert.That(Components<NetBootstrap>(), Has.Length.EqualTo(1));
            Assert.That(Components<NetSpectatorCamera>(), Has.Length.EqualTo(1));
            Assert.That(Components<NetworkManager>(), Is.Empty, "NetBootstrap configures the NetworkManager in code");

            var spawns = Components<NetSpawnPoint>();
            Assert.That(spawns.Count(s => s.Team == NetSpawnPoint.SpawnTeam.Alpha), Is.EqualTo(ArenaSceneBuilder.SpawnsPerTeam));
            Assert.That(spawns.Count(s => s.Team == NetSpawnPoint.SpawnTeam.Bravo), Is.EqualTo(ArenaSceneBuilder.SpawnsPerTeam));

            var points = Components<NetControlPoint>();
            Assert.That(points, Has.Length.EqualTo(ArenaSceneBuilder.ControlPoints.Count));
            foreach (var (name, position, index) in ArenaSceneBuilder.ControlPoints)
            {
                var point = points.Single(p => p.name == name);
                Assert.That(point.transform.parent, Is.Null, name + " is an in-scene NetworkObject at the root");
                Assert.That(point.GetComponent<NetworkObject>(), Is.Not.Null, name);
                Assert.That(point.transform.position, Is.EqualTo(position), name);
                Assert.That(point.Zone, Is.Not.Null, name);
                Assert.That(point.Index, Is.EqualTo(index), name);
                Assert.That(point.Zone.Radius, Is.EqualTo(ArenaSceneBuilder.ControlPointRadius), name);
                Assert.That(point.Zone.ShowsMarker, Is.True, name + " shows the ground circle while playing");

                // 床に立つプレイヤーのカプセルの中心 (y = 0) が範囲に入る
                Assert.That(point.Zone.Contains(new Vector3(position.x, 0f, position.z)), Is.True, name);

                foreach (var part in new[] { DominationSceneBuilder.PoleName, DominationSceneBuilder.FlagName })
                {
                    var child = point.transform.Find(part);
                    Assert.That(child, Is.Not.Null, name + " has a " + part);
                    Assert.That(child.GetComponent<Renderer>(), Is.Not.Null, name + " " + part);
                    Assert.That(child.GetComponent<Collider>(), Is.Null, name + " " + part + " does not block shots or movement");
                }
            }
        }

        [Test]
        public void BuildingsAreNetworkedDestructibleStructuresWithDoors()
        {
            ArenaSceneBuilder.PopulateScene(_scene, Create("TestPlayer"), Create("TestMatch"));

            var structures = Components<DestructibleStructure>();
            Assert.That(structures, Has.Length.EqualTo(ArenaSceneBuilder.Buildings.Count));
            foreach (var structure in structures)
            {
                Assert.That(structure.transform.parent, Is.Null, structure.name + " is an in-scene NetworkObject at the root");
                Assert.That(structure.GetComponent<NetDestructibleStructure>(), Is.Not.Null, structure.name);
                Assert.That(structure.GetComponent<NetworkObject>(), Is.Not.Null, structure.name);
                Assert.That(structure.DestructionEnabled, Is.False, "the match turns destruction on from its rules");
                Assert.That(structure.DebrisPool, Is.Not.Null, structure.name);

                var pieces = structure.Pieces;
                Assert.That(pieces, Has.Count.EqualTo(ArenaSceneBuilder.BuildingPieceCount), structure.name);
                for (var i = 0; i < pieces.Count; i++)
                {
                    Assert.That(pieces[i].Index, Is.EqualTo(i), structure.name);
                }

                // 出入口の中央 (足元から 1 m) には部品が無く、南北どちらからも入れる
                var southDoor = structure.transform.position
                    + new Vector3(0f, 1f, -((ArenaSceneBuilder.BuildingDepth - 1) * ArenaSceneBuilder.PieceSize / 2f));
                Assert.That(pieces.Any(p => p.Collider.bounds.Contains(southDoor)), Is.False, structure.name + " south door");
                Assert.That(pieces.Any(p => p.Collider.bounds.Contains(ArenaSceneBuilder.MirrorZ(southDoor - structure.transform.position) + structure.transform.position)),
                    Is.False, structure.name + " north door");
            }
        }

        [Test]
        public void BuildingsUseThePieceVisualPrefabWhenOneIsSet()
        {
            var prefab = Create("Brick");
            GameObject.CreatePrimitive(PrimitiveType.Cube).transform.SetParent(prefab.transform, false);

            ArenaSceneBuilder.PopulateScene(_scene, Create("TestPlayer"), Create("TestMatch"), prefab);

            var pieces = Components<DestructiblePiece>();
            Assert.That(pieces, Has.Length.EqualTo(ArenaSceneBuilder.BuildingPieceCount * ArenaSceneBuilder.Buildings.Count));
            foreach (var piece in pieces)
            {
                Assert.That(piece.GetComponent<MeshFilter>(), Is.Null, piece.name + " has no placeholder cube");
                Assert.That(piece.Collider, Is.InstanceOf<BoxCollider>(), piece.name);
                var visual = piece.transform.Find(DestructiblePiece.VisualName);
                Assert.That(visual, Is.Not.Null, piece.name);
                Assert.That(visual.GetComponentInChildren<MeshFilter>(), Is.Not.Null, piece.name + " shows the prefab");
            }
        }

        [Test]
        public void BuildingsKeepThePlaceholderCubesWithoutAPrefab()
        {
            ArenaSceneBuilder.PopulateScene(_scene, Create("TestPlayer"), Create("TestMatch"));

            foreach (var piece in Components<DestructiblePiece>())
            {
                Assert.That(piece.GetComponent<MeshFilter>(), Is.Not.Null, piece.name + " is the placeholder cube");
                Assert.That(piece.transform.Find(DestructiblePiece.VisualName), Is.Null, piece.name);
            }
        }

        [Test]
        public void DoorIsInTheMiddleOfTheWall()
        {
            Assert.That(ArenaSceneBuilder.IsDoor(0, 0), Is.False);
            Assert.That(ArenaSceneBuilder.IsDoor(ArenaSceneBuilder.BuildingWidth / 2, 0), Is.True);
            Assert.That(ArenaSceneBuilder.IsDoor(ArenaSceneBuilder.BuildingWidth / 2, ArenaSceneBuilder.DoorHeight), Is.False, "the lintel stays");
            Assert.That(
                Enumerable.Range(0, ArenaSceneBuilder.BuildingWidth).Count(x => ArenaSceneBuilder.IsDoor(x, 0)),
                Is.EqualTo(ArenaSceneBuilder.DoorWidth));
            Assert.That(ArenaSceneBuilder.DoorHeight * ArenaSceneBuilder.PieceSize, Is.GreaterThan(NetSceneBuilder.PlayerHeight), "a player fits through");
        }

        private static IEnumerable<(string Name, Vector3 Center, Vector3 Size)> LevelBoxes()
        {
            return ArenaSceneBuilder.HighWalls.Concat(ArenaSceneBuilder.LowWalls).Concat(ArenaSceneBuilder.Steps);
        }

        private static void AssertVaultable(string name, Vector3 size)
        {
            Assert.That(size.y, Is.GreaterThanOrEqualTo(VaultProbe.MinHeight), name + " is not a mere step");
            Assert.That(size.y, Is.LessThanOrEqualTo(MotorSpec.DefaultVaultMaxHeight), name + " can be vaulted");
            Assert.That(Mathf.Min(size.x, size.z), Is.LessThanOrEqualTo(VaultProbe.MaxDepth), name + " is thin enough to vault over");
        }

        private static bool Near(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < Epsilon * Epsilon;
        }

        private T[] Components<T>()
            where T : Component
        {
            return _scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        /// <summary>Fps.Tests.EditMode の TestScenes.NewAdditive と同じ。保存されていない Untitled があると Additive の NewScene は例外を投げる。</summary>
        private static Scene NewAdditiveScene()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                {
                    if (File.Exists(BaseScenePath))
                    {
                        EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
                    }
                    else
                    {
                        EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), BaseScenePath);
                    }

                    break;
                }
            }

            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }
    }
}
