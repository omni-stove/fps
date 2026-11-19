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
    // シーンは 2 つにまとめる。
    // Playground: ボタンを押せばすぐ試せる機能を全部入れ、TPS の射撃場の置く物 (ITpsRangeFixture) を射撃場と同じ位置に置く (射撃場で重ならないよう配置済み)。
    // 射撃場に無い移動の置く物 (傾き・泳ぎ・壁走り・二段ジャンプ) は、敵の視界に入らないよう出現位置の後ろ (-Z) に一列に並べる。
    // HighDrop: 出現位置を高い台の上にし、始めから飛び降りてパラシュートと落下ダメージを試せるようにする。
    // どちらも床の外へ落ちないよう、床の縁に壁を立てる。
    public static class FeatureSceneBuilder
    {
        public const string MenuPath = SceneBuildSteps.MenuPath;
        public const string LogTag = "[FeatureSceneBuilder]";

        public const string BootstrapObjectName = "GameBootstrap";
        public const string FloorName = GameBootstrap.FloorName;

        public const string PlaygroundName = "Playground";
        public const string HighDropName = "HighDrop";

        // 前に機能ごとに分けていたシーンの名前の頭。組み直すときに残っていれば消す。
        public const string LegacyScenePrefix = "Feature_";

        public const float FloorMargin = 6f;

        // 置く物が無くても、出現位置と標的ダミーの列 (z = 10) と回避・グレネードの分の床は残す。
        public const float MinFloorHalfExtent = 15f;

        public const float FloorStep = 5f;

        public const float StationGap = 4f;

        public const float TraversalRowStartX = -30f;

        // 台の上 (出現位置) から地面までの高さ。開かずに落ちると MotorSpec.DefaultLethalFallSpeed を超えて倒れ、
        // ParachuteMotion.DefaultMinOpenHeight より十分高いので、端から落ちて少し下がってからでも開ける。
        public const float HighDropHeight = 20f;

        // 開いて流される分 (ParachuteTowerFixture と同じく 20m ほど) を台の縁から先に空ける。
        public const float HighDropMinFloorHalfExtent = 30f;

        public static IReadOnlyList<FeatureScene> All()
        {
            return new[] { Playground(), HighDrop() };
        }

        public static FeatureScene Playground()
        {
            return new FeatureScene(
                PlaygroundName, GameRulesAssetBuilder.PlaygroundRules(), PlaygroundStations(), 0f, MinFloorHalfExtent, usesTraversalTuning: true);
        }

        public static FeatureScene HighDrop()
        {
            return new FeatureScene(
                HighDropName, GameRulesAssetBuilder.HighDropRules(), HighDropStations(), -HighDropHeight, HighDropMinFloorHalfExtent, usesTraversalTuning: false);
        }

        public static IReadOnlyList<FeatureStation> PlaygroundStations()
        {
            var stations = new List<FeatureStation>
            {
                FeatureStation.InPlace(new FeatureCoversFixture()),
                FeatureStation.InPlace(new FeatureVaultWallFixture()),
                new FeatureStation(new FeatureHutFixture(), TpsRangeSceneBuilder.HutCenter),
                FeatureStation.InPlace(new LadderRangeFixture()),
                FeatureStation.InPlace(new MeleeRangeFixture()),
                FeatureStation.InPlace(new StaggerRangeFixture()),
                FeatureStation.InPlace(new SlideSlopeFixture()),
                FeatureStation.InPlace(new MantleLedgesFixture()),
                FeatureStation.InPlace(new ProneCrawlwayFixture()),
                FeatureStation.InPlace(new ZiplineRangeFixture()),
                FeatureStation.InPlace(new StealthRangeFixture()),
                FeatureStation.InPlace(new SuppressionRangeFixture()),
                FeatureStation.InPlace(new PingRangeFixture()),
                FeatureStation.InPlace(new GrappleWallFixture()),
            };

            var nearestZ = float.MaxValue;
            foreach (var station in stations)
            {
                nearestZ = Mathf.Min(nearestZ, station.Footprint.min.z);
            }

            var traversal = new List<ITpsRangeFixture>();
            foreach (var feature in TraversalFeatureSceneBuilder.Features)
            {
                traversal.Add(TraversalFeatureSceneBuilder.Fixture(feature));
            }

            stations.AddRange(Row(traversal, TraversalRowStartX, nearestZ - StationGap));
            return stations;
        }

        public static IReadOnlyList<FeatureStation> HighDropStations()
        {
            return new[] { FeatureStation.InPlace(new FeatureDropTowerFixture(HighDropHeight)) };
        }

        // 奥 (+Z) の端を backZ にそろえ、startX から +X へ StationGap ずつ空けて並べる。
        public static IReadOnlyList<FeatureStation> Row(IReadOnlyList<ITpsRangeFixture> fixtures, float startX, float backZ)
        {
            if (fixtures == null)
            {
                throw new ArgumentNullException(nameof(fixtures));
            }

            var stations = new List<FeatureStation>();
            var x = startX;
            foreach (var fixture in fixtures)
            {
                var footprint = fixture.Footprint;
                stations.Add(new FeatureStation(fixture, new Vector3(x - footprint.min.x, 0f, backZ - footprint.max.z)));
                x += footprint.size.x + StationGap;
            }

            return stations;
        }

        public static void Build()
        {
            // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            foreach (var feature in All())
            {
                BuildAt(feature, BuildSceneList.FeatureScene(feature.Name), GameRulesAssetBuilder.FeatureRulesPath(feature.Name));
            }

            new DownedSceneBuilder().BuildScene();
            RemoveLegacy();
        }

        public static void BuildAt(FeatureScene feature, string scenePath, string rulesPath)
        {
            if (feature == null)
            {
                throw new ArgumentNullException(nameof(feature));
            }

            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            var weapons = TpsRangeSceneBuilder.LoadPlayerWeapons();
            var rules = GameRulesAssetBuilder.LoadOrBuildFeatureRules(rulesPath, feature.Rules);

            // 能力の枠は TPS の射撃場と同じくグラップルと展開式の盾。使えるかはルールの Abilities で決まる。
            var grapple = AbilityAssetBuilder.LoadOrBuildGrappleAbility();
            var shield = AbilityAssetBuilder.LoadOrBuildShieldAbility();

            var motorSettings = feature.UsesTraversalTuning
                ? TraversalFeatureSceneBuilder.LoadOrBuildMotorSettings(TraversalFeatureSceneBuilder.MotorSettingsPath)
                : null;
            var traversal = feature.UsesTraversalTuning
                ? TraversalFeatureSceneBuilder.LoadOrBuildTraversalTuning(TraversalFeatureSceneBuilder.TraversalTuningPath)
                : null;

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            var bootstrap = PopulateScene(scene, weapons, rules, feature);
            bootstrap.AbilityDefinitions = new AbilityDefinition[] { grapple, shield };
            bootstrap.MotorSettings = motorSettings;
            bootstrap.TraversalTuningJson = traversal;

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the {feature.Name} feature scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogTag} saved '{scenePath}' for {feature.Name} and registered it in Build Settings");
        }

        // 置く物の前に GameBootstrap を置くのは、敵を置く物がその武器を引くため。
        public static GameBootstrap PopulateScene(
            Scene scene, IReadOnlyList<WeaponDefinition> weapons, GameRulesDefinition rules, FeatureScene feature)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the feature scene");
            SceneBuildHelpers.RequirePlayerWeapons(weapons, nameof(weapons));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (feature == null)
            {
                throw new ArgumentNullException(nameof(feature));
            }

            RequireUniqueNames(feature.Stations);

            // 不正なルールを配線しないよう、何か置く前に検査する。
            var gameRules = rules.ToRules();

            var created = new List<GameObject>();
            try
            {
                // MatchRunner は Start で同じ GameObject の GameBootstrap が Awake で作ったものを拾うので、ここでの配線は要らない。
                var bootstrapObject = new GameObject(BootstrapObjectName, typeof(GameBootstrap), typeof(MatchRunner));
                created.Add(bootstrapObject);
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
                var bootstrap = bootstrapObject.GetComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = weapons;
                bootstrap.RulesDefinition = rules;

                var area = FloorArea(feature.Stations, feature.MinFloorHalfExtent);
                var center = new Vector3(area.center.x, feature.FloorY, area.center.y);
                created.Add(SceneBuildHelpers.CreateStaticPlane(scene, null, FloorName, center, new Vector3(area.width / 10f, 1f, area.height / 10f)));
                created.Add(SceneBuildHelpers.CreatePerimeterWalls(
                    scene,
                    null,
                    center,
                    area.size / 2f,
                    PerimeterHeight(feature.Stations, feature.FloorY),
                    SceneBuildHelpers.DefaultPerimeterThickness));

                foreach (var station in feature.Stations)
                {
                    created.Add(PlaceStation(scene, station, gameRules));
                }

                EditorSceneManager.MarkSceneDirty(scene);
                return bootstrap;
            }
            catch
            {
                foreach (var gameObject in created)
                {
                    if (gameObject != null)
                    {
                        UnityEngine.Object.DestroyImmediate(gameObject);
                    }
                }

                throw;
            }
        }

        // 出現位置を中心にした最小の広さに、置く物それぞれの周りの余白を足し、FloorStep の倍数へ外向きにそろえる (x と z の矩形)。
        public static Rect FloorArea(IReadOnlyList<FeatureStation> stations, float minHalfExtent)
        {
            if (stations == null)
            {
                throw new ArgumentNullException(nameof(stations));
            }

            var min = new Vector2(-minHalfExtent, -minHalfExtent);
            var max = new Vector2(minHalfExtent, minHalfExtent);
            foreach (var station in stations)
            {
                var bounds = station.Footprint;
                min = Vector2.Min(min, new Vector2(bounds.min.x - FloorMargin, bounds.min.z - FloorMargin));
                max = Vector2.Max(max, new Vector2(bounds.max.x + FloorMargin, bounds.max.z + FloorMargin));
            }

            min = new Vector2(Mathf.Floor(min.x / FloorStep) * FloorStep, Mathf.Floor(min.y / FloorStep) * FloorStep);
            max = new Vector2(Mathf.Ceil(max.x / FloorStep) * FloorStep, Mathf.Ceil(max.y / FloorStep) * FloorStep);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // いちばん高い置く物の上から跳んでも越えられないよう、その上に既定の壁の高さを足す。
        public static float PerimeterHeight(IReadOnlyList<FeatureStation> stations, float floorY)
        {
            if (stations == null)
            {
                throw new ArgumentNullException(nameof(stations));
            }

            var top = floorY;
            foreach (var station in stations)
            {
                top = Mathf.Max(top, station.Footprint.max.y);
            }

            return top - floorY + SceneBuildHelpers.DefaultPerimeterHeight;
        }

        // 前に機能ごとに分けていたシーン・ルール・調整値が残っていれば消し、Build Settings からも外す。
        public static IReadOnlyList<string> LegacyAssetPaths()
        {
            var paths = new List<string>();
            foreach (var field in GameRulesDebugDraft.Fields)
            {
                if (GameRulesDebugDraft.IsToggle(field))
                {
                    paths.Add(ShooterKitRoot.OutputScene(LegacyScenePrefix + field));
                    paths.Add(GameRulesAssetBuilder.FeatureRulesPath(field.ToString()));
                }
            }

            foreach (var feature in TraversalFeatureSceneBuilder.Features)
            {
                paths.Add(ShooterKitRoot.OutputScene(LegacyScenePrefix + feature));
                paths.Add(GameRulesAssetBuilder.FeatureRulesPath(feature.ToString()));
            }

            paths.AddRange(TraversalFeatureSceneBuilder.LegacyAssetPaths);
            return paths;
        }

        public static void RemoveLegacy()
        {
            var removed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in LegacyAssetPaths())
            {
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)) && AssetDatabase.DeleteAsset(path))
                {
                    removed.Add(path);
                }
            }

            EditorBuildSettings.scenes = BuildSceneList.Without(EditorBuildSettings.scenes, LegacyAssetPaths());
            AssetDatabase.SaveAssets();
            if (removed.Count > 0)
            {
                Debug.Log($"{LogTag} removed the old per-feature assets: {string.Join(", ", removed)}");
            }
        }

        private static void RequireUniqueNames(IReadOnlyList<FeatureStation> stations)
        {
            if (stations == null)
            {
                throw new ArgumentNullException(nameof(stations));
            }

            var names = new HashSet<string>(StringComparer.Ordinal)
            {
                BootstrapObjectName, FloorName, SceneBuildHelpers.PerimeterName,
            };
            foreach (var station in stations)
            {
                var name = station.Fixture.Name;
                if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
                {
                    throw new ArgumentException($"{LogTag} station '{name}' is empty or already taken in the feature scene", nameof(stations));
                }
            }
        }

        private static GameObject PlaceStation(Scene scene, FeatureStation station, GameRules rules)
        {
            var fixture = station.Fixture;
            var type = fixture.GetType().FullName;
            var placed = fixture.Place(scene, rules);
            if (placed == null)
            {
                throw new InvalidOperationException($"{LogTag} {type} placed nothing");
            }

            if (placed.scene != scene || placed.transform.parent != null || !string.Equals(placed.name, fixture.Name, StringComparison.Ordinal))
            {
                if (!EditorUtility.IsPersistent(placed))
                {
                    UnityEngine.Object.DestroyImmediate(placed);
                }

                throw new InvalidOperationException($"{LogTag} {type} must place '{fixture.Name}' at the root of the feature scene");
            }

            placed.transform.position += station.Offset;
            return placed;
        }
    }

    public sealed class FeatureScene
    {
        public FeatureScene(
            string name, GameRules rules, IReadOnlyList<FeatureStation> stations, float floorY, float minFloorHalfExtent, bool usesTraversalTuning)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name must not be empty", nameof(name));
            }

            Name = name;
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Stations = stations ?? throw new ArgumentNullException(nameof(stations));
            FloorY = floorY;
            MinFloorHalfExtent = minFloorHalfExtent;
            UsesTraversalTuning = usesTraversalTuning;
        }

        public string Name { get; }

        public GameRules Rules { get; }

        public IReadOnlyList<FeatureStation> Stations { get; }

        // 出現位置 (GameBootstrap.PlayerSpawn) は動かせないので、高い所から始めるときは床の方を下げる。
        public float FloorY { get; }

        public float MinFloorHalfExtent { get; }

        // 壁走りと二段ジャンプの調整値 (TraversalFeatureSceneBuilder) をこのシーンの GameBootstrap にだけ渡すか。
        public bool UsesTraversalTuning { get; }
    }

    public readonly struct FeatureStation
    {
        public FeatureStation(ITpsRangeFixture fixture, Vector3 offset)
        {
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            Offset = offset;
        }

        public static FeatureStation InPlace(ITpsRangeFixture fixture) => new FeatureStation(fixture, Vector3.zero);

        public ITpsRangeFixture Fixture { get; }

        public Vector3 Offset { get; }

        public Bounds Footprint
        {
            get
            {
                var bounds = Fixture.Footprint;
                bounds.center += Offset;
                return bounds;
            }
        }
    }

    // ここから下は Playground・HighDrop だけで使う置く物。public にすると TPS の射撃場の生成 (ExtensionPoints) が拾って射撃場にも置くので internal にする。

    // 出現位置 (原点) の下に立つ台。上面を y = 0 にそろえ、標的ダミーの列 (z = 10) まで載る広さにする。
    // 台の -X の縁の外に、落ちても無事な段と怪我をする段を置き、その間は地面まで落ちる。登り直せるよう +X の面にはしごを付ける。
    internal sealed class FeatureDropTowerFixture : ITpsRangeFixture
    {
        public const string FixtureName = "DropTower";
        public const string TowerName = "SpawnTower";
        public const string SafeLedgeName = "Ledge_Safe";
        public const string HurtLedgeName = "Ledge_Hurt";

        public static readonly Vector2 TopMin = new Vector2(-6f, -4f);
        public static readonly Vector2 TopMax = new Vector2(6f, 13f);

        // 既定の重力 (MotorSpec.DefaultGravity = 20) で落ちて着く速さが、MotorSpec.DefaultSafeFallSpeed (12 m/s) 未満になる高さ (約 11 m/s)。
        public const float SafeLedgeDrop = 3f;

        // 着く速さが安全な速さを超え、MotorSpec.DefaultLethalFallSpeed (25 m/s) には届かない高さ (約 18 m/s)。
        public const float HurtLedgeDrop = 8f;

        public const float LedgeWidth = 4f;
        public const float LedgeDepth = 6f;

        private readonly float _height;

        public FeatureDropTowerFixture(float height)
        {
            if (!(height > HurtLedgeDrop))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, $"the tower must be higher than the hurt ledge ({HurtLedgeDrop} m)");
            }

            _height = height;
        }

        public int Order => 0;

        public string Name => FixtureName;

        public Vector3 TowerBase => new Vector3((TopMin.x + TopMax.x) / 2f, -_height, (TopMin.y + TopMax.y) / 2f);

        public Vector3 TowerSize => new Vector3(TopMax.x - TopMin.x, _height, TopMax.y - TopMin.y);

        public LevelBox SafeLedge => Ledge(SafeLedgeName, TopMin.y, SafeLedgeDrop);

        public LevelBox HurtLedge => Ledge(HurtLedgeName, TopMax.y - LedgeDepth, HurtLedgeDrop);

        public Bounds Footprint
        {
            get
            {
                var bounds = LadderTowerBuilder.Footprint(TowerBase, TowerSize, Vector3.right, LadderRangeFixture.ApproachDepth);
                bounds.Encapsulate(new Bounds(SafeLedge.Center, SafeLedge.Size));
                bounds.Encapsulate(new Bounds(HurtLedge.Center, HurtLedge.Size));
                return bounds;
            }
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var tower = LadderTowerBuilder.Create(scene, TowerName, TowerBase, TowerSize, Vector3.right, LadderRangeFixture.LadderWidth);
                tower.transform.SetParent(root.transform, true);
                SceneBuildHelpers.SetSurfaceMaterial(tower, SurfaceImpactAssetStep.ConcreteId);
                foreach (var ledge in new[] { SafeLedge, HurtLedge })
                {
                    var box = SceneBuildHelpers.CreateStaticBox(scene, root.transform, ledge.Name, ledge.Center, ledge.Size);
                    SceneBuildHelpers.SetSurfaceMaterial(box, SurfaceImpactAssetStep.ConcreteId);
                }

                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private LevelBox Ledge(string name, float minZ, float drop)
        {
            var height = _height - drop;
            return new LevelBox(
                name,
                new Vector3(TopMin.x - (LedgeWidth / 2f), -_height + (height / 2f), minZ + (LedgeDepth / 2f)),
                new Vector3(LedgeWidth, height, LedgeDepth));
        }
    }

    internal sealed class FeatureHutFixture : ITpsRangeFixture
    {
        public const string FixtureName = "HutYard";

        public int Order => 0;

        public string Name => FixtureName;

        public Bounds Footprint
        {
            get
            {
                var size = new Vector3(
                    DestructionTestSceneBuilder.HutWidth * DestructionTestSceneBuilder.PieceSize,
                    (DestructionTestSceneBuilder.WallHeight + 1) * DestructionTestSceneBuilder.PieceSize,
                    DestructionTestSceneBuilder.HutDepth * DestructionTestSceneBuilder.PieceSize);
                return new Bounds(new Vector3(0f, size.y / 2f, 0f), size);
            }
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var pool = new GameObject(DestructionTestSceneBuilder.DebrisPoolName).AddComponent<DebrisPool>();
                pool.transform.SetParent(root.transform, false);

                // 小屋の部品は小屋の子として親から見た位置で置かれるので、親を動かせば形を保ったまま移せる。
                var hut = DestructionTestSceneBuilder.CreateHut(scene, rules.Destruction == Destruction.On);
                // CreateHut は破壊のテスト場の位置に置くので、親の真ん中 (Footprint の中心) へ戻す。
                hut.transform.SetParent(root.transform, false);
                hut.transform.localPosition = Vector3.zero;
                hut.DebrisPool = pool;
                SceneBuildHelpers.SetSurfaceMaterial(hut.gameObject, SurfaceImpactAssetStep.WoodId);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }
    }

    internal sealed class FeatureCoversFixture : ITpsRangeFixture
    {
        public int Order => 0;

        public string Name => BootstrapSceneBuilder.CoversName;

        public Bounds Footprint
        {
            get
            {
                var bounds = new Bounds(BootstrapSceneBuilder.Covers[0].Center, BootstrapSceneBuilder.Covers[0].Size);
                foreach (var cover in BootstrapSceneBuilder.Covers)
                {
                    bounds.Encapsulate(new Bounds(cover.Center, cover.Size));
                }

                return bounds;
            }
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(Name);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var cover in BootstrapSceneBuilder.Covers)
            {
                var box = SceneBuildHelpers.CreateStaticBox(scene, root.transform, cover.Name, cover.Center, cover.Size);
                SceneBuildHelpers.SetSurfaceMaterial(box, SceneBuildHelpers.SurfaceIdOf(BootstrapSceneBuilder.CoverSurfaceIds, cover.Name));
            }

            return root;
        }
    }

    // TPS の射撃場と同じ位置に置き、射撃場で重ならないよう配置済みのほかの置く物と並べられるようにする。
    internal sealed class FeatureVaultWallFixture : ITpsRangeFixture
    {
        public int Order => 0;

        public string Name => TpsRangeSceneBuilder.VaultWallName;

        public Bounds Footprint => new Bounds(TpsRangeSceneBuilder.VaultWall.Center, TpsRangeSceneBuilder.VaultWall.Size);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var wall = SceneBuildHelpers.CreateStaticBox(scene, null, Name, TpsRangeSceneBuilder.VaultWall.Center, TpsRangeSceneBuilder.VaultWall.Size);
            SceneBuildHelpers.SetSurfaceMaterial(wall, SurfaceImpactAssetStep.WoodId);
            return wall;
        }
    }
}
