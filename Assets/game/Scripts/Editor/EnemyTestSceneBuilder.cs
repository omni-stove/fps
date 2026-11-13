using System;
using System.Collections.Generic;
using System.IO;
using Fps.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// 敵 AI の動作確認用シーン EnemyAiTest を生成する。床に <see cref="NavMeshSurface"/> を置いて NavMesh を焼き、
    /// NavMeshData をシーンの隣にアセットとして保存してから、敵 (<see cref="EnemyAgent"/>) と
    /// <see cref="GameBootstrap"/> (床は生成しない設定) を置く。プレイヤーとライトは実行時に GameBootstrap が作る。
    /// 何度実行しても同じ結果になる。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.EnemyTestSceneBuilder.Build</code>
    /// </summary>
    /// <remarks>
    /// 配置 (上から見て、プレイヤーは原点で +Z を向く):
    /// z = 8 に幅 18m (x = ±9) の壁があり、敵は壁の裏 (z = 14) を左右に巡回してから右手前 (14, 0, 2) へ出てくる。
    /// 右の列を手前へ下りて z ≒ 12 を過ぎたところでプレイヤーが視界 (20m) に入り、Chase → Attack になる。
    /// </remarks>
    public static class EnemyTestSceneBuilder
    {
        public const string ScenePath = "Assets/game/Scenes/EnemyAiTest.unity";

        /// <summary>焼いた NavMesh。Unity の慣習どおりシーン名のフォルダに置く。</summary>
        public const string NavMeshDataPath = "Assets/game/Scenes/EnemyAiTest/NavMesh-Floor.asset";

        public const string MenuPath = "Fps/Build Enemy AI Test Scene";
        public const string LogTag = "[EnemyTestSceneBuilder]";

        public const string FloorName = "Floor";
        public const string WallName = "Wall";
        public const string EnemyName = "Enemy-1";
        public const string EnemyId = "enemy-1";

        /// <summary>敵に持たせる武器の Id。シーンを生成するときに武器の一覧から引く。</summary>
        public const string EnemyWeaponId = WeaponAssetBuilder.RifleId;
        public const string BootstrapObjectName = "GameBootstrap";

        /// <summary>Plane は 10m 四方なので、4 倍で 40m 四方 (x, z とも ±20m)。</summary>
        public static readonly Vector3 FloorScale = new Vector3(4f, 1f, 4f);

        /// <summary>プレイヤー (原点) と巡回路の間を遮る壁。</summary>
        public static readonly Vector3 WallCenter = new Vector3(0f, 1.5f, 8f);

        public static readonly Vector3 WallSize = new Vector3(18f, 3f, 0.5f);

        /// <summary>巡回地点 (足元)。最初の地点に敵を置く。</summary>
        public static readonly Vector3[] PatrolRoute =
        {
            new Vector3(-14f, 0f, 14f),
            new Vector3(14f, 0f, 14f),
            new Vector3(14f, 0f, 2f),
        };

        public const float EnemyHeight = 2f;
        public const float EnemyRadius = 0.5f;
        public const float EnemySpeed = 3.5f;
        public const int EnemyMaxHealth = 100;

        /// <summary>巡回地点が NavMesh 上にあるかを確かめるときに探す半径 (メートル)。</summary>
        public const float NavMeshCheckRadius = 1f;

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
        /// 焼いた NavMesh に巡回地点が乗っていなければ例外を投げる (batchmode では終了コード 1 になる)。
        /// </summary>
        public static void BuildAt(string scenePath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            // 武器のアセットが無い・敵の武器がカタログに無いなら何も保存せずに止める (先に WeaponAssetBuilder.Build を走らせる)。
            var playerWeapons = WeaponAssetBuilder.LoadPlayerLoadout();
            var catalog = WeaponAssetBuilder.LoadCatalog();
            WeaponAssetBuilder.FindInCatalog(catalog, EnemyId, EnemyWeaponId);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 敵のコライダーで NavMesh に穴が空かないよう、地形だけを置いた状態で焼く。
            var surface = CreateLevel(scene);
            BakeNavMesh(surface, navMeshDataPath);
            SceneBuildHelpers.RequireOnNavMesh(PatrolRoute, NavMeshCheckRadius, "patrol point");
            CreateActors(scene, playerWeapons, catalog);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the enemy AI test scene to '{scenePath}'");
            }

            EditorBuildSettings.scenes = WithSceneAppended(EditorBuildSettings.scenes, scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{scenePath}' and '{navMeshDataPath}'");
        }

        /// <summary>
        /// 床 (NavMeshSurface 付き) と壁を空のシーンに置き、床の NavMeshSurface を返す。焼くのは <see cref="BakeNavMesh"/>。
        /// </summary>
        public static NavMeshSurface CreateLevel(Scene scene)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the level");

            var floor = SceneBuildHelpers.CreateStaticPlane(scene, null, FloorName, Vector3.zero, FloorScale);
            var surface = SceneBuildHelpers.AddNavMeshSurface(floor, CollectObjects.All);
            SceneBuildHelpers.CreateStaticBox(scene, null, WallName, WallCenter, WallSize);

            EditorSceneManager.MarkSceneDirty(scene);
            return surface;
        }

        /// <summary>
        /// surface の NavMesh を焼き、NavMeshData を assetPath に保存する (既にあれば置き換える)。
        /// </summary>
        public static NavMeshData BakeNavMesh(NavMeshSurface surface, string assetPath)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            assetPath = SceneBuildHelpers.ValidateAssetPath(assetPath);
            SceneBuildHelpers.BakeNavMesh(surface);
            return SceneBuildHelpers.SaveNavMeshData(surface, assetPath);
        }

        /// <summary>
        /// GameBootstrap (床は作らない) と敵 1 体を置く。敵は巡回路の最初の地点に立たせる。参照はすべてここで設定する。
        /// プレイヤーは playerWeapons (通常は <see cref="WeaponAssetBuilder.LoadPlayerLoadout"/> の rifle, handgun) を持ち、敵は enemyWeapons から
        /// <see cref="EnemyWeaponId"/> で引いた武器で撃つ。Id が enemyWeapons に無ければ何も置かずに
        /// <see cref="KeyNotFoundException"/> を投げる。
        /// </summary>
        public static EnemyAgent CreateActors(Scene scene, IReadOnlyList<WeaponDefinition> playerWeapons, WeaponCatalogAsset enemyWeapons)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            SceneBuildHelpers.RequirePlayerWeapons(playerWeapons, nameof(playerWeapons));

            if (enemyWeapons == null)
            {
                throw new ArgumentNullException(nameof(enemyWeapons));
            }

            var enemyWeapon = WeaponAssetBuilder.FindInCatalog(enemyWeapons, EnemyId, EnemyWeaponId);

            var bootstrapObject = new GameObject(BootstrapObjectName);
            SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.CreatesFloor = false;
            bootstrap.WeaponDefinitions = playerWeapons;

            var enemy = SceneBuildHelpers.CreateEnemy(
                scene,
                null,
                EnemyName,
                EnemyId,
                PatrolRoute[0],
                Quaternion.LookRotation(PatrolRoute[1] - PatrolRoute[0], Vector3.up),
                PatrolRoute,
                new EnemyBody(EnemyHeight, EnemyRadius, EnemySpeed, EnemyMaxHealth),
                enemyWeapon);
            EditorSceneManager.MarkSceneDirty(scene);
            return enemy;
        }

        /// <summary>Build Settings の末尾に path を有効な状態で加える。既にあれば位置はそのままで有効にする。</summary>
        public static EditorBuildSettingsScene[] WithSceneAppended(IReadOnlyList<EditorBuildSettingsScene> current, string path)
        {
            return SceneBuildHelpers.WithSceneAppended(current, path);
        }
    }
}
