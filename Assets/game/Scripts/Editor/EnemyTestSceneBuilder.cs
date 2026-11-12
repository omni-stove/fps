using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
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
            navMeshDataPath = ValidateAssetPath(navMeshDataPath);

            EnsureFolder(Path.GetDirectoryName(scenePath));
            EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 敵のコライダーで NavMesh に穴が空かないよう、地形だけを置いた状態で焼く。
            var surface = CreateLevel(scene);
            BakeNavMesh(surface, navMeshDataPath);
            RequireOnNavMesh(PatrolRoute);
            CreateActors(scene);

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
            RequireEmpty(scene);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = FloorName;
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.transform.localScale = FloorScale;
            floor.isStatic = true;
            SceneManager.MoveGameObjectToScene(floor, scene);

            var surface = floor.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0; // Humanoid (既定のエージェント)
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = WallName;
            wall.transform.SetPositionAndRotation(WallCenter, Quaternion.identity);
            wall.transform.localScale = WallSize;
            wall.isStatic = true;
            SceneManager.MoveGameObjectToScene(wall, scene);

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

            assetPath = ValidateAssetPath(assetPath);

            surface.BuildNavMesh();
            var data = surface.navMeshData;
            if (data == null)
            {
                throw new InvalidOperationException($"NavMeshSurface on '{surface.name}' produced no NavMesh data");
            }

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null && !AssetDatabase.DeleteAsset(assetPath))
            {
                throw new IOException($"Failed to replace the NavMesh data at '{assetPath}'");
            }

            AssetDatabase.CreateAsset(data, assetPath);
            EditorUtility.SetDirty(surface);
            return data;
        }

        /// <summary>
        /// GameBootstrap (床は作らない) と敵 1 体を置く。敵は巡回路の最初の地点に立たせる。参照はすべてここで設定する。
        /// </summary>
        public static EnemyAgent CreateActors(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a valid, loaded scene", nameof(scene));
            }

            var bootstrapObject = new GameObject(BootstrapObjectName);
            SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.CreatesFloor = false;

            var enemy = CreateEnemy(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            return enemy;
        }

        private static EnemyAgent CreateEnemy(Scene scene)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = EnemyName;
            SceneManager.MoveGameObjectToScene(body, scene);

            // カプセルの中心が Transform の位置なので、足元が地面に付くよう半分だけ上げる。
            var spawn = PatrolRoute[0] + (Vector3.up * (EnemyHeight / 2f));
            var facing = PatrolRoute[1] - PatrolRoute[0];
            body.transform.SetPositionAndRotation(spawn, Quaternion.LookRotation(facing, Vector3.up));

            var navAgent = body.AddComponent<NavMeshAgent>();
            navAgent.agentTypeID = 0;
            navAgent.baseOffset = EnemyHeight / 2f;
            navAgent.height = EnemyHeight;
            navAgent.radius = EnemyRadius;
            navAgent.speed = EnemySpeed;
            navAgent.angularSpeed = EnemyAgent.DefaultTurnSpeed;
            navAgent.acceleration = 8f;
            navAgent.stoppingDistance = 0f;
            navAgent.autoBraking = true;

            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(EnemyId, EnemyMaxHealth);

            // プリミティブ付属の CapsuleCollider を胴の当たり判定にする。
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);

            var agent = body.AddComponent<EnemyAgent>();
            agent.Id = EnemyId;
            agent.SetPatrolRoute(PatrolRoute);
            return agent;
        }

        /// <summary>Build Settings の末尾に path を有効な状態で加える。既にあれば位置はそのままで有効にする。</summary>
        public static EditorBuildSettingsScene[] WithSceneAppended(IReadOnlyList<EditorBuildSettingsScene> current, string path)
        {
            path = BootstrapSceneBuilder.ValidateScenePath(path);

            var scenes = new List<EditorBuildSettingsScene>();
            var found = false;
            if (current != null)
            {
                foreach (var scene in current)
                {
                    if (scene == null || string.IsNullOrEmpty(scene.path))
                    {
                        continue;
                    }

                    if (string.Equals(scene.path.Replace('\\', '/'), path, StringComparison.Ordinal))
                    {
                        if (found)
                        {
                            continue;
                        }

                        found = true;
                        scenes.Add(new EditorBuildSettingsScene(path, true));
                        continue;
                    }

                    scenes.Add(scene);
                }
            }

            if (!found)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            return scenes.ToArray();
        }

        private static void RequireOnNavMesh(IEnumerable<Vector3> points)
        {
            foreach (var point in points)
            {
                if (!NavMesh.SamplePosition(point, out _, NavMeshCheckRadius, NavMesh.AllAreas))
                {
                    throw new InvalidOperationException(
                        $"patrol point {point} is not on the baked NavMesh; check the floor and wall layout");
                }
            }
        }

        private static void RequireEmpty(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a valid, loaded scene", nameof(scene));
            }

            if (scene.rootCount != 0)
            {
                throw new InvalidOperationException(
                    $"scene '{scene.name}' already has {scene.rootCount} root object(s); the level must be built into an empty scene");
            }
        }

        private static string ValidateAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("asset path must not be empty", nameof(path));
            }

            var normalized = path.Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || !normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"asset path '{path}' must be a .asset under Assets/", nameof(path));
            }

            foreach (var segment in normalized.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new ArgumentException($"asset path '{path}' must not contain empty, '.' or '..' segments", nameof(path));
                }
            }

            return normalized;
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            var name = path.Substring(parent.Length + 1);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, name)))
            {
                throw new InvalidOperationException("failed to create folder " + path);
            }
        }
    }
}
