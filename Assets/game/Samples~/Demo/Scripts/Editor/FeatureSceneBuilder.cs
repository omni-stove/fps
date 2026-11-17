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
    // 試し場所は TPS の射撃場の置く物 (ITpsRangeFixture) をそのまま使い、射撃場の配置に意味があるもの以外は置いてから根ごと平行に移す。
    // 置く物は子と線の端 (ジップライン) を根から見た位置で持つので、移しても形は崩れない。
    public static class FeatureSceneBuilder
    {
        public const string MenuPath = "Shooter/Build Feature Scenes";
        public const string LogTag = "[FeatureSceneBuilder]";

        public const string BootstrapObjectName = "GameBootstrap";
        public const string FloorName = GameBootstrap.FloorName;

        // 標的ダミー (x = -4..4、半径 0.5) にかからないよう、その右に空ける。
        public const float BesideSpawnMinX = 6f;

        public const float BesideSpawnMinZ = 3f;

        public const float FloorMargin = 6f;

        // 試し場所が無くても、標的ダミーの列 (z = 10) と回避・グレネードの分の床は残す。
        public const float MinFloorHalfExtent = 15f;

        public const float FloorStep = 5f;

        public static IReadOnlyList<FeatureScene> All()
        {
            var scenes = new List<FeatureScene>();
            foreach (var field in GameRulesDebugDraft.Fields)
            {
                if (GameRulesDebugDraft.IsToggle(field))
                {
                    scenes.Add(new FeatureScene(field, Stations(field)));
                }
            }

            return scenes;
        }

        public static IReadOnlyList<FeatureStation> Stations(GameRulesDebugField field)
        {
            if (!GameRulesDebugDraft.IsToggle(field))
            {
                throw new ArgumentException($"{field} is not an on/off rule", nameof(field));
            }

            switch (field)
            {
                case GameRulesDebugField.Destruction:
                    return Beside(new FeatureHutFixture());
                case GameRulesDebugField.Downed:
                case GameRulesDebugField.Parachute:
                case GameRulesDebugField.FallDamage:
                    // ParachuteTowerFixture と同じ高さで、上がれるようはしごを付ける。
                    return Beside(new FeatureLadderTowerFixture(ParachuteTowerFixture.TowerHeight));
                case GameRulesDebugField.Ladder:
                    return Beside(new FeatureLadderTowerFixture(LadderRangeFixture.TowerSize.y));
                case GameRulesDebugField.Cover:
                case GameRulesDebugField.BlindFire:
                    return InPlace(new FeatureCoversFixture());
                case GameRulesDebugField.Vault:
                    return InPlace(new FeatureVaultWallFixture());
                case GameRulesDebugField.Melee:
                    return Beside(new MeleeRangeFixture());
                case GameRulesDebugField.Stagger:
                    return InPlace(new StaggerRangeFixture());
                case GameRulesDebugField.Slide:
                    return Beside(new SlideSlopeFixture());
                case GameRulesDebugField.Mantle:
                    return Beside(new MantleLedgesFixture());
                case GameRulesDebugField.Prone:
                    return Beside(new ProneCrawlwayFixture());
                case GameRulesDebugField.Zipline:
                    return Beside(new ZiplineRangeFixture());
                case GameRulesDebugField.Stealth:
                    return Beside(new StealthRangeFixture());
                case GameRulesDebugField.Suppression:
                    return Beside(new SuppressionRangeFixture());
                case GameRulesDebugField.Ping:
                    return Beside(new PingRangeFixture());
                case GameRulesDebugField.Abilities:
                    return Beside(new GrappleWallFixture());
                default:
                    return Array.Empty<FeatureStation>();
            }
        }

        [MenuItem(MenuPath)]
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
                BuildAt(feature, BuildSceneList.FeatureScene(feature.Field), GameRulesAssetBuilder.FeatureRulesPath(feature.Field));
            }
        }

        public static void BuildAt(FeatureScene feature, string scenePath, string rulesPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            var weapons = TpsRangeSceneBuilder.LoadPlayerWeapons();
            var rules = GameRulesAssetBuilder.LoadOrBuildFeatureRules(feature.Field, rulesPath);

            // 能力の枠は TPS の射撃場と同じくグラップルと展開式の盾。使えるかはルールの Abilities で決まり、能力のシーンだけ On。
            var grapple = AbilityAssetBuilder.LoadOrBuildGrappleAbility();
            var shield = AbilityAssetBuilder.LoadOrBuildShieldAbility();

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            var bootstrap = PopulateScene(scene, weapons, rules, feature.Stations);
            bootstrap.AbilityDefinitions = new AbilityDefinition[] { grapple, shield };

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the {feature.Field} feature scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogTag} saved '{scenePath}' for {feature.Field} and registered it in Build Settings");
        }

        // 置く物の前に GameBootstrap を置くのは、敵を置く物がその武器を引くため。
        public static GameBootstrap PopulateScene(
            Scene scene, IReadOnlyList<WeaponDefinition> weapons, GameRulesDefinition rules, IReadOnlyList<FeatureStation> stations)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the feature scene");
            SceneBuildHelpers.RequirePlayerWeapons(weapons, nameof(weapons));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            RequireUniqueNames(stations);

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

                var half = FloorHalfExtent(stations);
                created.Add(SceneBuildHelpers.CreateStaticPlane(scene, null, FloorName, Vector3.zero, FloorScale(half)));

                foreach (var station in stations)
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

        public static float FloorHalfExtent(IReadOnlyList<FeatureStation> stations)
        {
            if (stations == null)
            {
                throw new ArgumentNullException(nameof(stations));
            }

            var reach = 0f;
            foreach (var station in stations)
            {
                var bounds = station.Footprint;
                reach = Mathf.Max(reach, Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x), Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z));
            }

            var half = Mathf.Max(MinFloorHalfExtent, reach + FloorMargin);
            return Mathf.Ceil(half / FloorStep) * FloorStep;
        }

        public static Vector3 FloorScale(float half)
        {
            return new Vector3(half * 2f / 10f, 1f, half * 2f / 10f);
        }

        private static IReadOnlyList<FeatureStation> Beside(ITpsRangeFixture fixture)
        {
            return new[] { new FeatureStation(fixture, FeaturePlacement.BesideSpawn) };
        }

        private static IReadOnlyList<FeatureStation> InPlace(ITpsRangeFixture fixture)
        {
            return new[] { new FeatureStation(fixture, FeaturePlacement.InPlace) };
        }

        private static void RequireUniqueNames(IReadOnlyList<FeatureStation> stations)
        {
            if (stations == null)
            {
                throw new ArgumentNullException(nameof(stations));
            }

            var names = new HashSet<string>(StringComparer.Ordinal) { BootstrapObjectName, FloorName };
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
        public FeatureScene(GameRulesDebugField field, IReadOnlyList<FeatureStation> stations)
        {
            Field = field;
            Stations = stations ?? throw new ArgumentNullException(nameof(stations));
        }

        public GameRulesDebugField Field { get; }

        public IReadOnlyList<FeatureStation> Stations { get; }
    }

    public enum FeaturePlacement
    {
        InPlace,

        BesideSpawn,
    }

    public readonly struct FeatureStation
    {
        public FeatureStation(ITpsRangeFixture fixture, FeaturePlacement placement)
        {
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            Placement = placement;
        }

        public ITpsRangeFixture Fixture { get; }

        public FeaturePlacement Placement { get; }

        public Vector3 Offset
        {
            get
            {
                if (Placement == FeaturePlacement.InPlace)
                {
                    return Vector3.zero;
                }

                var footprint = Fixture.Footprint;
                return new Vector3(
                    FeatureSceneBuilder.BesideSpawnMinX - footprint.min.x, 0f, FeatureSceneBuilder.BesideSpawnMinZ - footprint.min.z);
            }
        }

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

    // ここから下は機能ごとのシーンだけで使う置く物。public にすると TPS の射撃場の生成 (ExtensionPoints) が拾って射撃場にも置くので internal にする。

    internal sealed class FeatureLadderTowerFixture : ITpsRangeFixture
    {
        public const string FixtureName = "LadderTower";

        private readonly float _height;

        public FeatureLadderTowerFixture(float height)
        {
            _height = height;
        }

        public int Order => 0;

        public string Name => FixtureName;

        public Bounds Footprint => LadderTowerBuilder.Footprint(Vector3.zero, Size, Vector3.left, LadderRangeFixture.ApproachDepth);

        private Vector3 Size => new Vector3(LadderRangeFixture.TowerSize.x, _height, LadderRangeFixture.TowerSize.z);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var tower = LadderTowerBuilder.Create(scene, FixtureName, Vector3.zero, Size, Vector3.left, LadderRangeFixture.LadderWidth);
            SceneBuildHelpers.SetSurfaceMaterial(tower, SurfaceImpactAssetStep.ConcreteId);
            return tower;
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
                hut.transform.SetParent(root.transform, false);
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

    internal sealed class FeatureVaultWallFixture : ITpsRangeFixture
    {
        public const float WallZ = 4f;

        public const float WallWidth = 4f;

        public int Order => 0;

        public string Name => TpsRangeSceneBuilder.VaultWallName;

        public Bounds Footprint => new Bounds(
            new Vector3(0f, TpsRangeSceneBuilder.VaultWallHeight / 2f, WallZ),
            new Vector3(WallWidth, TpsRangeSceneBuilder.VaultWallHeight, TpsRangeSceneBuilder.VaultWallThickness));

        public GameObject Place(Scene scene, GameRules rules)
        {
            var footprint = Footprint;
            var wall = SceneBuildHelpers.CreateStaticBox(scene, null, Name, footprint.center, footprint.size);
            SceneBuildHelpers.SetSurfaceMaterial(wall, SurfaceImpactAssetStep.WoodId);
            return wall;
        }
    }
}
