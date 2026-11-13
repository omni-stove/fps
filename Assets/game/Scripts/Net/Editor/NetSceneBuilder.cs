using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
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

        /// <summary>
        /// ネット対戦のプレイヤーの視点。三人称のゲームにするならここを <see cref="ViewMode.ThirdPerson"/> にして Build し直す
        /// (当たり判定とサーバーの検査は目から行うので、視点を変えても他は変わらない)。
        /// </summary>
        public const ViewMode PlayerViewMode = ViewMode.FirstPerson;

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

            var loadout = LoadOrBuildLoadout();
            var rules = LoadOrBuildRules();
            var playerPrefab = BuildPrefab(PlayerPrefabPath, CreatePlayerSource);
            var matchPrefab = BuildPrefab(MatchPrefabPath, () => CreateMatchSource(loadout, rules));
            BuildScene(playerPrefab, matchPrefab);
            BuildSceneList.Register(NetBootstrap.ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Net] NetSceneBuilder: wrote " + PlayerPrefabPath + ", " + MatchPrefabPath + " and " + NetBootstrap.ScenePath
                + " (loadout " + WeaponAssetBuilder.LoadoutPath + ", rules " + GameRulesAssetBuilder.TeamDeathmatchPath + ")");
        }

        /// <summary>
        /// 試合の進行役に渡すチームデスマッチのルール (<see cref="GameRulesAssetBuilder.TeamDeathmatchPath"/>) を
        /// <see cref="GameRulesAssetBuilder.LoadRules"/> で読む。無ければ <see cref="GameRulesAssetBuilder.Build"/> で作る。
        /// サーバーの起動時と同じく <see cref="NetTeamDeathmatch.TryLoadRules"/> に通らなければ、プレハブを書かずに例外を投げる。
        /// </summary>
        public static GameRulesDefinition LoadOrBuildRules()
        {
            if (AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(GameRulesAssetBuilder.TeamDeathmatchPath) == null)
            {
                Debug.Log("[Net] NetSceneBuilder: " + GameRulesAssetBuilder.TeamDeathmatchPath + " is missing; running GameRulesAssetBuilder.Build");
                GameRulesAssetBuilder.Build();
            }

            GameRulesDefinition rules;
            try
            {
                rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.TeamDeathmatchPath);
            }
            catch (GameRulesDefinitionException e)
            {
                throw new InvalidOperationException(
                    "[Net] NetSceneBuilder: " + GameRulesAssetBuilder.TeamDeathmatchPath + " cannot be used by the match: " + e.Message, e);
            }

            if (!NetTeamDeathmatch.TryLoadRules(rules, out _, out _, out var error))
            {
                throw new InvalidOperationException(
                    "[Net] NetSceneBuilder: " + GameRulesAssetBuilder.TeamDeathmatchPath + " cannot be used by the match: " + error);
            }

            return rules;
        }

        /// <summary>
        /// 試合の進行役に渡すデフォルトロードアウト (<see cref="WeaponAssetBuilder.LoadoutPath"/>) を
        /// <see cref="WeaponAssetBuilder.LoadDefaultLoadout"/> で読む。無ければ <see cref="WeaponAssetBuilder.Build"/> と同じものを作る
        /// (既にあれば中身は上書きしない)。サーバーの起動時と同じく <see cref="NetTeamDeathmatch.TryCreateLoadout"/> に通らなければ、
        /// プレハブを書かずに例外を投げる。
        /// </summary>
        public static LoadoutDefinition LoadOrBuildLoadout()
        {
            if (AssetDatabase.LoadAssetAtPath<LoadoutDefinition>(WeaponAssetBuilder.LoadoutPath) == null)
            {
                Debug.Log("[Net] NetSceneBuilder: " + WeaponAssetBuilder.LoadoutPath + " is missing; running WeaponAssetBuilder.Build");
                WeaponAssetBuilder.Build();
            }

            var loadout = WeaponAssetBuilder.LoadDefaultLoadout();
            if (!NetTeamDeathmatch.TryCreateLoadout(loadout, out _, out var error))
            {
                throw new InvalidOperationException(
                    "[Net] NetSceneBuilder: " + WeaponAssetBuilder.LoadoutPath + " cannot be used by the match: " + error);
            }

            return loadout;
        }

        private static GameObject CreatePlayerSource()
        {
            var source = new GameObject("NetPlayer");
            source.AddComponent<NetworkObject>();

            // 見た目は子に置く。しゃがみで NetStance が見た目だけを縦に縮める (本体を縮めると目や CharacterController まで縮む)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = NetStance.BodyName;
            body.transform.SetParent(source.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            // 位置は NetworkPlayerMotor が同期する (リスポーンのテレポートも Motor 経由)。NetworkTransform は付けない。
            // CharacterController 自体がカプセルのコライダーなので、プリミティブ付属のコライダーは二重になる
            UnityEngine.Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            var controller = source.AddComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.radius = PlayerRadius;
            controller.center = Vector3.zero;
            source.AddComponent<NetworkPlayerMotor>();

            AddView(source);

            source.AddComponent<NetPlayer>();

            // やられ演出。ルールが Killer のときだけ、自分のプレイヤーで倒れてからリスポーンまでカメラを動かす
            source.AddComponent<NetDeathCamera>();

            // 弾倉・予備弾・リロード。弾数はサーバーが持ち、持ち主にだけ同期する
            source.AddComponent<NetPlayerWeapon>();

            // 演出と手榴弾。専用サーバーでは演出だけを HeadlessPresentation が止める
            CharacterComponents.Attach(source, throwsGrenades: true);
            return source;
        }

        /// <summary>
        /// 視点を足す。pitch を回す目 (<see cref="NetPlayer.ViewName"/>) を目の高さに置き、その子 (<see cref="NetPlayer.ViewCameraName"/>) に
        /// カメラと AudioListener を付け、本体に視点の土台 <see cref="ViewRig"/> (<see cref="PlayerViewMode"/>) と yaw を回す <see cref="FirstPersonLook"/>、
        /// 覗き込みの <see cref="AimDownSights"/> を付ける。
        /// どれも無効にしておき、自分のプレイヤーでだけ <see cref="NetPlayer"/> が生成時に有効にする (他人のカメラが映ったり音を拾ったりしない)。
        /// </summary>
        private static void AddView(GameObject source)
        {
            var view = new GameObject(NetPlayer.ViewName);
            view.transform.SetParent(source.transform, false);
            view.transform.localPosition = new Vector3(0f, NetPlayer.EyeHeight, 0f);
            view.transform.localRotation = Quaternion.identity;

            var cameraObject = new GameObject(NetPlayer.ViewCameraName) { tag = "MainCamera" };
            cameraObject.transform.SetParent(view.transform, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;

            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = ViewNearClip;
            camera.enabled = false;
            cameraObject.AddComponent<AudioListener>().enabled = false;

            // Edit Mode の AddComponent では Awake が走らないので、参照はここで渡す。入力の解決は実行時の Awake に任せる
            var rig = source.AddComponent<ViewRig>();
            rig.enabled = false;
            rig.Initialize(view.transform, camera);
            rig.Mode = PlayerViewMode;
            source.AddComponent<FirstPersonLook>().enabled = false;

            // 覗き込みもカメラを持つ自分のプレイヤーでだけ動かす。参照と入力の解決は実行時の Awake に任せる
            source.AddComponent<AimDownSights>().enabled = false;
        }

        private static GameObject CreateMatchSource(LoadoutDefinition loadout, GameRulesDefinition rules)
        {
            var source = new GameObject("NetMatch");
            source.AddComponent<NetworkObject>();

            // 武器とルールの値はプレハブに持たず、アセットへの参照だけを持つ (サーバーは試合の開始時に読む)
            var match = source.AddComponent<NetTeamDeathmatch>();
            match.Loadout = loadout;
            match.Rules = rules;

            // 射撃・ヒット・被弾・キルをクライアントへ配る。NetTeamDeathmatch が同じ GameObject から探す
            source.AddComponent<NetCombatFeed>();
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
