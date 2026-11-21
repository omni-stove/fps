using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LadderComponent = Shooter.Runtime.Traversal.Ladder;
using WaterVolume = Shooter.Runtime.Traversal.Water;

namespace Shooter.Net.Tests
{
    public sealed class NetMapFeatureBuilderTests
    {
        private const float Epsilon = 1e-4f;

        private static readonly float GroundY = NetSceneBuilder.GroundY;

        private Scene _scene;

        private static NetMapFeatureLayout Layout => new NetMapFeatureLayout(
            new[] { new NetMapLadderTower("Tower", new Vector3(-20f, GroundY, 15f), new Vector3(4f, 3f, 4f), NetMapFeatureLayout.East, 0.8f) },
            new[] { new NetMapWaterPool("Pond", new Vector3(20f, GroundY, 15f), new Vector3(8f, 3f, 8f), 2.5f) },
            new[]
            {
                new NetMapVehicleSpot("Car", NetMapFeatureBuilder.CarKind, new Vector3(-20f, GroundY, -15f), 0f),
                new NetMapVehicleSpot("Parked", NetMapFeatureBuilder.ParkedKind, new Vector3(20f, GroundY, -15f), 90f),
            });

        [SetUp]
        public void SetUp()
        {
            _scene = NetTestScenes.NewAdditive();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void TheLayoutOfEachMapIsKeptInAJsonNamedAfterTheMap()
        {
            Assert.That(NetMapFeatureBuilder.LayoutPath("NetArena01"), Does.EndWith("/Maps/NetArena01.features.json"));
            Assert.That(ArenaSceneBuilder.FeatureLayoutPath, Is.EqualTo(NetMapFeatureBuilder.LayoutPath(ArenaSceneBuilder.MapName)));
            Assert.That(NetSceneBuilder.FeatureLayoutPath, Is.EqualTo(NetMapFeatureBuilder.LayoutPath("NetTest")));
            Assert.That(NetSceneBuilder.BattleRoyaleFeatureLayoutPath, Is.EqualTo(NetMapFeatureBuilder.LayoutPath(NetSceneBuilder.BattleRoyaleMapName)));
            Assert.Throws<ArgumentException>(() => NetMapFeatureBuilder.LayoutPath(" "));
        }

        [Test]
        public void TheVehicleKindsAreTheCarAndTheParkedVehicle()
        {
            Assert.That(NetMapFeatureBuilder.VehicleKinds, Is.EqualTo(new[] { "car", "parked" }));
            Assert.That(NetMapFeatureBuilder.IsVehicleKind("car"), Is.True);
            Assert.That(NetMapFeatureBuilder.IsVehicleKind("Car"), Is.False, "kinds are ids, matched exactly");
            Assert.That(NetMapFeatureBuilder.IsVehicleKind(null), Is.False);
        }

        [Test]
        public void TheTerrainHasTheLadderTowerAndTheWaterPool()
        {
            var root = NetMapFeatureBuilder.PlaceTerrain(_scene, null, Layout);

            Assert.That(root.name, Is.EqualTo(NetMapFeatureBuilder.TerrainRootName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            var ladder = root.GetComponentsInChildren<LadderComponent>(true).Single();
            Assert.That(ladder.Height, Is.EqualTo(3f));
            Assert.That(ladder.Width, Is.EqualTo(0.8f));
            Assert.That(Vector3.Distance(ladder.transform.position, new Vector3(-18f, GroundY, 15f)), Is.LessThan(Epsilon), "on the east face");
            Assert.That(Vector3.Distance(ladder.transform.forward, Vector3.left), Is.LessThan(Epsilon), "facing into the tower");
            Assert.That(root.transform.Find("Tower/" + LadderTowerBuilder.PlatformName).GetComponent<BoxCollider>(), Is.Not.Null, "the platform can be stood on");

            var water = root.GetComponentsInChildren<WaterVolume>(true).Single();
            Assert.That(Vector3.Distance(water.transform.position, new Vector3(20f, GroundY, 15f)), Is.LessThan(Epsilon), "the bottom middle of the box");
            Assert.That(water.Size, Is.EqualTo(new Vector3(8f, 3f, 8f)));
            Assert.That(water.SurfaceHeight, Is.EqualTo(2.5f));
            var surface = root.transform.Find("Pond/" + NetMapFeatureBuilder.WaterSurfaceName);
            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.GetComponent<Collider>(), Is.Null, "people swim through the surface");
            Assert.That(root.GetComponentsInChildren<Vehicle>(true), Is.Empty, "vehicles come after the NavMesh is baked");
        }

        [Test]
        public void TheTerrainGoesUnderASurfaceThatOnlyCollectsItsChildren()
        {
            var level = new GameObject("Level");
            SceneManager.MoveGameObjectToScene(level, _scene);
            var children = SceneBuildHelpers.AddNavMeshSurface(level, CollectObjects.Children);
            var ground = new GameObject("Ground");
            SceneManager.MoveGameObjectToScene(ground, _scene);
            var everything = SceneBuildHelpers.AddNavMeshSurface(ground, CollectObjects.All);

            Assert.That(NetMapFeatureBuilder.TerrainParent(children), Is.EqualTo(level.transform));
            Assert.That(NetMapFeatureBuilder.TerrainParent(everything), Is.Null);

            var root = NetMapFeatureBuilder.PlaceTerrain(_scene, level.transform, Layout);
            Assert.That(root.transform.parent, Is.EqualTo(level.transform));
            Assert.That(level.GetComponentsInChildren<LadderComponent>(true), Has.Length.EqualTo(1));
        }

        [Test]
        public void TheVehiclesAreTheKindsInTheLayout()
        {
            var root = NetMapFeatureBuilder.PlaceVehicles(_scene, null, Layout);

            Assert.That(root.name, Is.EqualTo(NetMapFeatureBuilder.VehiclesRootName));
            var vehicles = root.GetComponentsInChildren<Vehicle>(true);
            Assert.That(vehicles.Select(v => v.name), Is.EqualTo(new[] { "Car", "Parked" }));
            Assert.That(vehicles[0].Spec.Id, Is.EqualTo(CarFixtureBuilder.VehicleId));
            Assert.That(vehicles[0].GetComponent<SimpleCarMotor>(), Is.Not.Null);
            Assert.That(Vector3.Distance(vehicles[0].transform.position, new Vector3(-20f, GroundY, -15f)), Is.LessThan(Epsilon));
            Assert.That(vehicles[1].Spec.Id, Is.EqualTo(VehicleFixtureBuilder.DefaultName));
            Assert.That(Quaternion.Angle(vehicles[1].transform.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void AnUnknownVehicleKindIsRefusedAndLeavesNothingBehind()
        {
            var layout = new NetMapFeatureLayout(
                Array.Empty<NetMapLadderTower>(),
                Array.Empty<NetMapWaterPool>(),
                new[] { new NetMapVehicleSpot("Tank", "tank", Vector3.zero, 0f) });

            var e = Assert.Throws<InvalidOperationException>(() => NetMapFeatureBuilder.PlaceVehicles(_scene, null, layout));

            Assert.That(e.Message, Does.Contain("Tank has the vehicle kind 'tank'; use one of car, parked"));
            Assert.That(_scene.GetRootGameObjects().Where(go => go.name == NetMapFeatureBuilder.VehiclesRootName), Is.Empty);
        }

        // サーバーへの書き出し (MapCollisionExporter・MapVehicleExporter) がシーンから読む数と位置
        [Test]
        public void ThePlacedFeaturesAreWhatTheServerDataExportersWrite()
        {
            NetMapFeatureBuilder.PlaceTerrain(_scene, null, Layout);
            NetMapFeatureBuilder.PlaceVehicles(_scene, null, Layout);
            var roots = _scene.GetRootGameObjects();

            var ladder = MapCollisionExporter.CollectLadders(roots).Ladders.Single();
            Assert.That(ladder.Span.Bottom, Is.EqualTo(new System.Numerics.Vector3(-18f, GroundY, 15f)).Using<System.Numerics.Vector3>(Near));
            Assert.That(ladder.Span.Facing, Is.EqualTo(new System.Numerics.Vector3(-1f, 0f, 0f)).Using<System.Numerics.Vector3>(Near));
            Assert.That(ladder.Span.Height, Is.EqualTo(3f));
            Assert.That(ladder.Span.Width, Is.EqualTo(0.8f));

            var water = MapCollisionExporter.CollectWater(roots).Water.Single();
            Assert.That(water.Box.Min, Is.EqualTo(new System.Numerics.Vector3(16f, GroundY, 11f)).Using<System.Numerics.Vector3>(Near));
            Assert.That(water.Box.Max, Is.EqualTo(new System.Numerics.Vector3(24f, GroundY + 3f, 19f)).Using<System.Numerics.Vector3>(Near));
            Assert.That(water.Box.SurfaceY, Is.EqualTo(GroundY + 2.5f).Within(Epsilon));

            var vehicles = MapVehicleExporter.Collect(roots).Vehicles;
            Assert.That(vehicles.Select(v => v.Spec.Id), Is.EqualTo(new[] { CarFixtureBuilder.VehicleId, VehicleFixtureBuilder.DefaultName }));
            Assert.That(vehicles[0].Position, Is.EqualTo(new System.Numerics.Vector3(-20f, GroundY, -15f)).Using<System.Numerics.Vector3>(Near));
            Assert.That(vehicles[0].Seats, Has.Count.EqualTo(2));
            Assert.That(vehicles[0].Hull, Is.Not.Empty);

            var map = MapCollisionExporter.Collect(roots);
            Assert.That(map.Colliders, Has.Count.EqualTo(1), "only the tower platform is fixed; vehicles move and the water surface has no collider");
        }

        [Test]
        public void HasFeaturesSeesALadderWaterOrVehicle()
        {
            Assert.That(NetMapFeatureBuilder.HasFeatures(_scene), Is.False);

            var water = new GameObject("Water").AddComponent<WaterVolume>();
            SceneManager.MoveGameObjectToScene(water.gameObject, _scene);
            Assert.That(NetMapFeatureBuilder.HasFeatures(_scene), Is.True);

            UnityEngine.Object.DestroyImmediate(water.gameObject);
            NetMapFeatureBuilder.PlaceVehicles(_scene, null, Layout);
            Assert.That(NetMapFeatureBuilder.HasFeatures(_scene), Is.True);
        }

        [Test]
        public void AFittingLayoutHasNoProblems()
        {
            var problems = NetMapFeatureBuilder.Problems(Layout, 30f, Array.Empty<(string, Vector3, Vector3)>(), Array.Empty<(string, Vector3, float)>());

            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void ProblemsNameTheFloorWallsKeptPointsOverlapsAndSharedNames()
        {
            var layout = new NetMapFeatureLayout(
                new[]
                {
                    new NetMapLadderTower("Edge", new Vector3(28f, GroundY, 0f), new Vector3(4f, 3f, 4f), NetMapFeatureLayout.West, 0.8f),
                    new NetMapLadderTower("Twin", new Vector3(-10f, GroundY, 10f), new Vector3(4f, 3f, 4f), NetMapFeatureLayout.North, 0.8f),
                },
                new[] { new NetMapWaterPool("Twin", new Vector3(-10f, GroundY, 12f), new Vector3(4f, 3f, 4f), 2f) },
                new[]
                {
                    new NetMapVehicleSpot("Blocked", NetMapFeatureBuilder.CarKind, new Vector3(0f, GroundY, -10f), 0f),
                    new NetMapVehicleSpot("Hover", "hovercraft", Vector3.zero, 0f),
                });
            var walls = new[] { ("Wall", new Vector3(0f, GroundY + 1f, -10f), new Vector3(4f, 2f, 0.5f)) };
            var keep = new[] { ("Spawn", new Vector3(0f, GroundY, -14f), 2f) };

            var problems = NetMapFeatureBuilder.Problems(layout, 30f, walls, keep);

            Assert.That(problems, Has.Member("Edge reaches past the floor (keep it 1 m inside 30 m)"));
            Assert.That(problems, Has.Member("the name 'Twin' is used 2 times"));
            Assert.That(problems, Has.Member("Twin overlaps Twin"));
            Assert.That(problems, Has.Member("Blocked overlaps Wall"));
            Assert.That(problems, Has.Member("Blocked covers Spawn"));
            Assert.That(problems, Has.Member("Hover has the vehicle kind 'hovercraft'; use one of car, parked"));
            Assert.That(problems, Has.Count.EqualTo(6));
        }

        [Test]
        public void TheTowerFootprintIncludesWhereYouStandToGrabTheLadder()
        {
            var tower = new NetMapLadderTower("Tower", new Vector3(0f, 0f, 0f), new Vector3(4f, 3f, 4f), NetMapFeatureLayout.South, 0.8f);

            var footprint = NetMapFeatureBuilder.Footprint(tower);

            Assert.That(footprint.min.z, Is.EqualTo(-2f - NetMapFeatureBuilder.LadderApproachDepth).Within(Epsilon));
            Assert.That(footprint.max.z, Is.EqualTo(2f).Within(Epsilon));
            Assert.That(footprint.min.x, Is.EqualTo(-2f).Within(Epsilon));
        }

        [Test]
        public void TheVehicleFootprintLeavesRoomToGetOut()
        {
            var car = new NetMapVehicleSpot("Car", NetMapFeatureBuilder.CarKind, Vector3.zero, 0f);

            var footprint = NetMapFeatureBuilder.Footprint(car);

            var body = CarFixtureBuilder.BodyBounds(Vector3.zero, Quaternion.identity);
            Assert.That(footprint.size.x, Is.EqualTo(body.size.x + (2f * NetMapFeatureBuilder.VehicleClearance)).Within(Epsilon));
            Assert.That(footprint.size.z, Is.EqualTo(body.size.z + (2f * NetMapFeatureBuilder.VehicleClearance)).Within(Epsilon));
            Assert.That(footprint.max.x, Is.GreaterThanOrEqualTo(CarFixtureBuilder.RightExitLocal.x - Epsilon), "the side exit stays inside");
        }

        [Test]
        public void TheNetTestDefaultsFitTheMap()
        {
            var defaults = NetSceneBuilder.DefaultFeatures;

            Assert.That(NetSceneBuilder.FeatureProblems(defaults, CaptureTheFlagSpecRadius()), Is.Empty);
            Assert.That(defaults.LadderTowers, Has.Count.EqualTo(1));
            Assert.That(defaults.WaterPools, Has.Count.EqualTo(1));
            Assert.That(defaults.Vehicles, Has.Count.EqualTo(1));
            Assert.That(NetMapFeatureLayout.Parse(defaults.ToJson(), "NetTest.features.json").Count, Is.EqualTo(3));
        }

        [Test]
        public void TheBattleRoyaleDefaultsFitTheMap()
        {
            var defaults = NetSceneBuilder.BattleRoyaleDefaultFeatures;

            Assert.That(NetSceneBuilder.BattleRoyaleFeatureProblems(defaults), Is.Empty);
            Assert.That(defaults.LadderTowers, Has.Count.EqualTo(1));
            Assert.That(defaults.WaterPools, Has.Count.EqualTo(1));
            Assert.That(defaults.Vehicles, Has.Count.EqualTo(2));
            foreach (var vehicle in defaults.Vehicles)
            {
                var flat = new Vector2(vehicle.Position.x, vehicle.Position.z);
                Assert.That(flat.magnitude, Is.LessThan(NetSceneBuilder.BattleRoyaleZoneRadius), vehicle.Name + " starts inside the zone");
            }
        }

        [Test]
        public void TheArenaDefaultsFitTheMapAndTheTowersAreMirrored()
        {
            var defaults = ArenaSceneBuilder.DefaultFeatures;

            Assert.That(ArenaSceneBuilder.FeatureProblems(defaults, CaptureTheFlagSpecRadius()), Is.Empty);
            Assert.That(defaults.LadderTowers, Has.Count.EqualTo(2));
            Assert.That(defaults.WaterPools, Has.Count.EqualTo(1));
            Assert.That(defaults.Vehicles, Has.Count.EqualTo(1));
            Assert.That(defaults.LadderTowers[1].BaseCenter, Is.EqualTo(ArenaSceneBuilder.MirrorZ(defaults.LadderTowers[0].BaseCenter)));
        }

        [Test]
        public void ATowerOnTheArenaPayloadRouteIsRefused()
        {
            var layout = new NetMapFeatureLayout(
                new[] { new NetMapLadderTower("Blocker", new Vector3(4f, GroundY, 0f), new Vector3(2f, 3f, 2f), NetMapFeatureLayout.East, 0.8f) },
                Array.Empty<NetMapWaterPool>(),
                Array.Empty<NetMapVehicleSpot>());

            var problems = ArenaSceneBuilder.FeatureProblems(layout, CaptureTheFlagSpecRadius());

            Assert.That(problems, Has.Member("Blocker covers payload route point 3"));
        }

        [Test]
        public void ATowerBetweenTwoPayloadRoutePointsIsRefused()
        {
            var points = ArenaSceneBuilder.PayloadRoutePoints;
            var middle = (points[0] + points[1]) * 0.5f;
            var layout = new NetMapFeatureLayout(
                new[] { new NetMapLadderTower("Blocker", new Vector3(middle.x, GroundY, middle.z), new Vector3(2f, 3f, 2f), NetMapFeatureLayout.East, 0.8f) },
                Array.Empty<NetMapWaterPool>(),
                Array.Empty<NetMapVehicleSpot>());

            var problems = ArenaSceneBuilder.FeatureProblems(layout, CaptureTheFlagSpecRadius());

            Assert.That(problems, Has.Member("Blocker covers payload route segment 0"));
        }

        [Test]
        public void TheLadderFootIsInFrontOfTheLadder()
        {
            var points = NetSceneBuilder.LadderApproachPoints(Layout);

            Assert.That(points, Has.Count.EqualTo(1));
            Assert.That(Vector3.Distance(points[0], new Vector3(-18f + NetMapFeatureBuilder.LadderApproachDepth, GroundY, 15f)), Is.LessThan(Epsilon));
        }

        private static float CaptureTheFlagSpecRadius()
        {
            return Shooter.Core.CaptureTheFlagSpec.DefaultCaptureRadius;
        }

        private static bool Near(System.Numerics.Vector3 a, System.Numerics.Vector3 b)
        {
            return System.Numerics.Vector3.Distance(a, b) < Epsilon;
        }
    }
}
