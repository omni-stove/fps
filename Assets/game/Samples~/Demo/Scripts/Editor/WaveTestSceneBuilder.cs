using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// PvE の波 (<see cref="WaveSpawner"/>) を試すシーン WaveTest を生成する。床・壁・低い壁は敵 AI のテストシーンと同じ
    /// (<see cref="EnemyTestSceneBuilder.CreateLevel"/>) で、NavMesh を焼いてシーンの隣に保存してから、
    /// <see cref="GameBootstrap"/> (床は生成しない設定)、無効にした敵の雛形、敵陣営の出現点 (<see cref="SideSpawnPoint"/>)、
    /// <see cref="WaveSpawner"/> を置く。プレイヤーとライトは実行時に GameBootstrap が作る。
    /// 何度実行しても同じ結果になる。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.WaveTestSceneBuilder.Build</code>
    /// </summary>
    /// <remarks>
    /// プレイヤーは原点で +Z を向き、z = 8 の壁の裏 (z = 16〜17) の 3 か所から敵が出て、プレイヤーへ向かってくる。
    /// 波は WaveSpawner の既定の式 (1 波目 3 体、波ごとに 2 体と強さ 0.25 倍ずつ増え、同時に 4 体まで、休憩 5 秒、5 波) で決める。
    /// </remarks>
    public static class WaveTestSceneBuilder
    {
        public static string ScenePath => BuildSceneList.WaveTestScene;

        /// <summary>焼いた NavMesh。Unity の慣習どおりシーン名のフォルダに置く。</summary>
        public static string NavMeshDataPath => ModeSceneBuilder.NavMeshDataPathFor(ScenePath);

        public const string MenuPath = "Fps/Build Wave Test Scene";
        public const string LogTag = "[WaveTestSceneBuilder]";

        public const string BootstrapObjectName = "GameBootstrap";
        public const string SpawnerObjectName = "WaveSpawner";
        public const string TemplateName = "EnemyTemplate";
        public const string TemplateId = "wave-template";

        /// <summary>敵に持たせる武器の Id。シーンを生成するときに武器の一覧から引く。</summary>
        public const string EnemyWeaponId = WeaponAssetBuilder.RifleId;

        /// <summary>敵の陣営。プレイヤーは <see cref="ModeRunnerBase"/> と同じく陣営 0 にする。</summary>
        public static readonly Side EnemySide = Side.Bravo;

        public static readonly Side PlayerSide = Side.Alpha;

        /// <summary>雛形を置く足元。無効なので動かないが、複製の初期位置にならないよう出現点から離しておく。</summary>
        public static readonly Vector3 TemplateFeet = new Vector3(0f, 0f, 19f);

        /// <summary>出現点の名前と足元。壁の裏の左・中・右。</summary>
        public static readonly IReadOnlyList<(string Name, Vector3 Feet)> SpawnPoints = new[]
        {
            ("WaveSpawn-West", new Vector3(-14f, 0f, 16f)),
            ("WaveSpawn-Center", new Vector3(0f, 0f, 17f)),
            ("WaveSpawn-East", new Vector3(14f, 0f, 16f)),
        };

        /// <summary>プレイヤーの出現位置 (GameBootstrap の既定)。出現点からここへ歩いて行けることを確かめる。</summary>
        public static readonly Vector3 PlayerFeet = Vector3.zero;

        public const float EnemyHeight = EnemyTestSceneBuilder.EnemyHeight;
        public const float EnemyRadius = EnemyTestSceneBuilder.EnemyRadius;
        public const float EnemySpeed = EnemyTestSceneBuilder.EnemySpeed;
        public const int EnemyMaxHealth = EnemyTestSceneBuilder.EnemyMaxHealth;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildAt(ScenePath, NavMeshDataPath);
        }

        /// <summary>
        /// シーンを生成して NavMesh を焼き、両方を保存して Build Settings に加える (既存の並びは変えない)。
        /// 出現点が NavMesh に乗っていないか、プレイヤーの位置へ歩いて行けなければ例外を投げる (batchmode では終了コード 1 になる)。
        /// </summary>
        public static void BuildAt(string scenePath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作ってから読む。
            var playerWeapons = WeaponAssetBuilder.LoadPlayerLoadout();
            var catalog = WeaponAssetBuilder.LoadCatalog();
            WeaponAssetBuilder.FindInCatalog(catalog, TemplateId, EnemyWeaponId);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 敵のコライダーで NavMesh に穴が空かないよう、地形だけを置いた状態で焼く。
            var surface = EnemyTestSceneBuilder.CreateLevel(scene);
            EnemyTestSceneBuilder.BakeNavMesh(surface, navMeshDataPath);
            var spawnFeet = new List<Vector3>(SpawnPoints.Count);
            foreach (var (_, feet) in SpawnPoints)
            {
                spawnFeet.Add(feet);
            }

            SceneBuildHelpers.RequireOnNavMesh(spawnFeet, EnemyTestSceneBuilder.NavMeshCheckRadius, "wave spawn point");
            SceneBuildHelpers.RequireReachable(PlayerFeet, spawnFeet, EnemyTestSceneBuilder.NavMeshCheckRadius, "wave spawn point");
            CreateActors(scene, playerWeapons, catalog);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the wave test scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{scenePath}' and '{navMeshDataPath}'");
        }

        /// <summary>
        /// GameBootstrap (床は作らない)、無効にした敵の雛形、敵陣営の出現点、<see cref="WaveSpawner"/> を置き、参照をすべてここで設定する。
        /// プレイヤーは playerWeapons を持ち、敵は enemyWeapons から <see cref="EnemyWeaponId"/> で引いた武器で撃つ。
        /// Id が enemyWeapons に無ければ何も置かずに <see cref="KeyNotFoundException"/> を投げる。
        /// </summary>
        public static WaveSpawner CreateActors(Scene scene, IReadOnlyList<WeaponDefinition> playerWeapons, WeaponCatalogAsset enemyWeapons)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            SceneBuildHelpers.RequirePlayerWeapons(playerWeapons, nameof(playerWeapons));

            if (enemyWeapons == null)
            {
                throw new ArgumentNullException(nameof(enemyWeapons));
            }

            var enemyWeapon = WeaponAssetBuilder.FindInCatalog(enemyWeapons, TemplateId, EnemyWeaponId);

            var bootstrapObject = new GameObject(BootstrapObjectName);
            SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.CreatesFloor = false;
            bootstrap.WeaponDefinitions = playerWeapons;

            var template = SceneBuildHelpers.CreateEnemy(
                scene,
                null,
                TemplateName,
                TemplateId,
                TemplateFeet,
                SceneBuildHelpers.FacingTowards(TemplateFeet, PlayerFeet),
                Array.Empty<Vector3>(),
                new EnemyBody(EnemyHeight, EnemyRadius, EnemySpeed, EnemyMaxHealth),
                enemyWeapon);
            template.gameObject.AddComponent<SideMember>().Side = EnemySide;
            template.gameObject.SetActive(false);

            var points = new SideSpawnPoint[SpawnPoints.Count];
            for (var i = 0; i < points.Length; i++)
            {
                var (pointName, feet) = SpawnPoints[i];
                var marker = new GameObject(pointName);
                SceneManager.MoveGameObjectToScene(marker, scene);
                marker.transform.SetPositionAndRotation(feet, SceneBuildHelpers.FacingTowards(feet, PlayerFeet));
                points[i] = marker.AddComponent<SideSpawnPoint>();
                points[i].Side = EnemySide;
            }

            var spawnerObject = new GameObject(SpawnerObjectName);
            SceneManager.MoveGameObjectToScene(spawnerObject, scene);
            var spawner = spawnerObject.AddComponent<WaveSpawner>();
            spawner.EnemyTemplate = template;
            spawner.SpawnPoints = points;
            spawner.EnemySide = EnemySide;
            spawner.PlayerSide = PlayerSide;

            EditorSceneManager.MarkSceneDirty(scene);
            return spawner;
        }
    }
}
