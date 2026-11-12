using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
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
