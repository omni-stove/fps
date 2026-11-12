using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Net;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// ネットワークの動作確認用シーン NetTest とプレイヤーのプレハブを生成し、ビルド設定に加える。
    /// 何度実行しても同じ結果になる。batchmode では
    /// <c>-batchmode -quit -projectPath . -executeMethod Fps.Editor.NetSceneBuilder.Build</c> で呼ぶ。
    /// </summary>
    public static class NetSceneBuilder
    {
        public const string PlayerPrefabPath = "Assets/game/Prefabs/Net/NetPlayer.prefab";

        /// <summary>プレイヤーのカプセルの高さと半径 (m)。プリミティブの Capsule と同じ大きさ。</summary>
        public const float PlayerHeight = 2f;
        public const float PlayerRadius = 0.5f;

        /// <summary>床 (Plane は 10m 四方) の拡大率。歩き回っても落ちにくい 50m 四方にする。</summary>
        public static readonly Vector3 GroundScale = new Vector3(5f, 1f, 5f);

        /// <summary>床の高さ。カプセルの中心が原点のとき足元が床に接する。</summary>
        public const float GroundY = -PlayerHeight / 2f;

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

            var prefab = BuildPlayerPrefab();
            BuildScene(prefab);
            AddSceneToBuildSettings(NetBootstrap.ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Net] NetSceneBuilder: wrote " + PlayerPrefabPath + " and " + NetBootstrap.ScenePath);
        }

        private static GameObject BuildPlayerPrefab()
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(PlayerPrefabPath));

            var source = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                source.name = "NetPlayer";
                source.AddComponent<NetworkObject>();

                // CharacterController 自体がカプセルのコライダーなので、プリミティブ付属のコライダーは二重になる
                UnityEngine.Object.DestroyImmediate(source.GetComponent<CapsuleCollider>());
                var controller = source.AddComponent<CharacterController>();
                controller.height = PlayerHeight;
                controller.radius = PlayerRadius;
                controller.center = Vector3.zero;
                source.AddComponent<NetworkPlayerMotor>();
                var saved = PrefabUtility.SaveAsPrefabAsset(source, PlayerPrefabPath, out var success);
                if (!success || saved == null)
                {
                    throw new InvalidOperationException("failed to save " + PlayerPrefabPath);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }

            // 読み込み時の OnValidate で GlobalObjectIdHash が埋まるので、それをディスクへ書き戻す
            AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null || prefab.GetComponent<NetworkObject>() == null)
            {
                throw new InvalidOperationException(PlayerPrefabPath + " has no NetworkObject after import");
            }

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssetIfDirty(prefab);
            return prefab;
        }

        private static void BuildScene(GameObject playerPrefab)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(NetBootstrap.ScenePath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrapObject = new GameObject("NetBootstrap");
            var bootstrap = bootstrapObject.AddComponent<NetBootstrap>();
            bootstrap.PlayerPrefab = playerPrefab;

            // 画面ありで起動したときにカプセルが見えるようにするだけの最小限の構成
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 4f, -8f), Quaternion.Euler(25f, 0f, 0f));

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, GroundY, 0f);
            ground.transform.localScale = GroundScale;

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
