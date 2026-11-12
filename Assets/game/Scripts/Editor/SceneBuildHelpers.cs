using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// シーン生成スクリプト (<see cref="EnemyTestSceneBuilder"/>、<see cref="MissionTestSceneBuilder"/>、
    /// <see cref="CampaignSceneBuilder"/>) に共通の処理。パスの検査、フォルダの作成、Build Settings への追加、
    /// 地形と NavMesh、目標の区域、敵の生成をまとめる。どれも呼び出し元の出力を変えないよう、元の実装をそのまま移した。
    /// </summary>
    public static class SceneBuildHelpers
    {
        /// <summary>NavMeshAgent の既定のエージェント (Humanoid)。</summary>
        public const int HumanoidAgentTypeId = 0;

        public const float EnemyAcceleration = 8f;

        /// <summary>Assets/ 以下の .asset であることを確かめ、区切りを / に揃えて返す。</summary>
        public static string ValidateAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("asset path must not be empty", nameof(path));
            }

            var normalized = path.Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException($"asset path '{path}' must be under Assets/", nameof(path));
            }

            if (!normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"asset path '{path}' must end with .asset", nameof(path));
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

        /// <summary>path のフォルダを (親も含めて) 作る。既にあれば何もしない。</summary>
        public static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            var guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            if (string.IsNullOrEmpty(guid))
            {
                throw new IOException($"Failed to create the folder '{path}'");
            }
        }

        /// <summary>
        /// Build Settings のシーン一覧に path を有効な状態で加える。既にあれば位置を変えずに有効にし (重複は落とす)、
        /// 他のシーンは順番と有効/無効をそのまま残す。
        /// </summary>
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

        /// <summary>scene が読み込み済みの有効なシーンであることを確かめる。</summary>
        public static void RequireLoaded(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a valid, loaded scene", nameof(scene));
            }
        }

        /// <summary>scene が読み込み済みで、まだ何も置かれていないことを確かめる。what は例外の文に出す生成物の名前。</summary>
        public static void RequireEmpty(Scene scene, string what)
        {
            RequireLoaded(scene);

            if (scene.rootCount != 0)
            {
                throw new InvalidOperationException(
                    $"scene '{scene.name}' already has {scene.rootCount} root object(s); {what} must be built into an empty scene");
            }
        }

        /// <summary>
        /// path の MissionDefinition を configure で上書きする。無ければ作る。既存のアセットは GUID を保つので、
        /// それを参照しているシーンは壊れない。configure は不正な定義なら例外を投げ、そのときは保存しない。
        /// </summary>
        public static MissionDefinition CreateOrUpdateMissionDefinition(string path, Action<MissionDefinition> configure)
        {
            return CreateOrUpdateAsset(path, configure);
        }

        /// <summary>
        /// path の ScriptableObject (型 T) を configure で上書きする。無ければ作る。既存のアセットは GUID を保つので、
        /// それを参照しているシーンやアセットは壊れない。configure は不正な中身なら例外を投げ、そのときは保存しない
        /// (新しく作ったインスタンスは捨てる)。configure は値を検査してから書き込むと、既存のアセットのメモリ上の中身も汚さない。
        /// path に別の型のアセットがあれば例外を投げる。
        /// </summary>
        public static T CreateOrUpdateAsset<T>(string path, Action<T> configure)
            where T : ScriptableObject
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            path = ValidateAssetPath(path);

            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is T))
            {
                throw new InvalidOperationException(
                    $"'{path}' is a {existing.GetType().Name}, not a {typeof(T).Name}; move it away first");
            }

            var asset = (T)existing;
            var created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                asset.name = Path.GetFileNameWithoutExtension(path);
            }

            try
            {
                configure(asset);
            }
            catch
            {
                if (created)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }

                throw;
            }

            if (created)
            {
                EnsureFolder(Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// owner に Humanoid 用の NavMeshSurface を付けて返す。surface は collectObjects に従って集めた地形の
        /// 物理コライダーから焼く。焼くのは <see cref="BakeNavMesh(NavMeshSurface)"/>。
        /// </summary>
        public static NavMeshSurface AddNavMeshSurface(GameObject owner, CollectObjects collectObjects)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var surface = owner.AddComponent<NavMeshSurface>();
            surface.agentTypeID = HumanoidAgentTypeId;
            surface.collectObjects = collectObjects;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return surface;
        }

        /// <summary>動かない Plane の床を置く。parent が null ならシーンの直下に置く。</summary>
        public static GameObject CreateStaticPlane(Scene scene, Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = name;
            plane.transform.SetPositionAndRotation(position, Quaternion.identity);
            plane.transform.localScale = scale;
            plane.isStatic = true;
            Place(scene, parent, plane);
            return plane;
        }

        /// <summary>動かない直方体 (壁・遮蔽物) を置く。center は中心、size は大きさ。parent が null ならシーンの直下に置く。</summary>
        public static GameObject CreateStaticBox(Scene scene, Transform parent, string name, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetPositionAndRotation(center, Quaternion.identity);
            box.transform.localScale = size;
            box.isStatic = true;
            Place(scene, parent, box);
            return box;
        }

        /// <summary>
        /// surface の NavMesh を焼いて (アセットには保存せず) 返す。焼いた NavMesh はすぐ NavMesh のクエリに使える。
        /// 保存は <see cref="SaveNavMeshData"/>。
        /// </summary>
        public static NavMeshData BakeNavMesh(NavMeshSurface surface)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            surface.BuildNavMesh();
            var data = surface.navMeshData;
            if (data == null)
            {
                throw new InvalidOperationException($"NavMeshSurface on '{surface.name}' produced no NavMesh data");
            }

            return data;
        }

        /// <summary>surface の焼いた NavMeshData を assetPath に保存する (既にあれば置き換える)。</summary>
        public static NavMeshData SaveNavMeshData(NavMeshSurface surface, string assetPath)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            assetPath = ValidateAssetPath(assetPath);

            var data = surface.navMeshData;
            if (data == null)
            {
                throw new InvalidOperationException($"NavMeshSurface on '{surface.name}' has no baked NavMesh data to save");
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
        /// 焼いた NavMesh に points (足元) がすべて乗っていることを確かめる。乗っていない点があれば、
        /// what (例外の文に出す点の種類) を添えて例外を投げる。
        /// </summary>
        public static void RequireOnNavMesh(IEnumerable<Vector3> points, float radius, string what)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            foreach (var point in points)
            {
                if (!NavMesh.SamplePosition(point, out _, radius, NavMesh.AllAreas))
                {
                    throw new InvalidOperationException(
                        $"{what} {point} is not on the baked NavMesh; check the floor and wall layout");
                }
            }
        }

        /// <summary>
        /// 焼いた NavMesh の上で from から points (足元) のそれぞれへ歩いて行けることを確かめる。
        /// 行けない点があれば、what を添えて例外を投げる。
        /// </summary>
        public static void RequireReachable(Vector3 from, IEnumerable<Vector3> points, float radius, string what)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (!NavMesh.SamplePosition(from, out var start, radius, NavMesh.AllAreas))
            {
                throw new InvalidOperationException($"start {from} is not on the baked NavMesh; check the floor and wall layout");
            }

            var path = new NavMeshPath();
            foreach (var point in points)
            {
                if (!NavMesh.SamplePosition(point, out var end, radius, NavMesh.AllAreas)
                    || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete)
                {
                    throw new InvalidOperationException(
                        $"{what} {point} cannot be reached from {from} on the baked NavMesh; check the wall layout");
                }
            }
        }

        /// <summary>
        /// 床に置く薄い板の目印。BoxCollider はトリガーにし、板の上に triggerHeight の高さまで伸ばす。
        /// CharacterController が確実にトリガーを通知するよう、動かない kinematic の Rigidbody を付ける。
        /// position は板の底面の中心 (床の高さ)。シーンにはまだ移さない。
        /// </summary>
        public static GameObject CreateAreaPad(string name, Vector3 position, Vector3 padScale, float triggerHeight)
        {
            if (!(padScale.y > 0f) || !(triggerHeight >= padScale.y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(triggerHeight), triggerHeight, "the pad must be thicker than 0 and the trigger at least as tall as the pad");
            }

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = name;
            pad.transform.SetPositionAndRotation(position + new Vector3(0f, padScale.y / 2f, 0f), Quaternion.identity);
            pad.transform.localScale = padScale;

            var collider = pad.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            var height = triggerHeight / padScale.y;
            collider.size = new Vector3(1f, height, 1f);
            collider.center = new Vector3(0f, (height - 1f) / 2f, 0f);

            var body = pad.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return pad;
        }

        /// <summary>
        /// カプセルの敵 (<see cref="EnemyAgent"/>、NavMeshAgent、Damageable、胴の Hitbox) を feet (足元) に置く。
        /// patrolRoute が空なら Idle のまま、その場で待つ。parent が null ならシーンの直下に置く。
        /// </summary>
        public static EnemyAgent CreateEnemy(
            Scene scene,
            Transform parent,
            string name,
            string id,
            Vector3 feet,
            Quaternion rotation,
            IReadOnlyList<Vector3> patrolRoute,
            EnemyBody body)
        {
            if (patrolRoute == null)
            {
                throw new ArgumentNullException(nameof(patrolRoute));
            }

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = name;
            Place(scene, parent, capsule);

            // カプセルの中心が Transform の位置なので、足元が地面に付くよう半分だけ上げる。
            var spawn = feet + (Vector3.up * (body.Height / 2f));
            capsule.transform.SetPositionAndRotation(spawn, rotation);

            var navAgent = capsule.AddComponent<NavMeshAgent>();
            navAgent.agentTypeID = HumanoidAgentTypeId;
            navAgent.baseOffset = body.Height / 2f;
            navAgent.height = body.Height;
            navAgent.radius = body.Radius;
            navAgent.speed = body.Speed;
            navAgent.angularSpeed = EnemyAgent.DefaultTurnSpeed;
            navAgent.acceleration = EnemyAcceleration;
            navAgent.stoppingDistance = 0f;
            navAgent.autoBraking = true;

            var damageable = capsule.AddComponent<Damageable>();
            damageable.Initialize(id, body.MaxHealth);

            // プリミティブ付属の CapsuleCollider を胴の当たり判定にする。
            capsule.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);

            var agent = capsule.AddComponent<EnemyAgent>();
            agent.Id = id;
            agent.SetPatrolRoute(patrolRoute);
            return agent;
        }

        /// <summary>from から to を水平に向く回転。同じ位置なら +Z を向く。</summary>
        public static Quaternion FacingTowards(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
        }

        private static void Place(Scene scene, Transform parent, GameObject gameObject)
        {
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, true);
            }
            else
            {
                SceneManager.MoveGameObjectToScene(gameObject, scene);
            }
        }
    }

    /// <summary>敵の体の大きさ・速さ・体力。</summary>
    public readonly struct EnemyBody
    {
        public float Height { get; }
        public float Radius { get; }
        public float Speed { get; }
        public int MaxHealth { get; }

        public EnemyBody(float height, float radius, float speed, int maxHealth)
        {
            if (!(height > 0f) || !(radius > 0f) || !(speed > 0f) || maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "height, radius, speed and max health must be positive");
            }

            Height = height;
            Radius = radius;
            Speed = speed;
            MaxHealth = maxHealth;
        }
    }
}
