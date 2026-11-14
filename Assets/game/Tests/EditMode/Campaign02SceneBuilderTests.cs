using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary><see cref="Campaign02Mission"/> の配置を <see cref="CampaignSceneBuilder"/> で置いて確かめる。</summary>
    public sealed class Campaign02SceneBuilderTests
    {
        private CampaignMissionSpec _spec;
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
            _spec = Campaign02Mission.Spec();
            _definition = ScriptableObject.CreateInstance<MissionDefinition>();
            _definition.name = Campaign02Mission.MissionId;
            CampaignSceneBuilder.Configure(_definition, _spec);
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

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

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
        public void MissionsAreCampaign01ThenCampaign02()
        {
            var missions = CampaignSceneBuilder.Missions();

            Assert.That(missions, Has.Count.EqualTo(2));
            Assert.That(missions[0].Id, Is.EqualTo(CampaignSceneBuilder.MissionId));
            Assert.That(missions[0].ScenePath, Is.EqualTo(BuildSceneList.CampaignScene));
            Assert.That(missions[0].DefinitionPath, Is.EqualTo(CampaignSceneBuilder.DefinitionPath));
            Assert.That(missions[0].NavMeshDataPath, Is.EqualTo(CampaignSceneBuilder.NavMeshDataPath));
            Assert.That(missions[1].Id, Is.EqualTo("Campaign02"));
            Assert.That(missions[1].NameKey, Is.EqualTo("campaign.mission.Campaign02.name"));
            Assert.That(missions[1].ScenePath, Is.EqualTo(BuildSceneList.Campaign02Scene));
            Assert.That(missions[1].DefinitionPath, Is.EqualTo("Assets/game/Missions/Campaign02.asset"));
            Assert.That(missions[1].NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/Campaign02/NavMesh-Level.asset"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.Campaign02Scene));
        }

        [Test]
        public void MissionIsEnterBuildingThenUseTerminalThenKillReinforcementsThenReachExtraction()
        {
            var mission = _definition.CreateMission();

            Assert.That(_definition.DisplayNameKey, Is.EqualTo(Campaign02Mission.MissionNameKey));
            Assert.That(Localizer.Resolve(_definition.DisplayName), Is.EqualTo("Campaign 02"));
            var reinforcements = Localizer.Resolve(mission.Objectives[2].Description);
            Assert.That(reinforcements, Is.Not.EqualTo(mission.Objectives[2].DescriptionKey), "the description is in the table");
            Assert.That(reinforcements, Does.Contain(Campaign02Mission.Reinforcements.Count.ToString(CultureInfo.InvariantCulture)));
            Assert.That(mission.Objectives, Has.Count.EqualTo(4));
            Assert.That(mission.Objectives[0].Id, Is.EqualTo(Campaign02Mission.EnterBuildingId));
            Assert.That(mission.Objectives[0].Prerequisites, Is.Empty);
            Assert.That(mission.Objectives[1].Id, Is.EqualTo(Campaign02Mission.UseTerminalId));
            Assert.That(mission.Objectives[1].Prerequisites, Is.EqualTo(new[] { Campaign02Mission.EnterBuildingId }));
            Assert.That(mission.Objectives[2].Id, Is.EqualTo(Campaign02Mission.KillReinforcementsId));
            Assert.That(mission.Objectives[2].RequiredCount, Is.EqualTo(Campaign02Mission.Reinforcements.Count));
            Assert.That(mission.Objectives[2].Prerequisites, Is.EqualTo(new[] { Campaign02Mission.UseTerminalId }));
            Assert.That(mission.Objectives[3].Id, Is.EqualTo(Campaign02Mission.ReachExtractionId));
            Assert.That(mission.Objectives[3].Prerequisites, Is.EqualTo(new[] { Campaign02Mission.KillReinforcementsId }));
        }

        [Test]
        public void PopulatedSceneIsWiredInCode()
        {
            var layout = Populate();

            // Level, GameBootstrap, MissionRunner, BuildingEntrance, Guards, Terminal, Reinforcements, CoverPoints, Extraction,
            // CampaignRunner, 3 つのチェックポイント。
            Assert.That(_scene.GetRootGameObjects(), Has.Length.EqualTo(13));
            Assert.That(layout.Bootstrap.CreatesFloor, Is.False);
            Assert.That(layout.Runner.Definition, Is.SameAs(_definition));
            Assert.That(layout.Campaign.Runner, Is.SameAs(layout.Runner));
            Assert.That(layout.Campaign.Bootstrap, Is.SameAs(layout.Bootstrap));
            Assert.That(layout.Campaign.RulesDefinition, Is.SameAs(_normalRules));
            Assert.That(layout.Campaign.HardRules, Is.SameAs(_hardRules));

            var entrance = layout.Area(Campaign02Mission.EnterBuildingId);
            Assert.That(entrance, Is.InstanceOf<ReachAreaTrigger>());
            Assert.That(entrance.Runner, Is.SameAs(layout.Runner));
            Assert.That(entrance.name, Is.EqualTo(Campaign02Mission.BuildingEntranceObjectName));

            var terminal = layout.Area(Campaign02Mission.UseTerminalId);
            Assert.That(terminal, Is.InstanceOf<InteractObjective>(), "the terminal is operated, not just reached");
            Assert.That(terminal.Runner, Is.SameAs(layout.Runner));
            Assert.That(terminal.GetComponent<BoxCollider>().isTrigger, Is.True);

            var extraction = layout.Area(Campaign02Mission.ReachExtractionId);
            Assert.That(extraction, Is.InstanceOf<ReachAreaTrigger>());
            Assert.That(extraction.Runner, Is.SameAs(layout.Runner));

            Assert.That(layout.KillObjectives, Has.Count.EqualTo(1), "the guards are not part of any objective");
            var kill = layout.KillObjective(Campaign02Mission.KillReinforcementsId);
            Assert.That(kill.name, Is.EqualTo(Campaign02Mission.ReinforcementsObjectName));
            Assert.That(kill.Runner, Is.SameAs(layout.Runner));

            var reveal = layout.Reveal(Campaign02Mission.KillReinforcementsId);
            Assert.That(reveal.gameObject, Is.SameAs(kill.gameObject));
            Assert.That(reveal.Runner, Is.SameAs(layout.Runner));
            Assert.That(reveal.Targets, Has.Count.EqualTo(Campaign02Mission.Reinforcements.Count));

            var placements = _spec.AllEnemies();
            Assert.That(layout.Enemies, Has.Count.EqualTo(placements.Count));
            Assert.That(placements.Count, Is.EqualTo(Campaign02Mission.Guards.Count + Campaign02Mission.Reinforcements.Count));
            for (var i = 0; i < placements.Count; i++)
            {
                var enemy = layout.Enemies[i];
                Assert.That(enemy.Id, Is.EqualTo(placements[i].Id));
                Assert.That(enemy.WeaponDefinition.Id, Is.EqualTo(placements[i].WeaponId));
                Assert.That(enemy.CoverRegistry, Is.SameAs(layout.CoverRegistry), $"{enemy.Id} shares the cover points");

                var isReinforcement = i >= Campaign02Mission.Guards.Count;
                Assert.That(
                    enemy.gameObject.activeSelf,
                    Is.EqualTo(!isReinforcement),
                    isReinforcement ? $"{enemy.Id} waits hidden for the terminal" : $"{enemy.Id} guards from the start");
                Assert.That(
                    enemy.transform.parent.name,
                    Is.EqualTo(isReinforcement ? Campaign02Mission.ReinforcementsObjectName : Campaign02Mission.GuardsObjectName));
                if (isReinforcement)
                {
                    Assert.That(reveal.Targets, Does.Contain(enemy.gameObject));
                }
            }

            Assert.That(layout.CoverRegistry, Is.Not.Null);
            Assert.That(layout.CoverRegistry.Points, Has.Count.EqualTo(_spec.CoverPoints.Count));
        }

        [Test]
        public void CoverPointsFaceTheirCovers()
        {
            var layout = Populate();
            Physics.SyncTransforms();

            var low = 0;
            var high = 0;
            foreach (var point in layout.CoverRegistry.Points)
            {
                // 低い遮蔽物 (1m) はしゃがんだ高さで、高い遮蔽物 (2m) は立った高さで当たる。
                var origin = point.Position + (Vector3.up * (point.Height == CoverHeight.Low ? 0.5f : 1f));
                Assert.That(
                    Physics.Raycast(origin, point.Facing, out var hit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                    Is.True,
                    $"{point.name} faces a cover");
                Assert.That(hit.collider.name, Does.StartWith("Cover_"), $"{point.name} faces a cover");
                Assert.That(point.name, Does.StartWith(hit.collider.name), $"{point.name} faces its own cover");
                low += point.Height == CoverHeight.Low ? 1 : 0;
                high += point.Height == CoverHeight.High ? 1 : 0;
            }

            Assert.That(low, Is.EqualTo(Campaign02Mission.LowCovers.Count * 4));
            Assert.That(high, Is.EqualTo(Campaign02Mission.HighCovers.Count * 4));
        }

        [Test]
        public void NavMeshReachesEveryPointFromTheStart()
        {
            Populate();

            Assert.That(NavMesh.SamplePosition(CampaignMissionSpec.PlayerStart, out var start, 1f, NavMesh.AllAreas), Is.True);
            var path = new NavMeshPath();
            foreach (var point in _spec.RequiredPoints())
            {
                Assert.That(NavMesh.SamplePosition(point, out var end, 1f, NavMesh.AllAreas), Is.True, $"{point} is on the NavMesh");
                Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), $"{point} is reachable from the start");
            }
        }

        [Test]
        public void CheckpointsAreTheStartInsideTheBuildingAndBeforeTheTerminal()
        {
            var layout = Populate();

            Assert.That(layout.Checkpoints, Has.Count.EqualTo(3));
            var start = layout.Checkpoint(Campaign02Mission.StartCheckpointId);
            Assert.That(
                Vector3.Distance(start.RespawnPosition, GameBootstrap.PlayerSpawn),
                Is.LessThan(1e-4f),
                "the start checkpoint restarts where GameBootstrap spawns the player");

            var building = layout.Checkpoint(Campaign02Mission.BuildingCheckpointId);
            var entrance = layout.Area(Campaign02Mission.EnterBuildingId).GetComponent<BoxCollider>();
            Assert.That(IsInside(entrance, Campaign02Mission.BuildingCheckpointPosition + Vector3.up), Is.True);
            Assert.That(building.RespawnPosition.z, Is.GreaterThan(Campaign02Mission.FrontWallZ), "restarts inside the building");

            var terminal = layout.Checkpoint(Campaign02Mission.TerminalCheckpointId);
            Assert.That(terminal.RespawnPosition.z, Is.GreaterThan(Campaign02Mission.PartitionZ), "restarts in the terminal room");
            Assert.That(terminal.RespawnPosition.z, Is.LessThan(Campaign02Mission.BackWallZ));
            var terminalPad = layout.Area(Campaign02Mission.UseTerminalId).GetComponent<BoxCollider>();
            Assert.That(
                IsInside(terminalPad, Campaign02Mission.TerminalCheckpointPosition + Vector3.up),
                Is.False,
                "restarting does not put the player on the terminal");

            foreach (var checkpoint in layout.Checkpoints)
            {
                Assert.That(checkpoint.Runner, Is.SameAs(layout.Campaign));
                Assert.That(checkpoint.PlayerTag, Is.EqualTo(GameBootstrap.PlayerTag));
            }
        }

        [Test]
        public void EntranceFillsTheBuildingWidthBehindTheOnlyDoor()
        {
            var layout = Populate();
            var area = layout.Area(Campaign02Mission.EnterBuildingId).GetComponent<BoxCollider>();

            foreach (var x in new[] { -Campaign02Mission.HalfWidth + 1f, 0f, Campaign02Mission.HalfWidth - 1f })
            {
                var point = new Vector3(x, 1f, Campaign02Mission.FrontWallZ + 2f);
                Assert.That(IsInside(area, point), Is.True, $"{point} is in the entrance");
            }
        }

        [Test]
        public void ReinforcementsAppearOutOfSightOfTheTerminal()
        {
            CampaignSceneBuilder.CreateLevel(_scene, _spec);
            Physics.SyncTransforms();

            var eyeHeight = GameBootstrap.PlayerSpawn.y + GameBootstrap.EyeOffset.y;
            var pad = Campaign02Mission.TerminalPosition;
            var half = Campaign02Mission.TerminalPadScale / 2f;
            foreach (var reinforcement in Campaign02Mission.Reinforcements)
            {
                foreach (var dx in new[] { -half.x, 0f, half.x })
                {
                    foreach (var dz in new[] { -half.z, 0f, half.z })
                    {
                        var eye = new Vector3(pad.x + dx, eyeHeight, pad.z + dz);
                        var point = reinforcement.Position + Vector3.up;
                        var toPoint = point - eye;
                        Assert.That(
                            Physics.Raycast(eye, toPoint.normalized, toPoint.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                            Is.True,
                            $"{reinforcement.Id} appears in sight of {eye}");
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
                foreach (var area in layout.Areas)
                {
                    Assert.That(area.Bind(layout.Runner), Is.True, $"{area.name} binds");
                }

                var kill = layout.KillObjective(Campaign02Mission.KillReinforcementsId);
                var reveal = layout.Reveal(Campaign02Mission.KillReinforcementsId);
                Assert.That(kill.Bind(layout.Runner), Is.True);
                Assert.That(reveal.Bind(layout.Runner), Is.True);
                Assert.That(kill.BoundTargets, Has.Count.EqualTo(Campaign02Mission.Reinforcements.Count));
                var mission = layout.Runner.Mission;
                var terminal = (InteractObjective)layout.Area(Campaign02Mission.UseTerminalId);

                // 建物に入る前は、端末に入っても操作できない。
                terminal.HandleEnter(collider);
                Assert.That(terminal.Interact(), Is.False);
                terminal.HandleExit(collider);
                Assert.That(reveal.Refresh(), Is.False);

                layout.Area(Campaign02Mission.EnterBuildingId).HandleEnter(collider);
                Assert.That(mission.GetState(Campaign02Mission.EnterBuildingId), Is.EqualTo(ObjectiveState.Completed));
                Assert.That(mission.GetState(Campaign02Mission.UseTerminalId), Is.EqualTo(ObjectiveState.Active));

                terminal.HandleEnter(collider);
                LogAssert.Expect(LogType.Log, $"[Mission] interacted objective={Campaign02Mission.UseTerminalId}");
                Assert.That(terminal.Interact(), Is.True);
                Assert.That(mission.GetState(Campaign02Mission.KillReinforcementsId), Is.EqualTo(ObjectiveState.Active));

                LogAssert.Expect(
                    LogType.Log,
                    $"[Mission] revealed objective={Campaign02Mission.KillReinforcementsId} targets={Campaign02Mission.Reinforcements.Count}");
                Assert.That(reveal.Refresh(), Is.True);
                foreach (var target in kill.BoundTargets)
                {
                    Assert.That(target.gameObject.activeSelf, Is.True, $"{target.name} appears once the terminal is used");
                }

                layout.Area(Campaign02Mission.ReachExtractionId).HandleEnter(collider);
                layout.Area(Campaign02Mission.ReachExtractionId).HandleExit(collider);
                Assert.That(mission.GetState(Campaign02Mission.ReachExtractionId), Is.EqualTo(ObjectiveState.Locked));

                foreach (var target in kill.BoundTargets)
                {
                    target.Kill();
                }

                Assert.That(mission.GetState(Campaign02Mission.KillReinforcementsId), Is.EqualTo(ObjectiveState.Completed));

                LogAssert.Expect(LogType.Log, MissionReporter.CompletedLine);
                layout.Area(Campaign02Mission.ReachExtractionId).HandleEnter(collider);
                Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void CommittedAssetsMatchTheBuilder()
        {
            var definition = AssetDatabase.LoadAssetAtPath<MissionDefinition>(Campaign02Mission.DefinitionPath);
            Assume.That(definition, Is.Not.Null, $"run {nameof(CampaignSceneBuilder)}.Build first");
            Assume.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(Campaign02Mission.ScenePath),
                Is.Not.Null,
                $"run {nameof(CampaignSceneBuilder)}.Build first");

            Assert.That(definition.CreateMission().Objectives, Has.Count.EqualTo(4));

            EditorSceneManager.CloseScene(_scene, true);
            _scene = EditorSceneManager.OpenScene(Campaign02Mission.ScenePath, OpenSceneMode.Additive);

            var runners = Components<MissionRunner>();
            Assert.That(runners, Has.Length.EqualTo(1));
            Assert.That(runners[0].Definition, Is.SameAs(definition));
            Assert.That(Components<InteractObjective>(), Has.Length.EqualTo(1));
            Assert.That(Components<ReachAreaTrigger>(), Has.Length.EqualTo(2));
            Assert.That(Components<CheckpointTrigger>(), Has.Length.EqualTo(3));
            Assert.That(Components<EnemyAgent>(), Has.Length.EqualTo(_spec.AllEnemies().Count));

            var reveals = Components<ObjectiveReveal>();
            Assert.That(reveals, Has.Length.EqualTo(1));
            Assert.That(reveals[0].Runner, Is.SameAs(runners[0]));
            foreach (var target in reveals[0].Targets)
            {
                Assert.That(target, Is.Not.Null, $"rerun {nameof(CampaignSceneBuilder)}.Build");
                Assert.That(target.activeSelf, Is.False, "reinforcements are saved hidden");
            }

            var surfaces = Components<Unity.AI.Navigation.NavMeshSurface>();
            Assert.That(surfaces, Has.Length.EqualTo(1));
            Assert.That(AssetDatabase.GetAssetPath(surfaces[0].navMeshData), Is.EqualTo(Campaign02Mission.NavMeshDataPath));
        }

        [Test]
        public void SpecRejectsLayoutsThatCannotBeCompleted()
        {
            var objectives = new[]
            {
                new ObjectiveDefinition("reach", ""),
                new ObjectiveDefinition("kill", "", 2, false, "reach"),
            };
            var enemy = EnemyPlacement.Ambush("Enemy", "enemy", WeaponAssetBuilder.RifleId, Vector3.zero, Vector3.forward);
            var reach = CampaignAreaElement.Reach("Reach", "reach", Vector3.zero, Vector3.one);

            // 2 体倒す目標に 1 体しかいない。
            var e = Assert.Throws<ArgumentException>(() => Spec(objectives, reach, new CampaignEnemyGroupElement("Enemies", new[] { enemy }, "kill")));
            Assert.That(e.Message, Does.Contain("needs 2 kills"));

            // 撃破の目標を進める物が無い。
            e = Assert.Throws<ArgumentException>(() => Spec(objectives, reach));
            Assert.That(e.Message, Does.Contain("progresses objective 'kill'"));

            // 同じ目標を 2 つの範囲が進める。
            e = Assert.Throws<ArgumentException>(
                () => Spec(new[] { new ObjectiveDefinition("reach", "") }, reach, CampaignAreaElement.Reach("Reach2", "reach", Vector3.zero, Vector3.one)));
            Assert.That(e.Message, Does.Contain("already progresses"));

            // 知らない目標を指す。
            e = Assert.Throws<ArgumentException>(
                () => Spec(new[] { new ObjectiveDefinition("reach", "") }, reach, CampaignAreaElement.Reach("Other", "missing", Vector3.zero, Vector3.one)));
            Assert.That(e.Message, Does.Contain("unknown objective 'missing'"));

            // 名前の重なり。
            e = Assert.Throws<ArgumentException>(
                () => Spec(new[] { new ObjectiveDefinition("reach", "") }, CampaignAreaElement.Reach(CampaignSceneBuilder.LevelObjectName, "reach", Vector3.zero, Vector3.one)));
            Assert.That(e.Message, Does.Contain("twice"));
        }

        [Test]
        public void SpecNeedsTheDefinitionNamedAfterTheMissionAndOneCoverPointsElement()
        {
            var objectives = new[] { new ObjectiveDefinition("reach", "") };
            var reach = CampaignAreaElement.Reach("Reach", "reach", Vector3.zero, Vector3.one);

            Assert.Throws<ArgumentException>(
                () => new CampaignMissionSpec(
                    "Mission", "Mission", "Assets/Mission.unity", "Assets/Other.asset", "Assets/NavMesh.asset",
                    Vector3.zero, Vector3.one, Array.Empty<LevelBox>(), Array.Empty<LevelBox>(), Array.Empty<CoverPointPlacement>(),
                    objectives, new CampaignElement[] { reach }, Array.Empty<CampaignCheckpoint>()));

            var point = CoverPointPlacement.Low("Point", Vector3.zero, Vector3.forward);
            Assert.Throws<ArgumentException>(
                () => new CampaignMissionSpec(
                    "Mission", "Mission", "Assets/Mission.unity", "Assets/Mission.asset", "Assets/NavMesh.asset",
                    Vector3.zero, Vector3.one, Array.Empty<LevelBox>(), Array.Empty<LevelBox>(), new[] { point },
                    objectives, new CampaignElement[] { reach }, Array.Empty<CampaignCheckpoint>()),
                "cover points without a place to put them");
        }

        [Test]
        public void LowCoverPointsRejectCoversThatAreTooTall()
        {
            var tall = new LevelBox("Cover_Tall", new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 1f));

            Assert.Throws<ArgumentException>(() => CampaignSceneBuilder.LowCoverPoints(new[] { tall }));
            Assert.That(CampaignSceneBuilder.HighCoverPoints(new[] { tall }), Has.Count.EqualTo(4));
        }

        private static CampaignMissionSpec Spec(IReadOnlyList<ObjectiveDefinition> objectives, params CampaignElement[] elements)
        {
            return new CampaignMissionSpec(
                "Mission",
                "Mission",
                "Assets/Mission.unity",
                "Assets/Mission.asset",
                "Assets/NavMesh.asset",
                Vector3.zero,
                Vector3.one,
                Array.Empty<LevelBox>(),
                Array.Empty<LevelBox>(),
                Array.Empty<CoverPointPlacement>(),
                objectives,
                elements,
                Array.Empty<CampaignCheckpoint>());
        }

        private CampaignLayout Populate()
        {
            var layout = CampaignSceneBuilder.PopulateScene(
                _scene, _spec, _definition, new[] { _rifle, _handgun }, _catalog, new CampaignRules(_easyRules, _normalRules, _hardRules));
            _baked = layout.Surface.navMeshData;
            return layout;
        }

        private T[] Components<T>()
            where T : Component
        {
            var found = new List<T>();
            foreach (var root in _scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return found.ToArray();
        }

        private static GameRulesDefinition NewRules(string assetName, GameRules rules)
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = assetName;
            GameRulesAssetBuilder.Configure(definition, rules);
            return definition;
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
