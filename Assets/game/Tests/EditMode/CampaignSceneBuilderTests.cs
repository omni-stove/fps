using System;
using System.IO;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class CampaignSceneBuilderTests
    {
        private MissionDefinition _definition;
        private WeaponDefinition _rifle;
        private WeaponDefinition _handgun;
        private WeaponCatalogAsset _catalog;
        private GameRulesDefinition _easyRules;
        private GameRulesDefinition _normalRules;
        private GameRulesDefinition _hardRules;
        private Scene _scene;
        private NavMeshData _baked;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
            _definition = ScriptableObject.CreateInstance<MissionDefinition>();
            _definition.name = "Campaign01";
            CampaignSceneBuilder.Configure(_definition);
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            _handgun.name = "Handgun";
            WeaponAssetBuilder.Configure(_handgun, WeaponAssetBuilder.HandgunSpec());
            _catalog = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            _catalog.name = "WeaponCatalog";
            WeaponAssetBuilder.Configure(_catalog, new[] { _rifle, _handgun });
            _easyRules = NewRules("CampaignEasy", GameRulesAssetBuilder.CampaignEasyRules());
            _normalRules = NewRules("CampaignNormal", GameRulesAssetBuilder.CampaignNormalRules());
            _hardRules = NewRules("CampaignHard", GameRulesAssetBuilder.CampaignHardRules());
        }

        private static GameRulesDefinition NewRules(string assetName, GameRules rules)
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = assetName;
            GameRulesAssetBuilder.Configure(definition, rules);
            return definition;
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
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_handgun);
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_easyRules);
            Object.DestroyImmediate(_normalRules);
            Object.DestroyImmediate(_hardRules);
        }

        [Test]
        public void MissionIsReachAreaAThenKillFourEnemiesThenReachExit()
        {
            var mission = _definition.CreateMission();

            Assert.That(_definition.DisplayNameKey, Is.EqualTo(CampaignSceneBuilder.MissionNameKey));
            Assert.That(Localizer.Resolve(_definition.DisplayName), Is.EqualTo("Campaign 01"));
            Assert.That(mission.Objectives.Count, Is.EqualTo(3));
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

            // Level, GameBootstrap, MissionRunner, AreaA, Enemies, CoverPoints, Exit, CampaignRunner, 2 つのチェックポイント。
            Assert.That(_scene.GetRootGameObjects(), Has.Length.EqualTo(10));
            Assert.That(layout.Reveals, Is.Empty, "every Campaign01 enemy is there from the start");
            foreach (var enemy in layout.Enemies)
            {
                Assert.That(enemy.gameObject.activeSelf, Is.True);
            }

            Assert.That(layout.Bootstrap, Is.Not.Null);
            Assert.That(layout.Bootstrap.gameObject.scene, Is.EqualTo(_scene));
            Assert.That(layout.Bootstrap.CreatesFloor, Is.False, "the scene's own floor carries the baked NavMesh");
            Assert.That(layout.Bootstrap.WeaponDefinitions, Is.EqualTo(PlayerWeapons), "the player carries the rifle and the handgun");
            var placements = CampaignSceneBuilder.AllEnemies();
            Assert.That(layout.Enemies.Count, Is.EqualTo(placements.Count));
            for (var i = 0; i < placements.Count; i++)
            {
                var enemy = layout.Enemies[i];
                Assert.That(enemy.WeaponDefinition, Is.Not.Null, $"{enemy.Id} has a weapon");
                Assert.That(
                    enemy.WeaponDefinition.Id,
                    Is.EqualTo(placements[i].WeaponId),
                    $"{enemy.Id} shoots the weapon of its placement");
                Assert.That(_catalog.Weapons, Does.Contain(enemy.WeaponDefinition), $"{enemy.Id}'s weapon comes from the catalog");
            }

            Assert.That(layout.Runner.Definition, Is.SameAs(_definition));
            Assert.That(layout.Runner.gameObject.scene, Is.EqualTo(_scene));

            Assert.That(layout.Area(CampaignSceneBuilder.ReachAreaAId).ObjectiveId, Is.EqualTo(CampaignSceneBuilder.ReachAreaAId));
            Assert.That(layout.Area(CampaignSceneBuilder.ReachAreaAId).Runner, Is.SameAs(layout.Runner));
            Assert.That(layout.Area(CampaignSceneBuilder.ReachAreaAId).GetComponent<BoxCollider>().isTrigger, Is.True);
            Assert.That(layout.Area(CampaignSceneBuilder.ReachAreaAId).GetComponent<Rigidbody>().isKinematic, Is.True);

            Assert.That(layout.KillObjective(CampaignSceneBuilder.KillEnemiesId).ObjectiveId, Is.EqualTo(CampaignSceneBuilder.KillEnemiesId));
            Assert.That(layout.KillObjective(CampaignSceneBuilder.KillEnemiesId).Runner, Is.SameAs(layout.Runner));

            Assert.That(layout.Area(CampaignSceneBuilder.ReachExitId).ObjectiveId, Is.EqualTo(CampaignSceneBuilder.ReachExitId));
            Assert.That(layout.Area(CampaignSceneBuilder.ReachExitId).Runner, Is.SameAs(layout.Runner));
            Assert.That(layout.Area(CampaignSceneBuilder.ReachExitId).GetComponent<BoxCollider>().isTrigger, Is.True);
        }

        [Test]
        public void CoverPointsSitBesideTheCoversAndAreSharedByAllEnemies()
        {
            var layout = Populate();

            var registry = layout.CoverRegistry;
            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.name, Is.EqualTo(CampaignSceneBuilder.CoverPointsObjectName));
            Assert.That(registry.gameObject.scene, Is.EqualTo(_scene));
            foreach (var enemy in layout.Enemies)
            {
                Assert.That(enemy.CoverRegistry, Is.SameAs(registry), $"{enemy.Id} shares the registry so enemies do not pick the same point");
            }

            var placements = CampaignSceneBuilder.CoverPoints();
            Assert.That(placements.Count, Is.EqualTo(CampaignSceneBuilder.Covers.Count * 4), "both faces, both ends of every cover");
            Assert.That(registry.Points.Count, Is.EqualTo(placements.Count));
            Physics.SyncTransforms();
            for (var i = 0; i < placements.Count; i++)
            {
                var point = registry.Points[i];
                Assert.That(point.name, Is.EqualTo(placements[i].Name));
                Assert.That(point.transform.IsChildOf(registry.transform), Is.True);
                Assert.That(point.Height, Is.EqualTo(CoverHeight.High), "the 2m covers hide a standing enemy");
                Assert.That(Vector3.Distance(point.Position, placements[i].Position), Is.LessThan(1e-4f));
                Assert.That(Vector3.Distance(point.PeekPosition, placements[i].PeekPosition), Is.LessThan(1e-4f));

                // 点から向きの方へ進むと、その遮蔽物に当たる。
                var origin = point.Position + Vector3.up;
                Assert.That(
                    Physics.Raycast(origin, point.Facing, out var hit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                    Is.True,
                    $"{point.name} faces a cover");
                Assert.That(hit.collider.name, Does.StartWith("Cover_"), $"{point.name} faces a cover");

                // 覗く位置からは、遮蔽物の端を越えて向きの方が見通せる。
                var peekOrigin = point.PeekPosition + Vector3.up;
                Assert.That(
                    Physics.Raycast(peekOrigin, point.Facing, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                    Is.False,
                    $"{point.name} peeks past the edge");
            }
        }

        [Test]
        public void CampaignRunnerAndTwoCheckpointsAreWired()
        {
            var layout = Populate();

            var campaigns = Components<CampaignRunner>();
            Assert.That(campaigns, Has.Length.EqualTo(1));
            Assert.That(campaigns[0], Is.SameAs(layout.Campaign));
            Assert.That(layout.Campaign.gameObject.scene, Is.EqualTo(_scene));
            Assert.That(layout.Campaign.Runner, Is.SameAs(layout.Runner));
            Assert.That(layout.Campaign.Bootstrap, Is.SameAs(layout.Bootstrap));
            Assert.That(layout.Campaign.Storage, Is.Null, "the save storage is FileSaveStorage.CreateDefault, chosen at run time");

            var checkpoints = Components<CheckpointTrigger>();
            Assert.That(checkpoints, Has.Length.EqualTo(2));
            Assert.That(checkpoints, Does.Contain(layout.Checkpoint(CampaignSceneBuilder.StartCheckpointId)));
            Assert.That(checkpoints, Does.Contain(layout.Checkpoint(CampaignSceneBuilder.AreaACheckpointId)));

            AssertCheckpoint(layout.Checkpoint(CampaignSceneBuilder.StartCheckpointId), layout, CampaignSceneBuilder.StartCheckpointId, CampaignSceneBuilder.StartCheckpointPosition);
            AssertCheckpoint(layout.Checkpoint(CampaignSceneBuilder.AreaACheckpointId), layout, CampaignSceneBuilder.AreaACheckpointId, CampaignSceneBuilder.AreaACheckpointPosition);
            Assert.That(CampaignSceneBuilder.StartCheckpointId, Is.EqualTo("start"));
            Assert.That(CampaignSceneBuilder.AreaACheckpointId, Is.EqualTo("area_a"));

            // 開始地点のチェックポイントからは、GameBootstrap がプレイヤーを出す位置そのままで再開する。
            Assert.That(
                Vector3.Distance(layout.Checkpoint(CampaignSceneBuilder.StartCheckpointId).RespawnPosition, GameBootstrap.PlayerSpawn),
                Is.LessThan(1e-4f));
        }

        [Test]
        public void CampaignSavesAtTheCheckpointsAndRestartsFromAreaA()
        {
            var layout = Populate();
            var root = Path.Combine(Path.GetTempPath(), "fps-campaign-scene-tests-" + Guid.NewGuid().ToString("N"));
            var storage = new FileSaveStorage(root);
            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            BootstrapWorld world = null;
            try
            {
                // Edit Mode では Awake/Start が走らないので、GameBootstrap に作らせて武器だけ手で初期化する。
                // 武器の初期化は CampaignRunner より先に Built を受け取る購読者で済ませる。
                layout.Bootstrap.Built += built => built.Weapon.Initialize(built.Camera.transform, attack, reload);
                layout.Campaign.Storage = storage;
                layout.Campaign.SceneReloader = () => { };
                LogAssert.Expect(LogType.Warning, new Regex(@"^\[Campaign\] GameBootstrap '.*' がまだプレイヤーを生成していない"));
                Assert.That(layout.Campaign.BeginFromSceneOrWait(), Is.True);
                Assert.That(layout.Campaign.IsWaitingForBootstrap, Is.True);
                Assert.That(layout.Campaign.IsStarted, Is.False);

                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                LogAssert.Expect(LogType.Log, "[Campaign] started mission=Campaign01");
                LogAssert.Expect(LogType.Log, "[Campaign] no save; starting mission=Campaign01 from the beginning");
                world = layout.Bootstrap.Build();

                Assert.That(layout.Campaign.IsStarted, Is.True, "the campaign starts as soon as GameBootstrap has built the player");
                Assert.That(layout.Campaign.IsWaitingForBootstrap, Is.False);
                Assert.That(layout.Campaign.BoundHud, Is.SameAs(world.Hud));
                Assert.That(layout.Runner.IsStarted, Is.True);

                // 案内の文字列を確かめるため、既定の生成役が作った HUD を具体クラスで読む
                var hud = (Hud)world.Hud;
                var player = world.Controller;
                LogAssert.Expect(LogType.Log, new Regex(@"^\[Campaign\] saved mission=Campaign01 checkpoint=start "));
                Assert.That(layout.Checkpoint(CampaignSceneBuilder.StartCheckpointId).HandleEnter(player), Is.True);
                Assert.That(hud.Model.NoticeText, Is.EqualTo(HudFormat.CheckpointSaved));

                layout.Runner.Progress(CampaignSceneBuilder.ReachAreaAId);
                LogAssert.Expect(LogType.Log, new Regex(@"^\[Campaign\] saved mission=Campaign01 checkpoint=area_a "));
                Assert.That(layout.Checkpoint(CampaignSceneBuilder.AreaACheckpointId).HandleEnter(player), Is.True);
                Assert.That(layout.Campaign.SaveSlot, Is.EqualTo(CampaignSaveSlots.Prefix + "Campaign01"));
                Assert.That(storage.TryRead(layout.Campaign.SaveSlot, out var text), Is.True);
                var saved = CampaignSave.FromJson(text);
                Assert.That(saved.CheckpointId, Is.EqualTo(CampaignSceneBuilder.AreaACheckpointId));
                Assert.That(saved.Weapons, Has.Count.EqualTo(2), "every weapon in the loadout is saved");
                Assert.That(saved.EquippedWeaponId, Is.EqualTo(WeaponAssetBuilder.RifleId));

                layout.Campaign.Tick(CampaignRunner.SavedNoticeSeconds);
                Assert.That(hud.Model.NoticeText, Is.EqualTo(HudFormat.NoNotice), "the saved notice disappears");

                LogAssert.Expect(LogType.Log, "[Combat] target died id=Player");
                LogAssert.Expect(LogType.Log, "[Campaign] player died; reloading in 3s from checkpoint=area_a");
                world.PlayerDamageable.Kill();
                Assert.That(hud.Model.NoticeText, Is.EqualTo(HudFormat.CheckpointRestart(3)));
                layout.Campaign.Tick(1.5f);
                Assert.That(hud.Model.NoticeText, Is.EqualTo(HudFormat.CheckpointRestart(2)));

                layout.Campaign.Stop();
                Assert.That(hud.Model.NoticeText, Is.EqualTo(HudFormat.NoNotice), "stopping takes the notice off the HUD");
            }
            finally
            {
                layout.Campaign.Stop();
                DestroyWorld(world);
                attack.Dispose();
                reload.Dispose();
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void FourEnemiesTwoPatrolAndTwoWaitInAmbush()
        {
            var layout = Populate();

            Assert.That(layout.Enemies.Count, Is.EqualTo(CampaignSceneBuilder.EnemyCount));
            Assert.That(CampaignSceneBuilder.Patrollers.Count, Is.EqualTo(2));
            Assert.That(CampaignSceneBuilder.Ambushers.Count, Is.EqualTo(2));

            var placements = CampaignSceneBuilder.AllEnemies();
            var patrolling = 0;
            for (var i = 0; i < layout.Enemies.Count; i++)
            {
                var agent = layout.Enemies[i];
                var placement = placements[i];

                Assert.That(agent.name, Is.EqualTo(placement.Name));
                Assert.That(agent.Id, Is.EqualTo(placement.Id));
                Assert.That(agent.gameObject.scene, Is.EqualTo(_scene));
                Assert.That(agent.transform.parent, Is.SameAs(layout.KillObjective(CampaignSceneBuilder.KillEnemiesId).transform));
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

            var area = layout.Area(CampaignSceneBuilder.ReachAreaAId).GetComponent<BoxCollider>();
            Assert.That(IsInside(area, CampaignSceneBuilder.AreaACheckpointPosition + Vector3.up), Is.True);
            Assert.That(IsInside(area, CampaignSceneBuilder.StartCheckpointPosition + Vector3.up), Is.False);
        }

        [Test]
        public void AreaAFillsTheWidthInFrontOfTheOnlyDoorway()
        {
            var layout = Populate();
            var area = layout.Area(CampaignSceneBuilder.ReachAreaAId).GetComponent<BoxCollider>();

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
                Assert.That(layout.Area(CampaignSceneBuilder.ReachAreaAId).Bind(layout.Runner), Is.True);
                Assert.That(layout.KillObjective(CampaignSceneBuilder.KillEnemiesId).Bind(layout.Runner), Is.True);
                Assert.That(layout.Area(CampaignSceneBuilder.ReachExitId).Bind(layout.Runner), Is.True);
                Assert.That(layout.KillObjective(CampaignSceneBuilder.KillEnemiesId).BoundTargets.Count, Is.EqualTo(CampaignSceneBuilder.EnemyCount));
                var mission = layout.Runner.Mission;

                layout.Area(CampaignSceneBuilder.ReachAreaAId).HandleEnter(collider);
                Assert.That(mission.GetState(CampaignSceneBuilder.ReachAreaAId), Is.EqualTo(ObjectiveState.Completed));
                Assert.That(mission.GetState(CampaignSceneBuilder.KillEnemiesId), Is.EqualTo(ObjectiveState.Active));

                layout.Area(CampaignSceneBuilder.ReachExitId).HandleEnter(collider);
                layout.Area(CampaignSceneBuilder.ReachExitId).HandleExit(collider);
                Assert.That(mission.GetState(CampaignSceneBuilder.ReachExitId), Is.EqualTo(ObjectiveState.Locked));

                foreach (var enemy in layout.Enemies)
                {
                    enemy.GetComponent<Damageable>().Kill();
                }

                Assert.That(mission.GetState(CampaignSceneBuilder.KillEnemiesId), Is.EqualTo(ObjectiveState.Completed));

                LogAssert.Expect(LogType.Log, MissionReporter.CompletedLine);
                layout.Area(CampaignSceneBuilder.ReachExitId).HandleEnter(collider);
                Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PatrollersCarryTheRifleAndAmbushersTheHandgun()
        {
            var layout = Populate();

            var patrollers = CampaignSceneBuilder.Patrollers.Count;
            for (var i = 0; i < layout.Enemies.Count; i++)
            {
                var expected = i < patrollers ? _rifle : _handgun;
                Assert.That(layout.Enemies[i].WeaponDefinition, Is.SameAs(expected), $"{layout.Enemies[i].Id} weapon");
            }
        }

        [Test]
        public void RefusesToPopulateWhenAnEnemyWeaponIsNotInTheCatalog()
        {
            var rifleOnly = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            try
            {
                rifleOnly.name = "RifleOnly";
                WeaponAssetBuilder.Configure(rifleOnly, new[] { _rifle });

                var e = Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                    () => CampaignSceneBuilder.PopulateScene(_scene, _definition, PlayerWeapons, rifleOnly, Rules));
                var ambusher = CampaignSceneBuilder.Ambushers[0];
                Assert.That(e.Message, Does.Contain($"'{ambusher.Id}'"), "names the placement");
                Assert.That(e.Message, Does.Contain($"'{WeaponAssetBuilder.HandgunId}'"), "names the weapon id");
                Assert.That(e.Message, Does.Contain("'RifleOnly'"), "names the catalog");
                Assert.That(_scene.rootCount, Is.Zero, "nothing is placed");
            }
            finally
            {
                Object.DestroyImmediate(rifleOnly);
            }
        }

        [Test]
        public void ResolvesEveryPlacementWeaponFromTheCatalog()
        {
            var weapons = CampaignSceneBuilder.ResolveEnemyWeapons(_catalog);

            var placements = CampaignSceneBuilder.AllEnemies();
            Assert.That(weapons, Has.Length.EqualTo(placements.Count));
            for (var i = 0; i < placements.Count; i++)
            {
                Assert.That(weapons[i].Id, Is.EqualTo(placements[i].WeaponId));
            }

            Assert.That(weapons, Has.Member(_rifle).And.Member(_handgun), "the campaign uses more than one weapon");
            Assert.Throws<ArgumentNullException>(() => CampaignSceneBuilder.ResolveEnemyWeapons(null));
        }

        [Test]
        public void PlacementsNeedAWeaponId()
        {
            Assert.Throws<ArgumentException>(
                () => EnemyPlacement.Ambush("Enemy", "enemy", " ", Vector3.zero, Vector3.forward));
            Assert.Throws<ArgumentException>(
                () => EnemyPlacement.Patrol("Enemy", "enemy", null, Vector3.zero, Vector3.forward));
        }

        [Test]
        public void RefusesToPopulateANonEmptyScene()
        {
            SceneManager.MoveGameObjectToScene(new GameObject("Stray"), _scene);

            Assert.Throws<InvalidOperationException>(
                () => CampaignSceneBuilder.PopulateScene(_scene, _definition, PlayerWeapons, _catalog, Rules));
            Assert.That(_scene.rootCount, Is.EqualTo(1), "nothing is left behind");
        }

        [Test]
        public void RefusesToPopulateWithoutDefinition()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignSceneBuilder.PopulateScene(_scene, null, PlayerWeapons, _catalog, Rules));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        [Test]
        public void RefusesToPopulateWithoutAPlayerWeapon()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignSceneBuilder.PopulateScene(_scene, _definition, null, _catalog, Rules));
            Assert.Throws<ArgumentException>(
                () => CampaignSceneBuilder.PopulateScene(_scene, _definition, new WeaponDefinition[] { _rifle, null }, _catalog, Rules));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        [Test]
        public void RefusesToPopulateWithoutAWeaponCatalog()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignSceneBuilder.PopulateScene(_scene, _definition, PlayerWeapons, null, Rules));
            Assert.That(_scene.rootCount, Is.Zero);
        }

        [Test]
        public void RefusesToPopulateWithoutRules()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignSceneBuilder.PopulateScene(_scene, _definition, PlayerWeapons, _catalog, null));
            Assert.That(_scene.rootCount, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => new CampaignRules(null, _normalRules, _hardRules));
            Assert.Throws<ArgumentNullException>(() => new CampaignRules(_easyRules, null, _hardRules));
            Assert.Throws<ArgumentNullException>(() => new CampaignRules(_easyRules, _normalRules, null));
        }

        [Test]
        public void RulesAreWiredToTheBootstrapAndTheCampaignRunner()
        {
            var layout = Populate();

            Assert.That(layout.Bootstrap.RulesDefinition, Is.SameAs(_normalRules), "the player starts with the normal rules");
            Assert.That(layout.Campaign.RulesDefinition, Is.SameAs(_normalRules), "no -difficulty means normal");
            Assert.That(layout.Campaign.EasyRules, Is.SameAs(_easyRules));
            Assert.That(layout.Campaign.NormalRules, Is.SameAs(_normalRules));
            Assert.That(layout.Campaign.HardRules, Is.SameAs(_hardRules));
            Assert.That(layout.Campaign.RulesFor(CampaignDifficulty.Hard), Is.SameAs(_hardRules));
        }

        [Test]
        public void LoadRulesReadsTheCommittedCampaignRules()
        {
            Assume.That(
                AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(GameRulesAssetBuilder.CampaignNormalPath),
                Is.Not.Null,
                $"run {nameof(GameRulesAssetBuilder)}.Build first");

            var rules = CampaignSceneBuilder.LoadRules();

            Assert.That(AssetDatabase.GetAssetPath(rules.Easy), Is.EqualTo(GameRulesAssetBuilder.CampaignEasyPath));
            Assert.That(AssetDatabase.GetAssetPath(rules.Normal), Is.EqualTo(GameRulesAssetBuilder.CampaignNormalPath));
            Assert.That(AssetDatabase.GetAssetPath(rules.Hard), Is.EqualTo(GameRulesAssetBuilder.CampaignHardPath));
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

            Assert.That(definition.CreateMission().Objectives.Count, Is.EqualTo(3));

            // テストの前に開いていた追加シーンは使わず、保存済みのシーンを開いて確かめる。
            EditorSceneManager.CloseScene(_scene, true);
            _scene = EditorSceneManager.OpenScene(CampaignSceneBuilder.ScenePath, OpenSceneMode.Additive);

            MissionRunner runner = null;
            GameBootstrap bootstrap = null;
            NavMeshSurface surface = null;
            var triggers = 0;
            var enemies = 0;
            foreach (var root in _scene.GetRootGameObjects())
            {
                runner = runner != null ? runner : root.GetComponent<MissionRunner>();
                bootstrap = bootstrap != null ? bootstrap : root.GetComponent<GameBootstrap>();
                surface = surface != null ? surface : root.GetComponent<NavMeshSurface>();
                triggers += root.GetComponents<ReachAreaTrigger>().Length;
                enemies += root.GetComponentsInChildren<EnemyAgent>(true).Length;
            }

            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.Definition, Is.SameAs(definition));

            // 保存した参照が読み直しても切れていない。
            var campaigns = Components<CampaignRunner>();
            Assert.That(campaigns, Has.Length.EqualTo(1), $"run {nameof(CampaignSceneBuilder)}.Build again to add the CampaignRunner");
            Assert.That(campaigns[0].Runner, Is.SameAs(runner));
            Assert.That(campaigns[0].Bootstrap, Is.SameAs(bootstrap));
            var checkpoints = Components<CheckpointTrigger>();
            Assert.That(checkpoints, Has.Length.EqualTo(2));
            var ids = new System.Collections.Generic.List<string>();
            foreach (var checkpoint in checkpoints)
            {
                Assert.That(checkpoint.Runner, Is.SameAs(campaigns[0]));
                ids.Add(checkpoint.CheckpointId);
            }

            Assert.That(ids, Is.EquivalentTo(new[] { CampaignSceneBuilder.StartCheckpointId, CampaignSceneBuilder.AreaACheckpointId }));

            var normal = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(GameRulesAssetBuilder.CampaignNormalPath);
            Assert.That(normal, Is.Not.Null, $"run {nameof(GameRulesAssetBuilder)}.Build");
            Assert.That(bootstrap.RulesDefinition, Is.SameAs(normal), $"rerun {nameof(CampaignSceneBuilder)}.Build to wire the rules");
            Assert.That(campaigns[0].RulesDefinition, Is.SameAs(normal), $"rerun {nameof(CampaignSceneBuilder)}.Build to wire the rules");
            Assert.That(
                AssetDatabase.GetAssetPath(campaigns[0].EasyRules), Is.EqualTo(GameRulesAssetBuilder.CampaignEasyPath));
            Assert.That(campaigns[0].NormalRules, Is.SameAs(normal));
            Assert.That(
                AssetDatabase.GetAssetPath(campaigns[0].HardRules), Is.EqualTo(GameRulesAssetBuilder.CampaignHardPath));
            Assert.That(triggers, Is.EqualTo(2));
            Assert.That(enemies, Is.EqualTo(CampaignSceneBuilder.EnemyCount));

            var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponAssetBuilder.RiflePath);
            Assert.That(rifle, Is.Not.Null, $"run {nameof(WeaponAssetBuilder)}.Build");
            Assert.That(bootstrap.WeaponDefinitions, Is.EqualTo(TestScenes.ExpectedPlayerWeapons()), $"rerun {nameof(CampaignSceneBuilder)}.Build");
            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(WeaponAssetBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"run {nameof(WeaponAssetBuilder)}.Build");
            var expected = CampaignSceneBuilder.ResolveEnemyWeapons(catalog);
            var placements = CampaignSceneBuilder.AllEnemies();
            var agents = Components<EnemyAgent>();
            Assert.That(agents, Has.Length.EqualTo(placements.Count));
            for (var i = 0; i < placements.Count; i++)
            {
                var agent = Array.Find(agents, a => a.Id == placements[i].Id);
                Assert.That(agent, Is.Not.Null, $"'{placements[i].Id}' is in the scene");
                Assert.That(agent.WeaponDefinition, Is.SameAs(expected[i]), $"rerun {nameof(CampaignSceneBuilder)}.Build");
            }

            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.navMeshData, Is.Not.Null, "the NavMesh must be baked and committed");
            Assert.That(AssetDatabase.GetAssetPath(surface.navMeshData), Is.EqualTo(CampaignSceneBuilder.NavMeshDataPath));
        }

        private WeaponDefinition[] PlayerWeapons => new[] { _rifle, _handgun };

        private CampaignRules Rules => new CampaignRules(_easyRules, _normalRules, _hardRules);

        private CampaignLayout Populate()
        {
            var layout = CampaignSceneBuilder.PopulateScene(_scene, _definition, PlayerWeapons, _catalog, Rules);
            _baked = layout.Surface.navMeshData;
            return layout;
        }

        /// <summary>_scene に置かれた T をすべて (無効なものも) 集める。</summary>
        private T[] Components<T>()
            where T : Component
        {
            var found = new System.Collections.Generic.List<T>();
            foreach (var root in _scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return found.ToArray();
        }

        private void AssertCheckpoint(CheckpointTrigger checkpoint, CampaignLayout layout, string id, Vector3 feet)
        {
            Assert.That(checkpoint, Is.Not.Null);
            Assert.That(checkpoint.gameObject.scene, Is.EqualTo(_scene));
            Assert.That(checkpoint.CheckpointId, Is.EqualTo(id));
            Assert.That(checkpoint.Runner, Is.SameAs(layout.Campaign));
            Assert.That(checkpoint.PlayerTag, Is.EqualTo(GameBootstrap.PlayerTag));

            var collider = checkpoint.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.True);
            Assert.That(checkpoint.GetComponent<Rigidbody>().isKinematic, Is.True, "the trigger reports the CharacterController");
            Assert.That(IsInside(collider, feet + Vector3.up), Is.True, $"checkpoint '{id}' covers {feet}");

            var expectedRespawn = feet + new Vector3(0f, GameBootstrap.PlayerHeight / 2f, 0f);
            Assert.That(
                Vector3.Distance(checkpoint.RespawnPosition, expectedRespawn),
                Is.LessThan(1e-4f),
                $"the player restarts from '{id}' with the feet on the floor");
        }

        private static void DestroyWorld(BootstrapWorld world)
        {
            if (world == null)
            {
                return;
            }

            // Edit Mode では Hud の OnDestroy が走らないので、PanelSettings もここで消す。
            if (world.Hud != null)
            {
                Object.DestroyImmediate(world.Hud.gameObject);
            }

            if (world.HudPanelSettings != null)
            {
                Object.DestroyImmediate(world.HudPanelSettings);
            }

            if (world.Floor != null)
            {
                Object.DestroyImmediate(world.Floor);
            }

            Object.DestroyImmediate(world.Player);
            Object.DestroyImmediate(world.Light.gameObject);
            Object.DestroyImmediate(world.TargetRoot);
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
