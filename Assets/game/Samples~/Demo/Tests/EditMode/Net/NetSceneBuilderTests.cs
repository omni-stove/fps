using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Net.Tests
{
    public sealed class NetSceneBuilderTests
    {
        private Scene _scene;

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
        public void TheBattleRoyaleMapIsNamedAfterItsSceneAndExportedForTheServer()
        {
            Assert.That(Path.GetFileNameWithoutExtension(NetSceneBuilder.BattleRoyaleScenePath), Is.EqualTo(NetSceneBuilder.BattleRoyaleMapName),
                "-map finds the scene by its file name");
            Assert.That(NetSceneBuilder.BattleRoyaleMapName, Is.EqualTo("NetBattleRoyale"));
            Assert.That(NetLaunchOptions.IsMapName(NetSceneBuilder.BattleRoyaleMapName), Is.True);
            Assert.That(NetSceneBuilder.BattleRoyaleScenePath, Is.EqualTo(BuildSceneList.NetBattleRoyaleScene));
            Assert.That(BuildSceneList.Scenes, Has.Member(NetSceneBuilder.BattleRoyaleScenePath), "clients load the map the server chose");
            Assert.That(BuildSceneList.ServerScenes, Has.Member(NetSceneBuilder.BattleRoyaleScenePath), "the server data is exported for it");
            Assert.That(BuildSceneList.ServerScenes[0], Is.EqualTo(NetSceneBuilder.ScenePath), "NetTest stays the default map");
        }

        [Test]
        public void TheLayoutHasNoSharedIdsAndKeepsEveryPointOnTheFloorAndOutOfTheWalls()
        {
            Assert.That(NetSceneBuilder.BattleRoyaleLayoutProblems(), Is.Empty);
            Assert.That(NetSceneBuilder.BattleRoyaleLootContainers.Select(c => c.Id), Is.Unique);
            Assert.That(NetSceneBuilder.BattleRoyaleExtractionPoints.Select(p => p.Id), Is.Unique);
            Assert.That(NetSceneBuilder.BattleRoyaleSpawnPoints.Select(p => p.Name), Is.Unique);
            Assert.That(NetSceneBuilder.BattleRoyaleLootContainers.Count(), Is.EqualTo(12));
            Assert.That(NetSceneBuilder.BattleRoyaleExtractionPoints.Count(), Is.EqualTo(4));
            Assert.That(NetSceneBuilder.BattleRoyaleSpawnPoints.Count(), Is.EqualTo(8));
            foreach (var (id, position, _) in NetSceneBuilder.BattleRoyaleLootContainers)
            {
                Assert.That(position.y, Is.EqualTo(NetSceneBuilder.GroundY), id + " stands on the floor");
            }

            foreach (var (id, position) in NetSceneBuilder.BattleRoyaleExtractionPoints)
            {
                Assert.That(position.y, Is.EqualTo(NetSceneBuilder.GroundY), id + " lies on the floor");
            }
        }

        [Test]
        public void TheZoneStartsAroundEverySpawnAndLeavesTheCornersOut()
        {
            var center = new Vector2(NetSceneBuilder.BattleRoyaleZoneCenter.x, NetSceneBuilder.BattleRoyaleZoneCenter.z);
            foreach (var (name, position) in NetSceneBuilder.BattleRoyaleSpawnPoints)
            {
                Assert.That(Vector2.Distance(center, new Vector2(position.x, position.z)), Is.LessThan(NetSceneBuilder.BattleRoyaleZoneRadius), name);
            }

            var corner = new Vector2(NetSceneBuilder.BattleRoyaleHalfExtent, NetSceneBuilder.BattleRoyaleHalfExtent);
            Assert.That(corner.magnitude, Is.GreaterThan(NetSceneBuilder.BattleRoyaleZoneRadius), "players are pushed out of the corners");
        }

        [Test]
        public void EveryBoxDrawsFromATableOfTheDemoLoot()
        {
            var loot = LootJson.Parse(ExtractionSceneBuilder.LootJsonText(), "ExtractionLoot.json");

            foreach (var (id, _, tableId) in NetSceneBuilder.BattleRoyaleLootContainers)
            {
                Assert.That(loot.Find(tableId), Is.Not.Null, id + " draws from " + tableId);
            }
        }

        [Test]
        public void ThePopulatedSceneHasTheBoxesExitsZoneAndSpawnsWithTheirIds()
        {
            NetSceneBuilder.PopulateBattleRoyale(_scene);

            var boxes = Components<LootContainer>();
            var exits = Components<ExtractionZone>();
            var zones = Components<ShrinkingZone>();
            var spawns = Components<NetSpawnPoint>();

            Assert.That(boxes.Select(b => b.ContainerId), Is.EqualTo(NetSceneBuilder.BattleRoyaleLootContainers.Select(c => c.Id)));
            Assert.That(boxes.Select(b => b.TableId), Is.EqualTo(NetSceneBuilder.BattleRoyaleLootContainers.Select(c => c.TableId)));
            Assert.That(exits.Select(e => e.PointId), Is.EqualTo(NetSceneBuilder.BattleRoyaleExtractionPoints.Select(p => p.Id)));
            Assert.That(zones, Has.Count.EqualTo(1));
            Assert.That(zones[0].InitialRadius, Is.EqualTo(NetSceneBuilder.BattleRoyaleZoneRadius));
            Assert.That(zones[0].BeginsOnStart, Is.False, "the server shrinks the zone");
            Assert.That(spawns, Has.Count.EqualTo(NetSceneBuilder.BattleRoyaleSpawnPoints.Count));
            Assert.That(spawns.All(s => s.Team == NetSpawnPoint.SpawnTeam.Any), Is.True, "the objective picks the squads");
        }

        [Test]
        public void TheSceneIsWrittenToTheServerWithBoxesExitsAndTheZone()
        {
            NetSceneBuilder.PopulateBattleRoyale(_scene);
            var path = NetSceneBuilder.BattleRoyaleScenePath;

            var boxes = ServerDataMaps.ToLootContainers(path, Components<LootContainer>());
            var exits = ServerDataMaps.ToExtractionPoints(path, Components<ExtractionZone>());
            var zone = ServerDataMaps.ToZone(path, Components<ShrinkingZone>());
            var points = new NetMapPoints(new List<SpawnPoint>(), new List<ControlPointArea>(), lootContainers: boxes, extractionPoints: exits, zone: zone);
            var read = NetMapPoints.Parse(NetMapPoints.Write(points), NetSceneBuilder.BattleRoyaleMapName + NetMapPoints.Extension);

            Assert.That(read.LootContainers, Has.Count.EqualTo(12));
            Assert.That(read.LootContainers[0].Id, Is.EqualTo("net-br-box-01"));
            Assert.That(read.LootContainers[0].TableId, Is.EqualTo(ExtractionSceneBuilder.SuppliesTable));
            Assert.That(read.LootContainers[0].Position, Is.EqualTo(new System.Numerics.Vector3(0f, -1f, -30f)));
            Assert.That(read.LootContainers[0].Reach, Is.EqualTo(LootContainer.DefaultReach));
            Assert.That(read.LootContainers[8].TableId, Is.EqualTo(ExtractionSceneBuilder.ValuablesTable));
            Assert.That(read.ExtractionPoints, Has.Count.EqualTo(4));
            Assert.That(read.ExtractionPoints[0].Id, Is.EqualTo("net-br-exit-north"));
            Assert.That(read.ExtractionPoints[0].Center, Is.EqualTo(new System.Numerics.Vector3(0f, -1f, 70f)));
            Assert.That(read.ExtractionPoints[0].Radius, Is.EqualTo(ExtractionZone.DefaultRadius));
            Assert.That(read.Zone.Center, Is.EqualTo(new System.Numerics.Vector3(0f, -1f, 0f)));
            Assert.That(read.Zone.Radius, Is.EqualTo(75f));
            Assert.That(read.DropHeight, Is.Null, "no offline runner is placed in the net map");
        }

        // クライアントは箱と脱出地点をスナップショットから出すので、シーンの印が Awake で形と当たりを作ると二重になり、サーバーの知らない当たりもできる
        [Test]
        public void TheMarkersStayShapelessWhenTheyBuildTheirLook()
        {
            NetSceneBuilder.PopulateBattleRoyale(_scene);

            foreach (var box in Components<LootContainer>())
            {
                box.Build();
                Assert.That(box.GetComponentsInChildren<Collider>(true), Is.Empty, box.ContainerId);
                Assert.That(box.GetComponentsInChildren<Renderer>(true), Is.Empty, box.ContainerId);
            }

            foreach (var exit in Components<ExtractionZone>())
            {
                exit.Build();
                Assert.That(exit.GetComponentsInChildren<Collider>(true), Is.Empty, exit.PointId);
                Assert.That(exit.GetComponentsInChildren<Renderer>(true), Is.Empty, exit.PointId);
            }
        }

        [Test]
        public void TheLevelHasTheFloorAndTheCoverWalls()
        {
            var ground = NetSceneBuilder.CreateBattleRoyaleLevel(_scene);

            Assert.That(ground.scene, Is.EqualTo(_scene));
            Assert.That(ground.transform.position.y, Is.EqualTo(NetSceneBuilder.GroundY));
            Assert.That(ground.transform.localScale, Is.EqualTo(NetSceneBuilder.BattleRoyaleGroundScale));
            var cover = _scene.GetRootGameObjects().Single(root => root.name == NetSceneBuilder.BattleRoyaleCoverRootName);
            Assert.That(cover.transform.childCount, Is.EqualTo(NetSceneBuilder.BattleRoyaleCoverWalls.Count));
        }

        [Test]
        public void AMarkerNeedsAnId()
        {
            var parent = new GameObject("Parent");
            SceneManager.MoveGameObjectToScene(parent, _scene);

            Assert.Throws<System.ArgumentException>(() => NetSceneBuilder.CreateLootContainerMarker(parent.transform, " ", Vector3.zero, "table"));
            Assert.Throws<System.ArgumentException>(() => NetSceneBuilder.CreateExtractionPointMarker(parent.transform, string.Empty, Vector3.zero));
            Assert.Throws<System.ArgumentNullException>(() => NetSceneBuilder.CreateLootContainerMarker(null, "box", Vector3.zero, "table"));
        }

        private List<T> Components<T>()
            where T : Component
        {
            var found = new List<T>();
            foreach (var root in _scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(false));
            }

            return found;
        }

    }
}
