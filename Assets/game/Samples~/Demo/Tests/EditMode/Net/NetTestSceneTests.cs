using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Net.Tests
{
    public sealed class NetTestSceneTests
    {
        private const string _rerun = "run Shooter.Editor.NetSceneBuilder.Build";

        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(NetSceneBuilder.ScenePath), Is.Not.Null,
                NetSceneBuilder.ScenePath + " is missing; " + _rerun);
            _scene = EditorSceneManager.OpenScene(NetSceneBuilder.ScenePath, OpenSceneMode.Additive);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void HasNoMissingScripts()
        {
            var missing = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .Where(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                .Select(t => t.name)
                .ToArray();

            Assert.That(missing, Is.Empty, "objects with missing scripts; " + _rerun);
        }

        [Test]
        public void TheGroundIsWalledInAlongItsEdges()
        {
            var perimeter = _scene.GetRootGameObjects().SingleOrDefault(go => go.name == SceneBuildHelpers.PerimeterName);
            Assert.That(perimeter, Is.Not.Null, "the ground has no perimeter walls; " + _rerun);

            var expected = new[]
            {
                ("Perimeter_North", new Vector3(0f, 3f, 30.5f), new Vector3(62f, 8f, 1f)),
                ("Perimeter_South", new Vector3(0f, 3f, -30.5f), new Vector3(62f, 8f, 1f)),
                ("Perimeter_East", new Vector3(30.5f, 3f, 0f), new Vector3(1f, 8f, 60f)),
                ("Perimeter_West", new Vector3(-30.5f, 3f, 0f), new Vector3(1f, 8f, 60f)),
            };
            Assert.That(perimeter.transform.childCount, Is.EqualTo(expected.Length));
            foreach (var (wallName, position, size) in expected)
            {
                var wall = perimeter.transform.Find(wallName);
                Assert.That(wall, Is.Not.Null, wallName);
                Assert.That(Vector3.Distance(wall.position, position), Is.LessThan(1e-3f), wallName);
                Assert.That(Vector3.Distance(wall.lossyScale, size), Is.LessThan(1e-3f), wallName);
                Assert.That(wall.GetComponent<BoxCollider>(), Is.Not.Null, wallName);
            }

            foreach (var (_, position, _) in NetSceneBuilder.SpawnPoints)
            {
                Assert.That(Mathf.Abs(position.x), Is.LessThan(30f), "spawn points stay inside the walls");
                Assert.That(Mathf.Abs(position.z), Is.LessThan(30f), "spawn points stay inside the walls");
            }
        }

        [Test]
        public void SpawnPointsCanStartTeamDeathmatch()
        {
            var points = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetSpawnPoint>(true))
                .Select(marker => marker.ToSpawnPoint())
                .ToArray();

            Assert.That(points.Any(p => p.Team == Team.Alpha), Is.True);
            Assert.That(points.Any(p => p.Team == Team.Bravo), Is.True);

            // サーバーと同じ組み立てで例外が出ないこと (どちらのチームも使える地点がある)
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(10, 300f, 3f), 100, points, new SpawnSelector(), _ => System.Numerics.Vector3.Zero);
            Assert.That(session.Join(0), Is.EqualTo(Team.Alpha));
            Assert.That(session.Join(1), Is.EqualTo(Team.Bravo));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void HasOneControlPointTheDominationRulesCanUse()
        {
            var zones = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<ControlPointZone>(true))
                .ToArray();

            Assert.That(zones, Has.Length.EqualTo(NetSceneBuilder.ControlPoints.Length), _rerun);
            Assert.That(zones, Has.Length.EqualTo(1));
            var zone = zones[0];
            Assert.That(zone.Index, Is.Zero);

            // 床に立つプレイヤーのカプセルの中心 (y=0) が範囲に入る
            Assert.That(zone.Contains(zone.transform.position + new Vector3(0f, -NetSceneBuilder.GroundY, 0f)), Is.True);

            var rules = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(GameRulesAssetBuilder.DominationPath);
            Assert.That(rules, Is.Not.Null, "run Shooter.Editor.GameRulesAssetBuilder.Build");
            Assert.That(rules.TryToRules(out var loaded, out var error), Is.True, error);
            Assert.That(loaded.Objective, Is.EqualTo(MatchObjective.Domination), "run Shooter.Editor.GameRulesAssetBuilder.Build");
        }

        [Test]
        public void HasOneFlagBasePerTeamTheCaptureTheFlagServerCanUse()
        {
            var bases = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<FlagBase>(false))
                .ToArray();

            Assert.That(bases.Select(b => b.Side), Is.EqualTo(new[] { Side.Alpha, Side.Bravo }),
                "the CaptureTheFlag server refuses a map without both bases; run Shooter.Editor.ServerDataMaps.EnsureFlagBases or " + _rerun);
            for (var i = 0; i < bases.Length; i++)
            {
                Assert.That(bases[i].name, Is.EqualTo(NetSceneBuilder.FlagBaseName(bases[i].Side)));
                Assert.That(bases[i].Position, Is.EqualTo(NetSceneBuilder.FlagBases[i].Position));
                Assert.That(bases[i].ShowsMarker, Is.True);
            }

            var match = AssetDatabase.LoadAssetAtPath<NetMatchDefinition>(NetSceneBuilder.MatchDefinitionPath);
            Assert.That(match, Is.Not.Null, NetSceneBuilder.MatchDefinitionPath + " is missing; " + _rerun);
            Assert.That(bases.Select(b => b.Radius), Is.All.EqualTo(NetSceneBuilder.CaptureRadius(match)), "the disc matches the radius the server judges by");

            // 床に立つプレイヤーのカプセルの中心 (y=0) で、自陣の台に運び込める
            foreach (var flagBase in bases)
            {
                Assert.That(flagBase.Contains(new Vector3(flagBase.Position.x, 0f, flagBase.Position.z)), Is.True, flagBase.name);
            }

            var spawns = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetSpawnPoint>(true))
                .ToArray();
            foreach (var spawn in spawns)
            {
                Assert.That(bases.Any(b => b.Contains(spawn.transform.position)), Is.False, spawn.name + " is not inside a flag base");
            }
        }

        [Test]
        public void HasACameraForBeforeTheLocalPlayerSpawns()
        {
            var cameras = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Camera>(true))
                .ToArray();

            Assert.That(cameras, Has.Length.EqualTo(1), _rerun);
            Assert.That(cameras[0].enabled, Is.True);
            Assert.That(cameras[0].CompareTag("MainCamera"), Is.True);
        }

        [Test]
        public void IsInBuildSettings()
        {
            var entry = EditorBuildSettings.scenes.FirstOrDefault(s => s.path == NetSceneBuilder.ScenePath);

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.enabled, Is.True);
        }

        [Test]
        public void BuildSceneListNamesTheSceneAsAServerMap()
        {
            Assert.That(BuildSceneList.NetTestScene, Is.EqualTo(NetSceneBuilder.ScenePath));
            Assert.That(BuildSceneList.Scenes, Has.Member(NetSceneBuilder.ScenePath), "clients load the map the server chose");
            Assert.That(BuildSceneList.ServerScenes[0], Is.EqualTo(NetSceneBuilder.ScenePath), "the server's default map is NetTest");
        }

        [Test]
        public void TheMatchDefinitionHasAUsableLoadoutAndTheDefaultSettings()
        {
            var match = AssetDatabase.LoadAssetAtPath<NetMatchDefinition>(NetSceneBuilder.MatchDefinitionPath);
            Assert.That(match, Is.Not.Null, NetSceneBuilder.MatchDefinitionPath + " is missing; " + _rerun);

            Assert.That(match.TryCreateLoadout(out var loadout, out var error), Is.True, error);
            Assert.That(loadout.Weapons, Is.Not.Empty);
            Assert.That(match.Catalog, Is.Not.Null, _rerun);
            Assert.That(match.ToSettings().RestartDelaySeconds, Is.EqualTo(NetMatchSettings.DefaultRestartDelaySeconds));

            var motor = match.ToMotor();
            Assert.That(motor.Stance.Standing.Height, Is.EqualTo(NetSceneBuilder.PlayerHeight));
            Assert.That(motor.Stance.Radius, Is.EqualTo(NetSceneBuilder.PlayerRadius));
        }
    }
}
