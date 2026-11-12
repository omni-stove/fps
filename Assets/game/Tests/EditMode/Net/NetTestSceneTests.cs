using System.Linq;
using Fps.Core;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net.Tests
{
    /// <summary>
    /// コミットされた NetTest シーン (Fps.Editor.NetSceneBuilder.Build の出力) の参照が解決できているか。
    /// 失敗したら Build をやり直してシーンとプレハブをコミットする。
    /// </summary>
    public sealed class NetTestSceneTests
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(NetBootstrap.ScenePath), Is.Not.Null,
                NetBootstrap.ScenePath + " is missing; run Fps.Editor.NetSceneBuilder.Build");
            _scene = EditorSceneManager.OpenScene(NetBootstrap.ScenePath, OpenSceneMode.Additive);
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
        public void HasOneBootstrapWithNetworkedPlayerPrefab()
        {
            var bootstraps = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .ToArray();

            Assert.That(bootstraps, Has.Length.EqualTo(1));
            var bootstrap = bootstraps[0];
            Assert.That(bootstrap.transform.parent, Is.Null, "NetworkManager needs a root GameObject");
            Assert.That(bootstrap.PlayerPrefab, Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabAsset(bootstrap.PlayerPrefab), Is.True);

            var networkObject = bootstrap.PlayerPrefab.GetComponent<NetworkObject>();
            Assert.That(networkObject, Is.Not.Null);
            Assert.That(bootstrap.PlayerPrefab.GetComponent<NetPlayer>(), Is.Not.Null);
            Assert.That(bootstrap.PlayerPrefab.GetComponent<NetworkTransform>(), Is.Null,
                "NetworkPlayerMotor syncs the position; a NetworkTransform would fight it");
        }

        [Test]
        public void HasMatchPrefabWithTeamDeathmatch()
        {
            var bootstrap = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .Single();

            Assert.That(bootstrap.MatchPrefab, Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabAsset(bootstrap.MatchPrefab), Is.True);
            Assert.That(bootstrap.MatchPrefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(bootstrap.MatchPrefab.GetComponent<NetTeamDeathmatch>(), Is.Not.Null);
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

            // 実行時と同じ組み立てで例外が出ないこと (どちらのチームも使える地点がある)
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(10, 300f, 3f), 100, points, new SpawnSelector(), _ => System.Numerics.Vector3.Zero);
            Assert.That(session.Join(0), Is.EqualTo(Team.Alpha));
            Assert.That(session.Join(1), Is.EqualTo(Team.Bravo));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void PlayerPrefabMovesWithCharacterController()
        {
            var bootstrap = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .Single();
            var prefab = bootstrap.PlayerPrefab;

            Assert.That(prefab.GetComponent<NetworkPlayerMotor>(), Is.Not.Null,
                "NetPlayer has no NetworkPlayerMotor; run Fps.Editor.NetSceneBuilder.Build");
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);

            // CharacterController と別のコライダーがあると自分自身にぶつかる
            Assert.That(prefab.GetComponents<Collider>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void PlayerPrefabHasDisabledFirstPersonViewAtEyeHeight()
        {
            var prefab = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetBootstrap>(true))
                .Single()
                .PlayerPrefab;

            var view = prefab.transform.Find(NetPlayer.ViewName);
            Assert.That(view, Is.Not.Null, "NetPlayer has no pitch pivot; run Fps.Editor.NetSceneBuilder.Build");
            Assert.That(view.parent, Is.EqualTo(prefab.transform), "yaw turns the body and pitch turns this child");
            Assert.That(view.localPosition, Is.EqualTo(new Vector3(0f, NetPlayer.EyeHeight, 0f)),
                "the camera is the shot origin, which the server checks with NetPlayer.EyeHeight");

            var camera = view.GetComponent<Camera>();
            var listener = view.GetComponent<AudioListener>();
            var look = prefab.GetComponent<Fps.Runtime.FirstPersonLook>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(listener, Is.Not.Null);
            Assert.That(look, Is.Not.Null);
            Assert.That(Fps.Runtime.FirstPersonLook.FindChildCamera(prefab.transform), Is.EqualTo(camera));

            // 他人のプレイヤーで映ったり音を拾ったりしないよう、NetPlayer が自分のプレイヤーでだけ有効にする
            Assert.That(camera.enabled, Is.False);
            Assert.That(listener.enabled, Is.False);
            Assert.That(look.enabled, Is.False);
        }

        [Test]
        public void HasSpectatorCameraForWhenNoPlayerIsLocal()
        {
            var spectators = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetSpectatorCamera>(true))
                .ToArray();

            Assert.That(spectators, Has.Length.EqualTo(1));
            Assert.That(spectators[0].GetComponent<Camera>(), Is.Not.Null);
            Assert.That(spectators[0].GetComponent<Camera>().enabled, Is.True);
        }

        [Test]
        public void HasNoNetworkManagerSavedInScene()
        {
            // 構成はコードで行うので、シーンに保存された NetworkManager があれば二重になる
            var managers = _scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<NetworkManager>(true));

            Assert.That(managers, Is.Empty);
        }

        [Test]
        public void IsInBuildSettings()
        {
            var entry = EditorBuildSettings.scenes.FirstOrDefault(s => s.path == NetBootstrap.ScenePath);

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.enabled, Is.True);
        }
    }
}
