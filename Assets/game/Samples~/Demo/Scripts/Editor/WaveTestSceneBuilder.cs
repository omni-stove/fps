using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public static class WaveTestSceneBuilder
    {
        public static string ScenePath => BuildSceneList.WaveTestScene;

        // Unity の慣習どおり、焼いた NavMesh はシーン名のフォルダに置く。
        public static string NavMeshDataPath => ModeSceneBuilder.NavMeshDataPathFor(ScenePath);

        public const string MenuPath = SceneBuildSteps.MenuPath;
        public const string LogTag = "[WaveTestSceneBuilder]";

        public const string BootstrapObjectName = "GameBootstrap";
        public const string SpawnerObjectName = "WaveSpawner";
        public const string TemplateName = "EnemyTemplate";
        public const string TemplateId = "wave-template";

        public const string EnemyWeaponId = WeaponAssetBuilder.RifleId;

        // プレイヤーは ModeRunnerBase と同じく陣営 0 なので、敵はそれと別の陣営にする。
        public static readonly Side EnemySide = Side.Bravo;

        public static readonly Side PlayerSide = Side.Alpha;

        // 雛形は無効なので動かないが、複製の初期位置にならないよう出現点から離しておく。
        public static readonly Vector3 TemplateFeet = new Vector3(0f, 0f, 19f);

        public static readonly IReadOnlyList<(string Name, Vector3 Feet)> SpawnPoints = new[]
        {
            ("WaveSpawn-West", new Vector3(-14f, 0f, 16f)),
            ("WaveSpawn-Center", new Vector3(0f, 0f, 17f)),
            ("WaveSpawn-East", new Vector3(14f, 0f, 16f)),
        };

        public static readonly Vector3 PlayerFeet = Vector3.zero;

        public const float EnemyHeight = EnemyTestSceneBuilder.EnemyHeight;
        public const float EnemyRadius = EnemyTestSceneBuilder.EnemyRadius;
        public const float EnemySpeed = EnemyTestSceneBuilder.EnemySpeed;
        public const int EnemyMaxHealth = EnemyTestSceneBuilder.EnemyMaxHealth;

        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildAt(ScenePath, NavMeshDataPath);
        }

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
