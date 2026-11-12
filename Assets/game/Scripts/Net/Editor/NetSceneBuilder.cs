using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Net;
using Fps.Runtime;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// ネットワークの動作確認用シーン NetTest と、プレイヤー・試合の進行役のプレハブを生成し、ビルド設定に加える。
    /// 何度実行しても同じ結果になる。batchmode では
    /// <c>-batchmode -quit -projectPath . -executeMethod Fps.Editor.NetSceneBuilder.Build</c> で呼ぶ。
    /// </summary>
    public static class NetSceneBuilder
    {
        public const string PlayerPrefabPath = "Assets/game/Prefabs/Net/NetPlayer.prefab";
        public const string MatchPrefabPath = "Assets/game/Prefabs/Net/NetMatch.prefab";

        /// <summary>プレイヤーのカプセルの高さと半径 (m)。プリミティブの Capsule と同じ大きさ。</summary>
        public const float PlayerHeight = 2f;
        public const float PlayerRadius = 0.5f;

        /// <summary>一人称カメラの近クリップ (m)。カプセルの半径より十分小さくし、壁に寄っても突き抜けて見えにくくする。</summary>
        public const float ViewNearClip = 0.05f;

        /// <summary>床 (Plane は 10m 四方) の拡大率。両陣地と東西の地点が収まる 60m 四方にする。</summary>
        public static readonly Vector3 GroundScale = new Vector3(6f, 1f, 6f);

        /// <summary>床の高さ。カプセルの中心が y=0 のとき足元が床に接する。</summary>
        public const float GroundY = -PlayerHeight / 2f;

        /// <summary>
        /// 出現地点。チームごとの陣地を南北に 2 か所ずつ、どちらも使える地点を東西に 1 か所ずつ置く。
        /// カプセルの中心が y=0 に来るので、<see cref="GroundY"/> の床にちょうど立つ。
        /// </summary>
        public static readonly (string Name, Vector3 Position, NetSpawnPoint.SpawnTeam Team)[] SpawnPoints =
        {
            ("Spawn Alpha 1", new Vector3(-4f, 0f, -22f), NetSpawnPoint.SpawnTeam.Alpha),
            ("Spawn Alpha 2", new Vector3(4f, 0f, -22f), NetSpawnPoint.SpawnTeam.Alpha),
            ("Spawn Bravo 1", new Vector3(-4f, 0f, 22f), NetSpawnPoint.SpawnTeam.Bravo),
            ("Spawn Bravo 2", new Vector3(4f, 0f, 22f), NetSpawnPoint.SpawnTeam.Bravo),
            ("Spawn Any West", new Vector3(-22f, 0f, 0f), NetSpawnPoint.SpawnTeam.Any),
            ("Spawn Any East", new Vector3(22f, 0f, 0f), NetSpawnPoint.SpawnTeam.Any),
        };

        /// <summary>
        /// プレハブにするのは、NetworkObject の GlobalObjectIdHash がアセットの GUID からエディタでしか作られず、
        /// 実行時に生成したオブジェクトではサーバーとクライアントで同じプレハブだと突き合わせられないため。
        /// </summary>
        [MenuItem("Fps/Net/Build NetTest Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Net] NetSceneBuilder: cancelled");
                return;
            }

            var playerPrefab = BuildPrefab(PlayerPrefabPath, CreatePlayerSource);
            var matchPrefab = BuildPrefab(MatchPrefabPath, CreateMatchSource);
            BuildScene(playerPrefab, matchPrefab);
            AddSceneToBuildSettings(NetBootstrap.ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Net] NetSceneBuilder: wrote " + PlayerPrefabPath + ", " + MatchPrefabPath + " and " + NetBootstrap.ScenePath);
        }

        private static GameObject CreatePlayerSource()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            source.name = "NetPlayer";
            source.AddComponent<NetworkObject>();

            // 位置は NetworkPlayerMotor が同期する (リスポーンのテレポートも Motor 経由)。NetworkTransform は付けない。
            // CharacterController 自体がカプセルのコライダーなので、プリミティブ付属のコライダーは二重になる
            UnityEngine.Object.DestroyImmediate(source.GetComponent<CapsuleCollider>());
            var controller = source.AddComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.radius = PlayerRadius;
            controller.center = Vector3.zero;
            source.AddComponent<NetworkPlayerMotor>();

            AddFirstPersonView(source);

            source.AddComponent<NetPlayer>();
            return source;
        }

        /// <summary>
        /// 一人称視点を足す。pitch を回す子 (<see cref="NetPlayer.ViewName"/>) を目の高さに置いてカメラと AudioListener を付け、
        /// 本体に yaw を回す <see cref="FirstPersonLook"/> を付ける。どれも無効にしておき、
        /// 自分のプレイヤーでだけ <see cref="NetPlayer"/> が生成時に有効にする (他人のカメラが映ったり音を拾ったりしない)。
        /// </summary>
        private static void AddFirstPersonView(GameObject source)
        {
            var view = new GameObject(NetPlayer.ViewName) { tag = "MainCamera" };
            view.transform.SetParent(source.transform, false);
            view.transform.localPosition = new Vector3(0f, NetPlayer.EyeHeight, 0f);
            view.transform.localRotation = Quaternion.identity;

            var camera = view.AddComponent<Camera>();
            camera.nearClipPlane = ViewNearClip;
            camera.enabled = false;
            view.AddComponent<AudioListener>().enabled = false;

            // Edit Mode の AddComponent では Awake が走らないので、参照の解決は実行時の Awake に任せる
            source.AddComponent<FirstPersonLook>().enabled = false;
        }

        private static GameObject CreateMatchSource()
        {
            var source = new GameObject("NetMatch");
            source.AddComponent<NetworkObject>();
            source.AddComponent<NetTeamDeathmatch>();
            return source;
        }

        private static GameObject BuildPrefab(string path, Func<GameObject> createSource)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(path));

            var source = createSource();
            try
            {
                var saved = PrefabUtility.SaveAsPrefabAsset(source, path, out var success);
                if (!success || saved == null)
                {
                    throw new InvalidOperationException("failed to save " + path);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            // 読み込み時の OnValidate で GlobalObjectIdHash が埋まるので、それをディスクへ書き戻す
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<NetworkObject>() == null)
            {
                throw new InvalidOperationException(path + " has no NetworkObject after import");
            }

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssetIfDirty(prefab);
            return prefab;
        }

        private static void BuildScene(GameObject playerPrefab, GameObject matchPrefab)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(NetBootstrap.ScenePath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrapObject = new GameObject("NetBootstrap");
            var bootstrap = bootstrapObject.AddComponent<NetBootstrap>();
            bootstrap.PlayerPrefab = playerPrefab;
            bootstrap.MatchPrefab = matchPrefab;

            // 自分のプレイヤーが居ない間 (サーバーや接続前) の観戦用に、両陣地のカプセルが見えるよう上から見下ろす。
            // 自分のプレイヤーが生成されたら NetSpectatorCamera が止め、一人称カメラに替わる
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<NetSpectatorCamera>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 38f, -38f), Quaternion.Euler(45f, 0f, 0f));

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, GroundY, 0f);
            ground.transform.localScale = GroundScale;

            var spawnRoot = new GameObject("SpawnPoints");
            foreach (var (pointName, position, team) in SpawnPoints)
            {
                var pointObject = new GameObject(pointName);
                pointObject.transform.SetParent(spawnRoot.transform, false);
                pointObject.transform.localPosition = position;
                pointObject.AddComponent<NetSpawnPoint>().Team = team;
            }

            if (!EditorSceneManager.SaveScene(scene, NetBootstrap.ScenePath))
            {
                throw new InvalidOperationException("failed to save " + NetBootstrap.ScenePath);
            }
        }

        /// <summary>既存のシーンの並び (起動時の最初のシーン) は変えず、無ければ末尾に加える。</summary>
        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var existing = scenes.FindIndex(s => s.path == scenePath);
            if (existing >= 0)
            {
                if (scenes[existing].enabled)
                {
                    return;
                }

                scenes[existing] = new EditorBuildSettingsScene(scenePath, true);
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            var name = path.Split('/').Last();
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, name)))
            {
                throw new InvalidOperationException("failed to create folder " + path);
            }
        }
    }
}
