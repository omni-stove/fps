using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public static class SceneBuildHelpers
    {
        public const int HumanoidAgentTypeId = 0;

        public const float EnemyAcceleration = 8f;

        private static readonly List<Action<GameObject>> _enemySteps = new List<Action<GameObject>>();

        public static string ValidateAssetPath(string path)
        {
            return ValidateAssetPath(path, ".asset");
        }

        public static string ValidateAssetPath(string path, string extension)
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

            if (!normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"asset path '{path}' must end with {extension}", nameof(path));
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

        public static void RequireLoaded(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a valid, loaded scene", nameof(scene));
            }
        }

        public static void RequireEmpty(Scene scene, string what)
        {
            RequireLoaded(scene);

            if (scene.rootCount != 0)
            {
                throw new InvalidOperationException(
                    $"scene '{scene.name}' already has {scene.rootCount} root object(s); {what} must be built into an empty scene");
            }
        }

        // 値の検査 (WeaponDefinition.ToSpec) は読み込み時 (WeaponAssetBuilder.LoadPlayerLoadout) に済んでいるので、ここでは並びだけを見る。
        public static void RequirePlayerWeapons(IReadOnlyList<WeaponDefinition> weapons, string parameterName)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (weapons.Count == 0)
            {
                throw new ArgumentException("the player needs at least one weapon", parameterName);
            }

            for (var i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null)
                {
                    throw new ArgumentException($"player weapon #{i} is missing", parameterName);
                }
            }
        }

        // 本体が型を知らない部品 (隠密など) を、それを持つ側が敵に付けられるようにする。CreateEnemy は登録の順に当てる。
        public static IReadOnlyList<Action<GameObject>> EnemySteps
        {
            get
            {
                lock (_enemySteps)
                {
                    return _enemySteps.ToArray();
                }
            }
        }

        // [InitializeOnLoadMethod] から呼ばれるので、同じ処理の二度目の登録は足さない。
        public static void RegisterEnemyStep(Action<GameObject> step)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            lock (_enemySteps)
            {
                if (!_enemySteps.Contains(step))
                {
                    _enemySteps.Add(step);
                }
            }
        }

        public static bool UnregisterEnemyStep(Action<GameObject> step)
        {
            if (step == null)
            {
                return false;
            }

            lock (_enemySteps)
            {
                return _enemySteps.Remove(step);
            }
        }

        // 既存のアセットは GUID を保つので、それを参照しているシーンやアセットは壊れない。
        // configure が例外を投げても保存はしないが、既存のアセットのメモリ上の中身は書き換わるので、configure は値を検査してから書き込む。
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

        public const string PerimeterName = "Perimeter";

        public static readonly IReadOnlyList<string> PerimeterWallNames = new[]
        {
            "Perimeter_North",
            "Perimeter_South",
            "Perimeter_East",
            "Perimeter_West",
        };

        // 二段ジャンプ・壁走り・グラップルでも越えられない高さにする。
        public const float DefaultPerimeterHeight = 8f;

        public const float DefaultPerimeterThickness = 1f;

        // 床の縁の内側に壁の内面を合わせ、角は東西の壁を南北の壁の厚み分伸ばして隙間なく塞ぐ。
        public static GameObject CreatePerimeterWalls(Scene scene, Transform parent, Vector3 center, Vector2 halfExtents)
        {
            return CreatePerimeterWalls(scene, parent, center, halfExtents, DefaultPerimeterHeight, DefaultPerimeterThickness);
        }

        public static GameObject CreatePerimeterWalls(
            Scene scene, Transform parent, Vector3 center, Vector2 halfExtents, float height, float thickness)
        {
            if (halfExtents.x <= 0f || halfExtents.y <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(halfExtents), halfExtents, "the walled area must have a positive size");
            }

            if (height <= 0f || thickness <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(height), $"height {height} and thickness {thickness} must be positive");
            }

            var root = new GameObject(PerimeterName);
            root.isStatic = true;
            Place(scene, parent, root);
            root.transform.position = center;

            var y = height / 2f;
            var spanX = (halfExtents.x + thickness) * 2f;
            var spanZ = halfExtents.y * 2f;
            var boxes = new[]
            {
                (Center: new Vector3(0f, y, halfExtents.y + thickness / 2f), Size: new Vector3(spanX, height, thickness)),
                (Center: new Vector3(0f, y, -halfExtents.y - thickness / 2f), Size: new Vector3(spanX, height, thickness)),
                (Center: new Vector3(halfExtents.x + thickness / 2f, y, 0f), Size: new Vector3(thickness, height, spanZ)),
                (Center: new Vector3(-halfExtents.x - thickness / 2f, y, 0f), Size: new Vector3(thickness, height, spanZ)),
            };

            for (var i = 0; i < boxes.Length; i++)
            {
                var wall = CreateStaticBox(scene, root.transform, PerimeterWallNames[i], center + boxes[i].Center, boxes[i].Size);
                SetSurfaceMaterial(wall, SurfaceMaterial.DefaultId);
            }

            return root;
        }

        // Unity の Plane は拡大 1 で 10m 四方。
        public static Vector2 PlaneHalfExtents(Vector3 planeScale)
        {
            return new Vector2(planeScale.x * 5f, planeScale.z * 5f);
        }

        public static SurfaceMaterial SetSurfaceMaterial(GameObject target, string materialId)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!target.TryGetComponent<SurfaceMaterial>(out var surface))
            {
                surface = target.AddComponent<SurfaceMaterial>();
            }

            surface.Id = materialId;
            return surface;
        }

        public static string SurfaceIdOf(IReadOnlyDictionary<string, string> ids, string name)
        {
            return ids != null && name != null && ids.TryGetValue(name, out var id) ? SurfaceMaterial.Normalize(id) : SurfaceMaterial.DefaultId;
        }

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

        // CharacterController が確実にトリガーを通知するよう、動かない kinematic の Rigidbody を付ける。
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

        public static EnemyAgent CreateEnemy(
            Scene scene,
            Transform parent,
            string name,
            string id,
            Vector3 feet,
            Quaternion rotation,
            IReadOnlyList<Vector3> patrolRoute,
            EnemyBody body,
            WeaponDefinition weapon)
        {
            if (patrolRoute == null)
            {
                throw new ArgumentNullException(nameof(patrolRoute));
            }

            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
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
            agent.WeaponDefinition = weapon;
            agent.SetPatrolRoute(patrolRoute);

            foreach (var step in EnemySteps)
            {
                step(capsule);
            }

            // 敵は手榴弾を投げない。演出だけを付ける。
            CharacterComponents.Attach(capsule, throwsGrenades: false);
            return agent;
        }

        public static CoverPointRegistry CreateCoverPoints(
            Scene scene, Transform parent, string rootName, IReadOnlyList<CoverPointPlacement> placements)
        {
            if (placements == null)
            {
                throw new ArgumentNullException(nameof(placements));
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var placement in placements)
            {
                if (!names.Add(placement.Name))
                {
                    throw new ArgumentException($"cover point name '{placement.Name}' is used twice", nameof(placements));
                }
            }

            var root = new GameObject(rootName);
            Place(scene, parent, root);
            var registry = root.AddComponent<CoverPointRegistry>();

            var points = new Shooter.Runtime.CoverPoint[placements.Count];
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                var marker = new GameObject(placement.Name);
                marker.transform.SetParent(root.transform, false);
                marker.transform.SetPositionAndRotation(placement.Position, FacingTowards(Vector3.zero, placement.Facing));

                points[i] = marker.AddComponent<Shooter.Runtime.CoverPoint>();
                points[i].Height = placement.Height;
                points[i].SetPeekOffsetWorld(placement.PeekOffset);
            }

            registry.SetPoints(points);
            return registry;
        }

        public static List<Vector3> CoverStandPoints(IEnumerable<CoverPointPlacement> placements)
        {
            if (placements == null)
            {
                throw new ArgumentNullException(nameof(placements));
            }

            var points = new List<Vector3>();
            foreach (var placement in placements)
            {
                points.Add(placement.Position);
                points.Add(placement.PeekPosition);
            }

            return points;
        }

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

    public readonly struct CoverPointPlacement
    {
        public string Name { get; }

        public Vector3 Position { get; }

        public Vector3 Facing { get; }

        public CoverHeight Height { get; }

        public Vector3 PeekOffset { get; }

        public Vector3 PeekPosition => Position + PeekOffset;

        public CoverPointPlacement(string name, Vector3 position, Vector3 facing, CoverHeight height, Vector3 peekOffset)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("cover point needs a name", nameof(name));
            }

            if (new Vector2(facing.x, facing.z).sqrMagnitude < 1e-6f)
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, $"cover point '{name}' must face horizontally");
            }

            Name = name;
            Position = position;
            Facing = facing;
            Height = height;
            PeekOffset = peekOffset;
        }

        public static CoverPointPlacement Low(string name, Vector3 position, Vector3 facing)
        {
            return new CoverPointPlacement(name, position, facing, CoverHeight.Low, Vector3.zero);
        }

        public static CoverPointPlacement High(string name, Vector3 position, Vector3 facing, Vector3 peekOffset)
        {
            return new CoverPointPlacement(name, position, facing, CoverHeight.High, peekOffset);
        }
    }
}
