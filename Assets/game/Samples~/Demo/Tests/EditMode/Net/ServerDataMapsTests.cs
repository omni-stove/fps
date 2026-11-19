using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class ServerDataMapsTests
    {
        private const int _probeCount = 4500;

        private const string ScenePath = "Assets/Test.unity";

        private readonly List<Object> _created = new List<Object>();

        private Scene _scene;

        private static IEnumerable<string> MapScenes => BuildSceneList.ServerScenes;

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void BoxesAreWrittenWithTheirOwnIdTableAndReach()
        {
            var north = Box("Crate", "crate-north", "common", new Vector3(4f, 0f, 12.5f), 2f);
            var south = Box("Crate", "crate-south", string.Empty, new Vector3(-4f, 0.5f, -12.5f), LootContainer.DefaultReach);

            var written = ServerDataMaps.ToLootContainers(ScenePath, new[] { north, south });

            Assert.That(written, Has.Count.EqualTo(2));
            Assert.That(written[0].Id, Is.EqualTo("crate-north"), "the id comes from the component, not the object name");
            Assert.That(written[0].TableId, Is.EqualTo("common"));
            Assert.That(written[0].Position, Is.EqualTo(new System.Numerics.Vector3(4f, 0f, 12.5f)));
            Assert.That(written[0].Reach, Is.EqualTo(2f));
            Assert.That(written[1].Id, Is.EqualTo("crate-south"));
            Assert.That(written[1].TableId, Is.Empty);
            Assert.That(written[1].Position, Is.EqualTo(new System.Numerics.Vector3(-4f, 0.5f, -12.5f)));
            Assert.That(written[1].Reach, Is.EqualTo(LootContainer.DefaultReach));
        }

        [Test]
        public void ABoxWithoutAnIdOrWithAnotherBoxsIdIsRefused()
        {
            var unnamed = Box("Unnamed", string.Empty, "common", Vector3.zero, 1f);
            var first = Box("First", "crate", "common", Vector3.zero, 1f);
            var second = Box("Second", "crate", "common", Vector3.one, 1f);

            var missing = Assert.Throws<System.InvalidOperationException>(() => ServerDataMaps.ToLootContainers(ScenePath, new[] { unnamed }));
            var shared = Assert.Throws<System.InvalidOperationException>(() => ServerDataMaps.ToLootContainers(ScenePath, new[] { first, second }));

            Assert.That(missing.Message, Does.Contain("LootContainer 'Unnamed' in " + ScenePath + " has no container id"));
            Assert.That(shared.Message, Does.Contain("'First' and 'Second'").And.Contain("share the container id 'crate'"));
        }

        [Test]
        public void ExitsAreWrittenWithTheirPointIdCenterAndRadius()
        {
            var east = Exit("Exit", "exit-east", new Vector3(30f, 0f, 0f), 3f);
            var west = Exit("Exit", "exit-west", new Vector3(-30f, 0.25f, 0f), 4.5f);

            var written = ServerDataMaps.ToExtractionPoints(ScenePath, new[] { east, west });

            Assert.That(written, Has.Count.EqualTo(2));
            Assert.That(written[0].Id, Is.EqualTo("exit-east"));
            Assert.That(written[0].Center, Is.EqualTo(new System.Numerics.Vector3(30f, 0f, 0f)));
            Assert.That(written[0].Radius, Is.EqualTo(3f));
            Assert.That(written[1].Id, Is.EqualTo("exit-west"));
            Assert.That(written[1].Center, Is.EqualTo(new System.Numerics.Vector3(-30f, 0.25f, 0f)));
            Assert.That(written[1].Radius, Is.EqualTo(4.5f));
            Assert.Throws<System.InvalidOperationException>(
                () => ServerDataMaps.ToExtractionPoints(ScenePath, new[] { east, Exit("Copy", "exit-east", Vector3.zero, 3f) }));
            Assert.Throws<System.InvalidOperationException>(() => ServerDataMaps.ToExtractionPoints(ScenePath, new[] { Exit("Blank", " ", Vector3.zero, 3f) }));
        }

        [Test]
        public void TheZoneIsWrittenAsItsFirstCircle()
        {
            var zone = Zone(new Vector3(0f, 1f, 2f), 60f);

            var written = ServerDataMaps.ToZone(ScenePath, new[] { zone });

            Assert.That(written.Center, Is.EqualTo(new System.Numerics.Vector3(0f, 1f, 2f)));
            Assert.That(written.Radius, Is.EqualTo(60f));
            Assert.That(ServerDataMaps.ToZone(ScenePath, System.Array.Empty<ShrinkingZone>()), Is.Null);
            var two = Assert.Throws<System.InvalidOperationException>(() => ServerDataMaps.ToZone(ScenePath, new[] { zone, Zone(Vector3.zero, 30f) }));
            Assert.That(two.Message, Does.Contain("has 2 ShrinkingZones"));
        }

        [Test]
        public void TheDropHeightComesFromARunnerThatDropsByParachute()
        {
            var definition = ScriptableObject.CreateInstance<BattleRoyaleDefinition>();
            _created.Add(definition);
            definition.DropHeight = 75f;
            var dropping = Runner(BattleRoyaleStart.Parachute, definition);
            var walking = Runner(BattleRoyaleStart.Ground, definition);

            Assert.That(ServerDataMaps.ToDropHeight(ScenePath, new[] { walking, dropping }), Is.EqualTo(75f));
            Assert.That(ServerDataMaps.ToDropHeight(ScenePath, new[] { walking }), Is.Null);
            Assert.That(ServerDataMaps.ToDropHeight(ScenePath, System.Array.Empty<BattleRoyaleRunner>()), Is.Null);
        }

        [Test]
        public void TheWrittenPointsReadBackWithBoxesExitsAndTheZone()
        {
            var points = new NetMapPoints(
                new List<SpawnPoint>(),
                new List<ControlPointArea>(),
                lootContainers: ServerDataMaps.ToLootContainers(ScenePath, new[] { Box("Crate", "crate", "common", new Vector3(1f, 0f, 2f), 1.5f) }),
                extractionPoints: ServerDataMaps.ToExtractionPoints(ScenePath, new[] { Exit("Exit", "exit", new Vector3(9f, 0f, 9f), 3f) }),
                zone: ServerDataMaps.ToZone(ScenePath, new[] { Zone(Vector3.zero, 40f) }),
                dropHeight: 60f);

            var read = NetMapPoints.Parse(NetMapPoints.Write(points), "Test.points.json");

            Assert.That(read.LootContainers.Single().Id, Is.EqualTo("crate"));
            Assert.That(read.ExtractionPoints.Single().Id, Is.EqualTo("exit"));
            Assert.That(read.Zone.Radius, Is.EqualTo(40f));
            Assert.That(read.DropHeight, Is.EqualTo(60f));
        }

        // 今のネットのマップには箱・脱出地点・エリアを置いていないので、書き出したファイルにも無い
        [Test]
        public void TheCommittedNetMapsHaveNoBoxesExitsOrZone()
        {
            var files = Directory.GetFiles(MapCollisionExporter.OutputDirectory, "*" + NetMapPoints.Extension);
            Assert.That(files, Is.Not.Empty);
            foreach (var path in files)
            {
                var read = NetMapPoints.Parse(File.ReadAllText(path), path);

                Assert.That(read.LootContainers, Is.Empty, path);
                Assert.That(read.ExtractionPoints, Is.Empty, path);
                Assert.That(read.Zone, Is.Null, path);
                Assert.That(read.DropHeight, Is.Null, path);
            }
        }

        [Test]
        public void ServerDataHasOneFilePerMap()
        {
            var expected = BuildSceneList.ServerScenes.Select(s => MapCollisionExporter.MapName(s) + MapCollisionExporter.FileExtension);
            var actual = Directory.GetFiles(MapCollisionExporter.OutputDirectory, "*" + MapCollisionExporter.FileExtension).Select(Path.GetFileName);

            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [TestCaseSource(nameof(MapScenes))]
        public void CommittedMapMatchesTheScene(string scenePath)
        {
            var sources = OpenAndCollect(scenePath, out var fresh);
            var committed = MapCollisionExporter.ReadFile(MapFile(scenePath));

            Assert.That(Bytes(committed), Is.EqualTo(Bytes(fresh)), MapFile(scenePath) + " is out of date with " + scenePath);
            Assert.That(sources, Is.Not.Empty);
        }

        [TestCaseSource(nameof(MapScenes))]
        public void CommittedMapRaycastsLikePhysics(string scenePath)
        {
            var sources = OpenAndCollect(scenePath, out _);
            var world = new BvhWorldCollision(MapCollisionExporter.ReadFile(MapFile(scenePath)));
            var probes = MapCollisionCheck.SampleProbes(MapCollisionCheck.BoundsOf(sources), _probeCount, 583);

            var mismatches = MapCollisionCheck.CompareRaycasts(world, probes, Physics.DefaultRaycastLayers, sources);

            Assert.That(mismatches, Is.Empty, mismatches.Count + " of " + probes.Count + " rays differ:\n" + string.Join("\n", mismatches.Take(20)));
        }

        private List<Collider> OpenAndCollect(string scenePath, out WorldMap map)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Assert.Ignore(scenePath + " has not been generated; run make export-server-data to generate it and compare");
            }

            Assert.That(File.Exists(MapFile(scenePath)), Is.True, MapFile(scenePath) + " is missing");
            _scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var sources = new List<Collider>();
            map = MapCollisionExporter.Collect(_scene.GetRootGameObjects(), sources);
            return sources;
        }

        private static string MapFile(string scenePath)
        {
            return Path.Combine(MapCollisionExporter.OutputDirectory, MapCollisionExporter.MapName(scenePath) + MapCollisionExporter.FileExtension);
        }

        private LootContainer Box(string objectName, string id, string tableId, Vector3 position, float reach)
        {
            var box = Created(objectName, position).AddComponent<LootContainer>();
            box.ContainerId = id;
            box.TableId = tableId;
            box.Reach = reach;
            return box;
        }

        private ExtractionZone Exit(string objectName, string id, Vector3 position, float radius)
        {
            var exit = Created(objectName, position).AddComponent<ExtractionZone>();
            exit.PointId = id;
            exit.Radius = radius;
            return exit;
        }

        private ShrinkingZone Zone(Vector3 position, float radius)
        {
            var zone = Created("Zone", position).AddComponent<ShrinkingZone>();
            zone.BeginsOnStart = false;
            zone.InitialRadius = radius;
            return zone;
        }

        private BattleRoyaleRunner Runner(BattleRoyaleStart start, BattleRoyaleDefinition definition)
        {
            var runner = Created("Runner", Vector3.zero).AddComponent<BattleRoyaleRunner>();
            runner.Definition = definition;
            runner.StartFrom = start;
            return runner;
        }

        private GameObject Created(string objectName, Vector3 position)
        {
            var created = new GameObject(objectName);
            created.transform.position = position;
            _created.Add(created);
            return created;
        }

        private static byte[] Bytes(WorldMap map)
        {
            using (var stream = new MemoryStream())
            {
                ShooterMapFile.Write(stream, map);
                return stream.ToArray();
            }
        }
    }
}
