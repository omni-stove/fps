using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net.Tests
{
    /// <summary>
    /// コミットされた NetTest シーンと試合の設定 (Fps.Editor.NetSceneBuilder.Build の出力) が、.NET のサーバーへの書き出しとクライアントで使えるか。
    /// 失敗したら Build をやり直してシーンとアセットをコミットする。
    /// </summary>
    public sealed class NetTestSceneTests
    {
        private const string _rerun = "run Fps.Editor.NetSceneBuilder.Build";

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

            Assert.That(missing, Is.Empty, "objects with removed (Netcode) components; " + _rerun);
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
            Assert.That(rules, Is.Not.Null, "run Fps.Editor.GameRulesAssetBuilder.Build");
            Assert.That(rules.TryToRules(out var loaded, out var error), Is.True, error);
            Assert.That(loaded.Objective, Is.EqualTo(MatchObjective.Domination), "run Fps.Editor.GameRulesAssetBuilder.Build");
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
