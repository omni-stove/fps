using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class CampaignSceneBuilderTests
    {
        private MissionDefinition _definition;
        private Scene _scene;
        private NavMeshData _baked;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<MissionDefinition>();
            _definition.name = "Campaign01";
            CampaignSceneBuilder.Configure(_definition);
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            // PopulateScene は焼いた NavMesh をアセットにしないので、テストで作った分はここで捨てる。
            if (_baked != null && !AssetDatabase.Contains(_baked))
            {
                Object.DestroyImmediate(_baked);
            }

            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void MissionIsReachAreaAThenKillFourEnemiesThenReachExit()
        {
            var mission = _definition.CreateMission();

            Assert.That(_definition.DisplayName, Is.EqualTo(CampaignSceneBuilder.MissionDisplayName));
            Assert.That(mission.Objectives, Has.Count.EqualTo(3));
            Assert.That(mission.Objectives[0].Id, Is.EqualTo(CampaignSceneBuilder.ReachAreaAId));
            Assert.That(mission.Objectives[0].Prerequisites, Is.Empty);
            Assert.That(mission.Objectives[1].Id, Is.EqualTo(CampaignSceneBuilder.KillEnemiesId));
            Assert.That(mission.Objectives[1].RequiredCount, Is.EqualTo(4));
            Assert.That(mission.Objectives[1].Prerequisites, Is.EqualTo(new[] { CampaignSceneBuilder.ReachAreaAId }));
            Assert.That(mission.Objectives[2].Id, Is.EqualTo(CampaignSceneBuilder.ReachExitId));
            Assert.That(mission.Objectives[2].Prerequisites, Is.EqualTo(new[] { CampaignSceneBuilder.KillEnemiesId }));
        }

        [Test]
        public void PopulatedSceneIsWiredInCode()
        {
            var layout = Populate();

            Assert.That(_scene.GetRootGameObjects(), Has.Length.EqualTo(6));

            Assert.That(layout.Bootstrap, Is.Not.Null);
            Assert.That(layout.Bootstrap.gameObject.scene, Is.EqualTo(_scene));
            Assert.That(layout.Bootstrap.CreatesFloor, Is.False, "the scene's own floor carries the baked NavMesh");

            Assert.That(layout.Runner.Definition, Is.SameAs(_definition));
            Assert.That(layout.Runner.gameObject.scene, Is.EqualTo(_scene));

            Assert.That(layout.AreaA.ObjectiveId, Is.EqualTo(CampaignSceneBuilder.ReachAreaAId));
            Assert.That(layout.AreaA.Runner, Is.SameAs(layout.Runner));
            Assert.That(layout.AreaA.GetComponent<BoxCollider>().isTrigger, Is.True);
            Assert.That(layout.AreaA.GetComponent<Rigidbody>().isKinematic, Is.True);

            Assert.That(layout.KillEnemies.ObjectiveId, Is.EqualTo(CampaignSceneBuilder.KillEnemiesId));
            Assert.That(layout.KillEnemies.Runner, Is.SameAs(layout.Runner));

            Assert.That(layout.Exit.ObjectiveId, Is.EqualTo(CampaignSceneBuilder.ReachExitId));
            Assert.That(layout.Exit.Runner, Is.SameAs(layout.Runner));
            Assert.That(layout.Exit.GetComponent<BoxCollider>().isTrigger, Is.True);
        }

        [Test]
        public void FourEnemiesTwoPatrolAndTwoWaitInAmbush()
        {
            var layout = Populate();

            Assert.That(layout.Enemies, Has.Count.EqualTo(CampaignSceneBuilder.EnemyCount));
            Assert.That(CampaignSceneBuilder.Patrollers, Has.Count.EqualTo(2));
            Assert.That(CampaignSceneBuilder.Ambushers, Has.Count.EqualTo(2));

            var placements = CampaignSceneBuilder.AllEnemies();
            var patrolling = 0;
            for (var i = 0; i < layout.Enemies.Count; i++)
            {
                var agent = layout.Enemies[i];
                var placement = placements[i];

                Assert.That(agent.name, Is.EqualTo(placement.Name));
                Assert.That(agent.Id, Is.EqualTo(placement.Id));
                Assert.That(agent.gameObject.scene, Is.EqualTo(_scene));
                Assert.That(agent.transform.parent, Is.SameAs(layout.KillEnemies.transform));
                Assert.That(agent.PatrolRoute, Is.EqualTo(placement.PatrolRoute));
                patrolling += agent.PatrolRoute.Count > 0 ? 1 : 0;

                var navAgent = agent.GetComponent<NavMeshAgent>();
                Assert.That(navAgent.baseOffset, Is.EqualTo(CampaignSceneBuilder.EnemyHeight / 2f));
                var feet = agent.transform.position - (Vector3.up * navAgent.baseOffset);
                Assert.That(Vector3.Distance(feet, placement.Position), Is.LessThan(1e-4f));

                var damageable = agent.GetComponent<Damageable>();
                Assert.That(damageable.Id, Is.EqualTo(placement.Id));
                Assert.That(damageable.MaxHealth, Is.EqualTo(CampaignSceneBuilder.EnemyMaxHealth));
                Assert.That(agent.GetComponent<Hitbox>().Owner, Is.SameAs(damageable));
                Assert.That(agent.GetComponent<CapsuleCollider>().isTrigger, Is.False, "enemies must be solid to be shot");
            }

            Assert.That(patrolling, Is.EqualTo(2));
            foreach (var ambusher in CampaignSceneBuilder.Ambushers)
            {
                Assert.That(ambusher.Patrols, Is.False);
                Assert.That(ambusher.Position.z, Is.GreaterThan(CampaignSceneBuilder.DividerZ), "ambushers wait past area A");
            }
        }

        [Test]
        public void NavMeshIsBakedAndReachesEveryPointFromTheStart()
        {
            var layout = Populate();

            Assert.That(layout.Surface, Is.Not.Null);
            Assert.That(layout.Surface.navMeshData, Is.Not.Null);
            Assert.That(layout.Surface.collectObjects, Is.EqualTo(CollectObjects.Children));
            Assert.That(layout.Surface.useGeometry, Is.EqualTo(NavMeshCollectGeometry.PhysicsColliders));

            Assert.That(
                NavMesh.SamplePosition(CampaignSceneBuilder.StartCheckpointPosition, out var start, 1f, NavMesh.AllAreas),
                Is.True);
            var path = new NavMeshPath();
            foreach (var point in CampaignSceneBuilder.RequiredPoints())
            {
                Assert.That(NavMesh.SamplePosition(point, out var end, 1f, NavMesh.AllAreas), Is.True, $"{point} is on the NavMesh");
                Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), $"{point} is reachable from the start");
            }
        }

        [Test]
        public void CheckpointsAreTheStartAndInsideAreaA()
        {
            var layout = Populate();

            var playerFeet = GameBootstrap.PlayerSpawn - (Vector3.up * (GameBootstrap.PlayerHeight / 2f));
            Assert.That(CampaignSceneBuilder.StartCheckpointPosition, Is.EqualTo(playerFeet));

            var area = layout.AreaA.GetComponent<BoxCollider>();
            Assert.That(IsInside(area, CampaignSceneBuilder.AreaACheckpointPosition + Vector3.up), Is.True);
            Assert.That(IsInside(area, CampaignSceneBuilder.StartCheckpointPosition + Vector3.up), Is.False);
        }

        [Test]
        public void AreaAFillsTheWidthInFrontOfTheOnlyDoorway()
        {
            var layout = Populate();
            var area = layout.AreaA.GetComponent<BoxCollider>();

            // 仕切りの手前を幅いっぱいに覆うので、仕切りを抜けるには必ず区域 A を通る。
            var halfWidth = CampaignSceneBuilder.FloorScale.x * 5f;
            foreach (var x in new[] { -halfWidth + 1f, 0f, halfWidth - 1f })
            {
                var point = new Vector3(x, 1f, CampaignSceneBuilder.DividerZ - 1f);
                Assert.That(IsInside(area, point), Is.True, $"{point} is in area A");
            }

            // Edit Mode では物理の同期を待たずに確かめたいので、Collider.bounds ではなく Transform から計算する。
            var transform = area.transform;
            var bottom = transform.TransformPoint(area.center - new Vector3(0f, area.size.y / 2f, 0f)).y;
            var top = transform.TransformPoint(area.center + new Vector3(0f, area.size.y / 2f, 0f)).y;
            Assert.That(bottom, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(top, Is.EqualTo(CampaignSceneBuilder.AreaTriggerHeight).Within(1e-3f));
            Assert.That(top - bottom, Is.GreaterThan(GameBootstrap.PlayerHeight));
        }

        [Test]
        public void EnemiesCannotBeSeenBeforeAreaA()
        {
            // KillTargetObjective は目標が Active になる前の撃破を数えないので、区域 A の手前から敵が見えてはいけない。
            var surface = CampaignSceneBuilder.CreateLevel(_scene);
            Physics.SyncTransforms();

            var eyeHeight = GameBootstrap.PlayerSpawn.y + GameBootstrap.EyeOffset.y;
            var areaStart = CampaignSceneBuilder.AreaAPosition.z - (CampaignSceneBuilder.AreaAPadScale.z / 2f);
            foreach (var target in CampaignSceneBuilder.RequiredPoints())
            {
                if (target.z <= CampaignSceneBuilder.DividerZ)
                {
                    continue;
                }

                for (var x = -14f; x <= 14f; x += 2f)
                {
                    for (var z = -9f; z < areaStart; z += 3f)
                    {
                        var eye = new Vector3(x, eyeHeight, z);
                        var point = target + Vector3.up;
                        var toPoint = point - eye;
                        var blocked = Physics.Raycast(
                            eye,
                            toPoint.normalized,
                            out var hit,
                            toPoint.magnitude,
                            Physics.DefaultRaycastLayers,
                            QueryTriggerInteraction.Ignore);
                        Assert.That(blocked, Is.True, $"{target} is visible from {eye}");
                        Assert.That(hit.transform.IsChildOf(surface.transform), Is.True, $"{hit.collider.name} is part of the level");
                    }
                }
            }
        }

        [Test]
        public void GeneratedLayoutCanBeCompletedInOrder()
        {
            var layout = Populate();
            var player = new GameObject("Player");
            try
            {
                player.tag = GameBootstrap.PlayerTag;
                var collider = player.AddComponent<CapsuleCollider>();

                Assert.That(layout.Runner.TryStart(), Is.True);
                Assert.That(layout.AreaA.Bind(layout.Runner), Is.True);
                Assert.That(layout.KillEnemies.Bind(layout.Runner), Is.True);
                Assert.That(layout.Exit.Bind(layout.Runner), Is.True);
                Assert.That(layout.KillEnemies.BoundTargets, Has.Count.EqualTo(CampaignSceneBuilder.EnemyCount));
                var mission = layout.Runner.Mission;

                layout.AreaA.HandleEnter(collider);
                Assert.That(mission.GetState(CampaignSceneBuilder.ReachAreaAId), Is.EqualTo(ObjectiveState.Completed));
                Assert.That(mission.GetState(CampaignSceneBuilder.KillEnemiesId), Is.EqualTo(ObjectiveState.Active));

                layout.Exit.HandleEnter(collider);
                layout.Exit.HandleExit(collider);
                Assert.That(mission.GetState(CampaignSceneBuilder.ReachExitId), Is.EqualTo(ObjectiveState.Locked));

                foreach (var enemy in layout.Enemies)
                {
                    enemy.GetComponent<Damageable>().Kill();
                }

                Assert.That(mission.GetState(CampaignSceneBuilder.KillEnemiesId), Is.EqualTo(ObjectiveState.Completed));

                LogAssert.Expect(LogType.Log, MissionReporter.CompletedLine);
                layout.Exit.HandleEnter(collider);
                Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RefusesToPopulateANonEmptyScene()
        {
            SceneManager.MoveGameObjectToScene(new GameObject("Stray"), _scene);

            Assert.Throws<InvalidOperationException>(() => CampaignSceneBuilder.PopulateScene(_scene, _definition));
            Assert.That(_scene.rootCount, Is.EqualTo(1), "nothing is left behind");
        }

        [Test]
        public void RefusesToPopulateWithoutDefinition()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignSceneBuilder.PopulateScene(_scene, null));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        [Test]
        public void CommittedAssetsMatchTheBuilder()
        {
            var definition = AssetDatabase.LoadAssetAtPath<MissionDefinition>(CampaignSceneBuilder.DefinitionPath);
            Assume.That(definition, Is.Not.Null, $"run {nameof(CampaignSceneBuilder)}.Build first");
            Assume.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(CampaignSceneBuilder.ScenePath),
                Is.Not.Null,
                $"run {nameof(CampaignSceneBuilder)}.Build first");

            Assert.That(definition.CreateMission().Objectives, Has.Count.EqualTo(3));

            // テストの前に開いていた追加シーンは使わず、保存済みのシーンを開いて確かめる。
            EditorSceneManager.CloseScene(_scene, true);
            _scene = EditorSceneManager.OpenScene(CampaignSceneBuilder.ScenePath, OpenSceneMode.Additive);

            MissionRunner runner = null;
            NavMeshSurface surface = null;
            var triggers = 0;
            var enemies = 0;
            foreach (var root in _scene.GetRootGameObjects())
            {
                runner = runner != null ? runner : root.GetComponent<MissionRunner>();
                surface = surface != null ? surface : root.GetComponent<NavMeshSurface>();
                triggers += root.GetComponents<ReachAreaTrigger>().Length;
                enemies += root.GetComponentsInChildren<EnemyAgent>(true).Length;
            }

            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.Definition, Is.SameAs(definition));
            Assert.That(triggers, Is.EqualTo(2));
            Assert.That(enemies, Is.EqualTo(CampaignSceneBuilder.EnemyCount));
            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.navMeshData, Is.Not.Null, "the NavMesh must be baked and committed");
            Assert.That(AssetDatabase.GetAssetPath(surface.navMeshData), Is.EqualTo(CampaignSceneBuilder.NavMeshDataPath));
        }

        private CampaignLayout Populate()
        {
            var layout = CampaignSceneBuilder.PopulateScene(_scene, _definition);
            _baked = layout.Surface.navMeshData;
            return layout;
        }

        /// <summary>Edit Mode では物理の同期を待たずに確かめたいので、Collider.bounds ではなく Transform から計算する。</summary>
        private static bool IsInside(BoxCollider box, Vector3 worldPoint)
        {
            var local = box.transform.InverseTransformPoint(worldPoint) - box.center;
            var half = box.size / 2f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
    }
}
