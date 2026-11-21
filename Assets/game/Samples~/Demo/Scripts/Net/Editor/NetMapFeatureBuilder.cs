using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LadderComponent = Shooter.Runtime.Traversal.Ladder;
using WaterVolume = Shooter.Runtime.Traversal.Water;

namespace Shooter.Editor
{
    // サーバーのはしご・水・乗り物の書き出し (MapCollisionExporter) はシーンの部品から読むので、マップの JSON の置き方をシーンの部品にする。
    public static class NetMapFeatureBuilder
    {
        public const string LogTag = "[Net] NetMapFeatureBuilder:";

        public const string TerrainRootName = "MapFeatures";
        public const string VehiclesRootName = "Vehicles";
        public const string WaterVolumeName = "Water";
        public const string WaterSurfaceName = "Surface";

        public const string CarKind = "car";
        public const string ParkedKind = "parked";

        // はしごの前に立ってつかむ場所 (LadderRangeFixture.ApproachDepth と同じ) も塔の占める場所に数える。
        public const float LadderApproachDepth = LadderRangeFixture.ApproachDepth;

        // 乗り物の横と後ろの降りる位置 (CarFixtureBuilder の Exit_*) が壁や塔に埋まらないよう、車体のまわりに空ける。
        public const float VehicleClearance = 1.2f;

        // 外周の壁の内側に、人が回り込める幅を残す。
        public const float FloorMargin = 1f;

        public const float WaterSurfaceThickness = 0.1f;

        private static readonly Dictionary<string, VehicleKind> _vehicleKinds = new Dictionary<string, VehicleKind>(StringComparer.Ordinal)
        {
            [CarKind] = new VehicleKind(
                (scene, parent, name, position, rotation) => CarFixtureBuilder.Create(scene, parent, name, position, rotation),
                CarFixtureBuilder.BodyBounds),
            [ParkedKind] = new VehicleKind(
                (scene, parent, name, position, rotation) => VehicleFixtureBuilder.Create(scene, parent, name, position, rotation),
                VehicleFixtureBuilder.BodyBounds),
        };

        public static IReadOnlyList<string> VehicleKinds => _vehicleKinds.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

        public static string LayoutPath(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))
            {
                throw new ArgumentException("a map name is needed", nameof(mapName));
            }

            return ShooterKitRoot.OutputPath("Maps/" + mapName + NetMapFeatureLayout.FileExtension);
        }

        // ゲーム側で書き換えた置き方を残すよう、既にあれば中身は変えない (ExtractionSceneBuilder.LoadOrBuildLootTuning と同じ)。
        public static NetMapFeatureLayout LoadOrBuild(string path, NetMapFeatureLayout defaults)
        {
            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            path = SceneBuildHelpers.ValidateAssetPath(path, ".json");
            var existing = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (existing != null)
            {
                return NetMapFeatureLayout.Parse(existing.text, path);
            }

            var json = defaults.ToJson();
            var layout = NetMapFeatureLayout.Parse(json, path);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path));
            File.WriteAllText(FullPath(path), json);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(path) == null)
            {
                throw new IOException(LogTag + " failed to import " + path);
            }

            Debug.Log(LogTag + " saved " + path + " (" + layout.LadderTowers.Count + " ladder tower(s), "
                + layout.WaterPools.Count + " water pool(s), " + layout.Vehicles.Count + " vehicle(s))");
            return layout;
        }

        public static Bounds Footprint(NetMapLadderTower tower)
        {
            return LadderTowerBuilder.Footprint(tower.BaseCenter, tower.Size, tower.SideDirection, LadderApproachDepth);
        }

        public static Bounds Footprint(NetMapWaterPool pool)
        {
            return pool.Footprint;
        }

        public static Bounds Footprint(NetMapVehicleSpot vehicle)
        {
            var body = Kind(vehicle).Body(vehicle.Position, vehicle.Rotation);
            body.Expand(new Vector3(VehicleClearance * 2f, 0f, VehicleClearance * 2f));
            return body;
        }

        public static bool IsVehicleKind(string kind)
        {
            return kind != null && _vehicleKinds.ContainsKey(kind);
        }

        // シーンを作って NavMesh を焼いてから止まらないよう、置く前に床・壁・ほかの物と重ならないかを確かめる。
        public static List<string> Problems(
            NetMapFeatureLayout layout,
            float halfExtent,
            IEnumerable<(string Name, Vector3 Center, Vector3 Size)> obstacles,
            IEnumerable<(string Name, Vector3 Position, float Radius)> keepClear,
            IEnumerable<(string Name, Vector3 From, Vector3 To, float Radius)> keepClearSegments = null)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (!(halfExtent > FloorMargin) || float.IsInfinity(halfExtent))
            {
                throw new ArgumentOutOfRangeException(nameof(halfExtent), halfExtent, "the floor must be wider than the margin");
            }

            var problems = new List<string>();
            var footprints = new List<(string Name, Bounds Bounds)>();
            foreach (var tower in layout.LadderTowers)
            {
                footprints.Add((tower.Name, Footprint(tower)));
            }

            foreach (var pool in layout.WaterPools)
            {
                footprints.Add((pool.Name, Footprint(pool)));
            }

            foreach (var vehicle in layout.Vehicles)
            {
                if (!IsVehicleKind(vehicle.Kind))
                {
                    problems.Add(vehicle.Name + " has the vehicle kind '" + vehicle.Kind + "'; use one of " + string.Join(", ", VehicleKinds));
                    continue;
                }

                footprints.Add((vehicle.Name, Footprint(vehicle)));
            }

            foreach (var group in footprints.GroupBy(f => f.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                problems.Add("the name '" + group.Key + "' is used " + group.Count() + " times");
            }

            var inside = halfExtent - FloorMargin;
            var obstacleList = obstacles?.ToList() ?? new List<(string, Vector3, Vector3)>();
            var keepClearList = keepClear?.ToList() ?? new List<(string, Vector3, float)>();
            var segmentList = keepClearSegments?.ToList() ?? new List<(string, Vector3, Vector3, float)>();
            for (var i = 0; i < footprints.Count; i++)
            {
                var (name, bounds) = footprints[i];
                if (bounds.min.x < -inside || bounds.max.x > inside || bounds.min.z < -inside || bounds.max.z > inside)
                {
                    problems.Add(name + " reaches past the floor (keep it " + FloorMargin + " m inside " + halfExtent + " m)");
                }

                foreach (var (obstacleName, center, size) in obstacleList)
                {
                    if (OverlapsHorizontally(bounds, new Bounds(center, size)))
                    {
                        problems.Add(name + " overlaps " + obstacleName);
                    }
                }

                foreach (var (pointName, position, radius) in keepClearList)
                {
                    if (HorizontalDistance(position, bounds) < radius)
                    {
                        problems.Add(name + " covers " + pointName);
                    }
                }

                foreach (var (segmentName, from, to, radius) in segmentList)
                {
                    if (HorizontalDistance(from, to, bounds) < radius)
                    {
                        problems.Add(name + " covers " + segmentName);
                    }
                }

                for (var j = i + 1; j < footprints.Count; j++)
                {
                    if (OverlapsHorizontally(bounds, footprints[j].Bounds))
                    {
                        problems.Add(name + " overlaps " + footprints[j].Name);
                    }
                }
            }

            return problems;
        }

        // 塔の足場は動かない当たりなので、NavMesh を焼く前に置く。水は当たりを持たないが、塔と一緒に置いて置き方の根を一つにする。
        public static GameObject PlaceTerrain(Scene scene, Transform parent, NetMapFeatureLayout layout)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var root = CreateRoot(scene, parent, TerrainRootName);
            try
            {
                foreach (var tower in layout.LadderTowers)
                {
                    var created = LadderTowerBuilder.Create(scene, tower.Name, tower.BaseCenter, tower.Size, tower.SideDirection, tower.LadderWidth);
                    created.transform.SetParent(root.transform, true);
                }

                foreach (var pool in layout.WaterPools)
                {
                    CreateWaterPool(root.transform, pool);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        // 乗り物の当たりで NavMesh に穴を開けないよう、焼いた後に置く。動かせる物の下の床も Bot が歩けるようにするため。
        public static GameObject PlaceVehicles(Scene scene, Transform parent, NetMapFeatureLayout layout)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var root = CreateRoot(scene, parent, VehiclesRootName);
            try
            {
                foreach (var spot in layout.Vehicles)
                {
                    Kind(spot).Create(scene, root.transform, spot.Name, spot.Position, spot.Rotation);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        // 一つでもあれば、ゲームが置き直したものとみなして足さない (ServerDataMaps.AddFlagBasesIfMissing と同じ)。
        public static bool HasFeatures(Scene scene)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            return scene.GetRootGameObjects().Any(root =>
                root.GetComponentsInChildren<LadderComponent>(true).Length > 0
                || root.GetComponentsInChildren<WaterVolume>(true).Length > 0
                || root.GetComponentsInChildren<Vehicle>(true).Length > 0);
        }

        // 子だけを集める NavMeshSurface は、その下に置かないと塔の足場を焼かない。
        public static Transform TerrainParent(NavMeshSurface surface)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            return surface.collectObjects == CollectObjects.Children ? surface.transform : null;
        }

        public static WaterVolume CreateWaterPool(Transform parent, NetMapWaterPool pool)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var poolObject = new GameObject(pool.Name);
            poolObject.transform.SetParent(parent, false);
            poolObject.transform.SetPositionAndRotation(pool.GroundCenter, Quaternion.identity);

            var water = new GameObject(WaterVolumeName).AddComponent<WaterVolume>();
            water.transform.SetParent(poolObject.transform, false);
            water.Size = pool.Size;
            water.SurfaceHeight = pool.SurfaceHeight;

            // 水面の板は見た目だけの目印なので当たりを外す。残すと、人が泳げず板の上に立ててしまい、サーバーの当たりにも入るため。
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = WaterSurfaceName;
            UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetParent(poolObject.transform, false);
            surface.transform.localPosition = new Vector3(0f, pool.SurfaceHeight - (WaterSurfaceThickness / 2f), 0f);
            surface.transform.localScale = new Vector3(pool.Size.x, WaterSurfaceThickness, pool.Size.z);
            return water;
        }

        private static VehicleKind Kind(NetMapVehicleSpot spot)
        {
            if (spot.Kind == null || !_vehicleKinds.TryGetValue(spot.Kind, out var kind))
            {
                throw new InvalidOperationException(LogTag + " " + spot.Name + " has the vehicle kind '" + spot.Kind + "'; use one of "
                    + string.Join(", ", VehicleKinds));
            }

            return kind;
        }

        private static GameObject CreateRoot(Scene scene, Transform parent, string name)
        {
            var root = new GameObject(name);
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }
            else
            {
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return root;
        }

        private static bool OverlapsHorizontally(Bounds a, Bounds b)
        {
            return a.min.x < b.max.x && a.max.x > b.min.x && a.min.z < b.max.z && a.max.z > b.min.z;
        }

        private static float HorizontalDistance(Vector3 point, Bounds bounds)
        {
            var dx = Mathf.Max(0f, bounds.min.x - point.x, point.x - bounds.max.x);
            var dz = Mathf.Max(0f, bounds.min.z - point.z, point.z - bounds.max.z);
            return new Vector2(dx, dz).magnitude;
        }

        // 線分が長方形を貫くときは端点も角も遠いことがあるので、先に交差を見て、交わらなければ端点と角からの最短を取る。
        private static float HorizontalDistance(Vector3 from, Vector3 to, Bounds bounds)
        {
            var a = new Vector2(from.x, from.z);
            var b = new Vector2(to.x, to.z);
            var min = new Vector2(bounds.min.x, bounds.min.z);
            var max = new Vector2(bounds.max.x, bounds.max.z);
            var d = b - a;
            var t0 = 0f;
            var t1 = 1f;
            for (var axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(d[axis]) < 1e-6f)
                {
                    if (a[axis] < min[axis] || a[axis] > max[axis])
                    {
                        t0 = 2f;
                    }

                    continue;
                }

                var ta = (min[axis] - a[axis]) / d[axis];
                var tb = (max[axis] - a[axis]) / d[axis];
                t0 = Mathf.Max(t0, Mathf.Min(ta, tb));
                t1 = Mathf.Min(t1, Mathf.Max(ta, tb));
            }

            if (t0 <= t1)
            {
                return 0f;
            }

            var best = Mathf.Min(HorizontalDistance(from, bounds), HorizontalDistance(to, bounds));
            var corners = new[] { min, new Vector2(min.x, max.y), max, new Vector2(max.x, min.y) };
            foreach (var corner in corners)
            {
                var t = d.sqrMagnitude < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(corner - a, d) / d.sqrMagnitude);
                best = Mathf.Min(best, (corner - (a + d * t)).magnitude);
            }

            return best;
        }

        private static string FullPath(string assetPath)
        {
            var project = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(project ?? string.Empty, assetPath);
        }

        private readonly struct VehicleKind
        {
            public VehicleKind(Func<Scene, Transform, string, Vector3, Quaternion, Vehicle> create, Func<Vector3, Quaternion, Bounds> body)
            {
                Create = create;
                Body = body;
            }

            public Func<Scene, Transform, string, Vector3, Quaternion, Vehicle> Create { get; }

            public Func<Vector3, Quaternion, Bounds> Body { get; }
        }
    }
}
