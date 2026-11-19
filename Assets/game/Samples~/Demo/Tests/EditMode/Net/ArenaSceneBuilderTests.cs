using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Net.Tests
{
    public sealed class ArenaSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;
        private const string BaseScenePath = "Temp/ShooterTestBase.unity";

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
            Assert.That(BuildSceneList.ServerScenes, Has.Member(ArenaSceneBuilder.ScenePath), "the server data is exported for it");
        }

        [Test]
        public void EachTeamHasEightSpawnPointsMirroredAcrossTheMiddle()
        {
            var alpha = ArenaSceneBuilder.SpawnPoints.Where(p => p.Team == NetSpawnPoint.SpawnTeam.Alpha).ToArray();
            var bravo = ArenaSceneBuilder.SpawnPoints.Where(p => p.Team == NetSpawnPoint.SpawnTeam.Bravo).ToArray();

            Assert.That(alpha, Has.Length.EqualTo(ArenaSceneBuilder.SpawnsPerTeam));
            Assert.That(bravo, Has.Length.EqualTo(ArenaSceneBuilder.SpawnsPerTeam));
            Assert.That(ArenaSceneBuilder.SpawnPoints.Count(), Is.EqualTo(ArenaSceneBuilder.SpawnsPerTeam * 2), "no spawn point is shared");
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
            var zones = ArenaSceneBuilder.ControlPoints
                .Select(p => ArenaSceneBuilder.CreateControlPoint(_scene, p.Name, p.Position, p.Index))
                .ToArray();

            Assert.That(zones.Select(z => z.Index), Is.EqualTo(ArenaSceneBuilder.ControlPoints.Select(p => p.Index)));
            var map = new ControlPointMap(zones.Select(z => z.Area).ToArray());
            Assert.That(() => new DominationObjective(map, 10f, 1f, 200, 600f, 3), Throws.Nothing);
        }

        [Test]
        public void EachTeamHasOneFlagBaseBehindItsSpawnsMirroredAcrossTheMiddle()
        {
            var bases = ArenaSceneBuilder.FlagBases;

            Assert.That(bases.Select(b => b.Side), Is.EqualTo(CaptureTheFlag.FlagSides));
            Assert.That(bases[0].Position, Is.EqualTo(new Vector3(0f, ArenaSceneBuilder.GroundY, -27f)));
            Assert.That(Near(bases[1].Position, ArenaSceneBuilder.MirrorZ(bases[0].Position)), Is.True, "Bravo mirrors Alpha");

            var alphaSpawns = ArenaSceneBuilder.SpawnPoints.Where(p => p.Team == NetSpawnPoint.SpawnTeam.Alpha);
            Assert.That(bases[0].Position.z, Is.LessThan(alphaSpawns.Min(p => p.Position.z)), "the Alpha base is behind the Alpha spawns");
            Assert.That(Mathf.Abs(bases[0].Position.z) + CaptureTheFlagSpec.DefaultCaptureRadius, Is.LessThan(ArenaSceneBuilder.HalfExtent),
                "the base area is inside the boundary");
        }

        [Test]
        public void FlagBaseAreasStayClearOfWallsSpawnsAndControlPoints()
        {
            Assert.That(ArenaSceneBuilder.FlagBaseOverlaps(CaptureTheFlagSpec.DefaultCaptureRadius), Is.Empty);
        }

        [Test]
        public void ATooWideCaptureRadiusIsReportedAsAnOverlap()
        {
            var problems = ArenaSceneBuilder.FlagBaseOverlaps(4f);

            Assert.That(problems, Has.Member("FlagBase-0 overlaps Boundary South"));
            Assert.That(problems, Has.Member("FlagBase-1 overlaps Boundary North"));
            Assert.That(problems, Has.Member("FlagBase-0 covers Spawn Alpha 2"));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void FlagBaseOverlapsRefusesARadiusThatIsNotPositive(float radius)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ArenaSceneBuilder.FlagBaseOverlaps(radius));
        }

        [Test]
        public void FlagBasesAreCheckedOnTheNavMesh()
        {
            var points = ArenaSceneBuilder.NavMeshCheckPoints();

            foreach (var (side, position) in ArenaSceneBuilder.FlagBases)
            {
                Assert.That(points, Has.Member(position), side.ToString());
            }
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
                Assert.That(size.y, Is.GreaterThanOrEqualTo(MotorSpec.DefaultCoverLowWallHeight), name + " is high cover");
            }

            foreach (var (name, _, size) in ArenaSceneBuilder.LowWalls)
            {
                Assert.That(size.y, Is.LessThan(MotorSpec.DefaultCoverLowWallHeight), name + " is low cover");
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
        public void PopulatedSceneHasTheCameraSpawnPointsAndControlPoints()
        {
            ArenaSceneBuilder.PopulateScene(_scene);

            Assert.That(Components<Camera>(), Has.Length.EqualTo(1), "a camera to show before the local player spawns");

            var spawns = Components<NetSpawnPoint>();
            Assert.That(spawns.Count(s => s.Team == NetSpawnPoint.SpawnTeam.Alpha), Is.EqualTo(ArenaSceneBuilder.SpawnsPerTeam));
            Assert.That(spawns.Count(s => s.Team == NetSpawnPoint.SpawnTeam.Bravo), Is.EqualTo(ArenaSceneBuilder.SpawnsPerTeam));

            var points = Components<ControlPointZone>();
            Assert.That(points, Has.Length.EqualTo(ArenaSceneBuilder.ControlPoints.Count));
            foreach (var (name, position, index) in ArenaSceneBuilder.ControlPoints)
            {
                var point = points.Single(p => p.name == name);
                Assert.That(point.transform.position, Is.EqualTo(position), name);
                Assert.That(point.Index, Is.EqualTo(index), name);
                Assert.That(point.Radius, Is.EqualTo(ArenaSceneBuilder.ControlPointRadius), name);
                Assert.That(point.ShowsMarker, Is.True, name + " shows the ground circle while playing");

                // 床に立つプレイヤーのカプセルの中心 (y = 0) が範囲に入る
                Assert.That(point.Contains(new Vector3(position.x, 0f, position.z)), Is.True, name);

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
        public void PopulatedSceneHasBothFlagBasesWithTheGivenRadiusButNoFlags()
        {
            ArenaSceneBuilder.PopulateScene(_scene, null, 3.5f);

            var bases = Components<FlagBase>();
            Assert.That(bases.Select(b => b.Side), Is.EqualTo(new[] { Side.Alpha, Side.Bravo }));
            foreach (var (side, position) in ArenaSceneBuilder.FlagBases)
            {
                var flagBase = bases.Single(b => b.Side == side);
                Assert.That(flagBase.name, Is.EqualTo("FlagBase-" + side.Id));
                Assert.That(flagBase.transform.parent.name, Is.EqualTo(NetSceneBuilder.FlagBasesRootName));
                Assert.That(flagBase.Position, Is.EqualTo(position));
                Assert.That(flagBase.Radius, Is.EqualTo(3.5f));
                Assert.That(flagBase.ShowsMarker, Is.True, "the base shows its disc while playing");
            }

            Assert.That(Components<FlagObject>(), Is.Empty, "the server carries the flags; a scene flag would stay on its base");
        }

        [Test]
        public void PopulatedSceneUsesTheDefaultCaptureRadiusWhenNoneIsGiven()
        {
            ArenaSceneBuilder.PopulateScene(_scene);

            Assert.That(Components<FlagBase>().Select(b => b.Radius), Is.All.EqualTo(CaptureTheFlagSpec.DefaultCaptureRadius));
        }

        [Test]
        public void FlagBasesNeedOneBasePerTeam()
        {
            var onlyAlpha = new[] { (Side.Alpha, Vector3.zero) };
            var twoAlpha = new[] { (Side.Alpha, Vector3.zero), (Side.Alpha, Vector3.one), (Side.Bravo, Vector3.zero) };

            Assert.Throws<System.ArgumentException>(() => NetSceneBuilder.CreateFlagBases(_scene, onlyAlpha, 2f));
            Assert.Throws<System.ArgumentException>(() => NetSceneBuilder.CreateFlagBases(_scene, twoAlpha, 2f));
            Assert.That(Components<FlagBase>(), Is.Empty, "nothing is left behind");
        }

        [Test]
        public void FlagBasesRefuseARadiusThatIsNotPositiveAndLeaveNothingBehind()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => NetSceneBuilder.CreateFlagBases(_scene, ArenaSceneBuilder.FlagBases, 0f));
            Assert.That(_scene.GetRootGameObjects().Where(go => go.name == NetSceneBuilder.FlagBasesRootName), Is.Empty);
        }

        [Test]
        public void BuildingsAreDestructibleStructuresWithDoors()
        {
            ArenaSceneBuilder.PopulateScene(_scene);

            var structures = Components<DestructibleStructure>();
            Assert.That(structures, Has.Length.EqualTo(ArenaSceneBuilder.Buildings.Count));
            foreach (var structure in structures)
            {
                Assert.That(structure.transform.parent, Is.Null, structure.name + " is numbered in scene order like the export");
                Assert.That(structure.DestructionEnabled, Is.False, "the server decides destruction from its rules");
                Assert.That(structure.DebrisPool, Is.Not.Null, structure.name);

                var pieces = structure.Pieces;
                Assert.That(pieces.Count(), Is.EqualTo(ArenaSceneBuilder.BuildingPieceCount), structure.name);
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

            ArenaSceneBuilder.PopulateScene(_scene, prefab);

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
            ArenaSceneBuilder.PopulateScene(_scene);

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

        [Test]
        public void PopulatedSceneHasOnePayloadCartOnItsRouteWithoutAZoneOrCollider()
        {
            ArenaSceneBuilder.PopulateScene(_scene);

            var carts = Components<PayloadCart>();
            Assert.That(carts, Has.Length.EqualTo(1));
            var cart = carts[0];
            Assert.That(cart.name, Is.EqualTo(ArenaSceneBuilder.PayloadCartName));
            Assert.That(cart.transform.parent.name, Is.EqualTo(ArenaSceneBuilder.PayloadRootName));
            Assert.That(cart.transform.position, Is.EqualTo(ArenaSceneBuilder.PayloadRoutePoints[0]), "the cart waits at the start");
            Assert.That(cart.Points.Select(p => p.position), Is.EqualTo(ArenaSceneBuilder.PayloadRoutePoints));
            Assert.That(cart.Points.All(p => !p.IsChildOf(cart.transform)), Is.True, "route points do not move with the cart");
            Assert.That(cart.Checkpoints, Is.EqualTo(new[] { 3 }));
            Assert.That(cart.GetComponentInChildren<ControlPointZone>(true), Is.Null, "the net client would count a zone as a control point");
            Assert.That(cart.GetComponentInChildren<Collider>(true), Is.Null, "the server collision would get a box that never moves");
            Assert.That(Components<ControlPointZone>(), Has.Length.EqualTo(ArenaSceneBuilder.ControlPoints.Count));
        }

        [Test]
        public void ThePayloadGoesFromInFrontOfAlphaToInFrontOfBravo()
        {
            var route = ArenaSceneBuilder.PayloadRoutePoints;
            Assert.That(route[0], Is.EqualTo(new Vector3(0f, ArenaSceneBuilder.GroundY, -18f)));
            Assert.That(route[route.Count - 1], Is.EqualTo(new Vector3(0f, ArenaSceneBuilder.GroundY, 18f)));
            Assert.That(route.All(p => Mathf.Approximately(p.y, ArenaSceneBuilder.GroundY)), Is.True, "the cart rolls on the ground");

            var payload = ServerRulesExporter.ToPayload(ArenaSceneBuilder.ScenePath, new[] { CartOnTheArenaRoute() }, PayloadRunner.DefaultAttackerSideId);
            Assert.That(payload.Attackers, Is.EqualTo(Team.Alpha));
            Assert.That(payload.Route.Length, Is.EqualTo(5d + 5d + 10d + 10d + 5d + 5d).Within(1e-4), "5 + 5 + 10 + 10 + 5 + 5 m");
            Assert.That(payload.Route.FractionAt(payload.Route.CheckpointDistance(0)), Is.EqualTo(0.5f).Within(1e-4f), "the checkpoint is half way");
        }

        // 台車の箱 (PayloadCart.DefaultBodySize) の長い方の半分を半径にして、道筋のどこでも壁・段差・建物に掛からないかを 0.25m ごとに確かめる
        [Test]
        public void ThePayloadRouteKeepsTheCartClearOfTheLevel()
        {
            var reach = Mathf.Max(PayloadCart.DefaultBodySize.x, PayloadCart.DefaultBodySize.z) / 2f;
            var route = ArenaSceneBuilder.PayloadRoutePoints;
            for (var i = 1; i < route.Count; i++)
            {
                var steps = Mathf.CeilToInt(Vector3.Distance(route[i - 1], route[i]) / 0.25f);
                for (var s = 0; s <= steps; s++)
                {
                    var at = Vector3.Lerp(route[i - 1], route[i], (float)s / steps);
                    foreach (var (name, center, size) in LevelBoxes())
                    {
                        Assert.That(HorizontalDistanceToBox(at, center, size), Is.GreaterThan(reach), "segment " + i + " at " + at + " hits " + name);
                    }

                    foreach (var (name, groundCenter) in ArenaSceneBuilder.Buildings)
                    {
                        var footprint = new Vector3(ArenaSceneBuilder.BuildingWidth, 0f, ArenaSceneBuilder.BuildingDepth) * ArenaSceneBuilder.PieceSize;
                        Assert.That(HorizontalDistanceToBox(at, groundCenter, footprint), Is.GreaterThan(reach), "segment " + i + " at " + at + " hits " + name);
                    }
                }
            }
        }

        [Test]
        public void PayloadRoutePointsAreCheckedOnTheNavMesh()
        {
            var points = ArenaSceneBuilder.NavMeshCheckPoints();

            foreach (var position in ArenaSceneBuilder.PayloadRoutePoints)
            {
                Assert.That(points, Has.Member(position));
            }
        }

        [Test]
        public void TheAttackersComeFromThePayloadRunnerOrTheRunnerDefault()
        {
            ArenaSceneBuilder.PopulateScene(_scene);
            Assert.That(ServerRulesExporter.FindAttackerSideId(_scene), Is.EqualTo(PayloadRunner.DefaultAttackerSideId), "the net scene has no runner");

            var host = new GameObject("Mode");
            SceneManager.MoveGameObjectToScene(host, _scene);
            var runner = host.AddComponent<PayloadRunner>();
            runner.AttackerSideId = 1;
            Assert.That(ServerRulesExporter.FindAttackerSideId(_scene), Is.EqualTo(1));

            var definition = ScriptableObject.CreateInstance<PayloadDefinition>();
            try
            {
                definition.AttackerSideId = 0;
                runner.Definition = definition;
                Assert.That(ServerRulesExporter.FindAttackerSideId(_scene), Is.EqualTo(0), "the definition asset wins over the runner field");
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private PayloadCart CartOnTheArenaRoute()
        {
            ArenaSceneBuilder.PopulateScene(_scene);
            return Components<PayloadCart>().Single();
        }

        private static float HorizontalDistanceToBox(Vector3 point, Vector3 center, Vector3 size)
        {
            var dx = Mathf.Max(0f, Mathf.Abs(point.x - center.x) - (size.x / 2f));
            var dz = Mathf.Max(0f, Mathf.Abs(point.z - center.z) - (size.z / 2f));
            return Mathf.Sqrt((dx * dx) + (dz * dz));
        }

        private static IEnumerable<(string Name, Vector3 Center, Vector3 Size)> LevelBoxes()
        {
            return ArenaSceneBuilder.HighWalls.Concat(ArenaSceneBuilder.LowWalls).Concat(ArenaSceneBuilder.Steps);
        }

        private static void AssertVaultable(string name, Vector3 size)
        {
            Assert.That(size.y, Is.GreaterThanOrEqualTo(MotorSpec.DefaultVaultMinHeight), name + " is not a mere step");
            Assert.That(size.y, Is.LessThanOrEqualTo(MotorSpec.DefaultVaultMaxHeight), name + " can be vaulted");
            Assert.That(Mathf.Min(size.x, size.z), Is.LessThanOrEqualTo(MotorSpec.DefaultVaultMaxDepth), name + " is thin enough to vault over");
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

        // 保存されていない Untitled があると Additive の NewScene は例外を投げるので、Shooter.Tests.EditMode の TestScenes.NewAdditive と同じく避ける。
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
