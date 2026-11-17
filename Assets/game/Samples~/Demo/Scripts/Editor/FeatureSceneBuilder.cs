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
    /// ルールのオンオフの項目 (<see cref="GameRulesDebugDraft.IsToggle"/>) ごとに、その機能だけを試す小さなシーン
    /// (<see cref="BuildSceneList.FeatureScene"/>) を 1 つずつ生成する。シーンにはその機能の試し場所 (<see cref="Stations"/>) だけを置き、
    /// ルールは <see cref="GameRulesAssetBuilder.FeatureRules"/> (その項目だけ On) を <see cref="GameRulesAssetBuilder.FeatureRulesPath"/> に置いて配線する。
    /// 実行中は F1 (ルールのデバッグパネル) で同じ項目を切って見比べ、F2 (<see cref="ViewModeDebugToggle"/>) で一人称と三人称を入れ替える。
    /// プレイヤー・標的ダミー・HUD は実行時に GameBootstrap が作る。保存したシーンは Build Settings に (並びを変えずに) 加える。
    /// 何度実行しても同じ結果になる。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.FeatureSceneBuilder.Build</code>
    /// </summary>
    /// <remarks>
    /// 試し場所は TPS の射撃場の置く物 (<see cref="ITpsRangeFixture"/>) をそのまま使い、射撃場の配置に意味があるもの
    /// (<see cref="FeaturePlacement.InPlace"/>) 以外は、置いてから根ごと出現位置の右前 (<see cref="FeaturePlacement.BesideSpawn"/>) へ平行に移す。
    /// 置く物は子と線の端 (ジップライン) を根から見た位置で持つので、移しても形は崩れない。
    /// 床は出現位置・標的ダミー・試し場所が収まる最小の正方形 (<see cref="FloorHalfExtent"/>)。
    /// </remarks>
    public static class FeatureSceneBuilder
    {
        public const string MenuPath = "Fps/Build Feature Scenes";
        public const string LogTag = "[FeatureSceneBuilder]";

        public const string BootstrapObjectName = "GameBootstrap";
        public const string FloorName = GameBootstrap.FloorName;

        /// <summary><see cref="FeaturePlacement.BesideSpawn"/> の試し場所の左端の x (m)。標的ダミー (x = -4..4、半径 0.5) の右に空ける。</summary>
        public const float BesideSpawnMinX = 6f;

        /// <summary><see cref="FeaturePlacement.BesideSpawn"/> の試し場所の手前の端の z (m)。</summary>
        public const float BesideSpawnMinZ = 3f;

        /// <summary>試し場所の外側に残す床の幅 (m)。</summary>
        public const float FloorMargin = 6f;

        /// <summary>床の半分の広さの下限 (m)。標的ダミーの列 (z = 10) と回避・グレネードの分。</summary>
        public const float MinFloorHalfExtent = 15f;

        /// <summary>床の半分の広さをこの倍数に切り上げる (m)。</summary>
        public const float FloorStep = 5f;

        /// <summary>機能ごとのシーンの組み立て。オンオフの項目すべてを 1 つずつ、<see cref="GameRulesDebugDraft.Fields"/> の順に並べる。</summary>
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

        /// <summary>field の機能の試し場所。標的ダミーと開けた床だけで試せる機能 (予備弾・アクティブリロード・回避・グレネードなど) は空。呼ぶたびに作り直す。</summary>
        /// <exception cref="ArgumentException">field がオンオフの項目でない。</exception>
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

        /// <summary>
        /// feature のシーンを scenePath に生成・保存し、Build Settings に加える (既存の並びは変えない)。
        /// rulesPath のルールが無ければ <see cref="GameRulesAssetBuilder.FeatureRules"/> で作り、あれば値を変えずに使う。
        /// 武器のアセットが無いか不正なら何も保存せずに例外を投げる (batchmode では終了コード 1 になる)。
        /// </summary>
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

        /// <summary>
        /// 空のシーンに床、GameBootstrap (床は作らない、weapons と rules を配線) と MatchRunner を置いた GameObject を入れ、
        /// stations の置く物を順に置いて移し、GameBootstrap を返す。置く物の前に GameBootstrap を置くのは、敵を置く物がその武器を引くため。
        /// 引数が不正か置く物が決まりを守らなければ、置いた物を消して例外を投げる。
        /// </summary>
        /// <exception cref="ArgumentException">stations の名前が空か、組み込みの物やほかの置く物と重なる。</exception>
        /// <exception cref="InvalidOperationException">置く物が null を返した、別のシーンや子に置いた、または名前と違う名前で置いた。</exception>
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

        /// <summary>
        /// stations を置くシーンの床の x, z 方向の半分の広さ (m)。出現位置・標的ダミー・移した後の試し場所から <see cref="FloorMargin"/> 離れるまでを、
        /// <see cref="MinFloorHalfExtent"/> 以上で <see cref="FloorStep"/> の倍数に切り上げる。
        /// </summary>
        /// <exception cref="ArgumentNullException">stations が null。</exception>
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

        /// <summary>半分の広さ half の床にする Plane の倍率。Plane は 10m 四方。</summary>
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

    /// <summary>機能ごとのシーン 1 つ分。試す項目と、置く試し場所。</summary>
    public sealed class FeatureScene
    {
        /// <exception cref="ArgumentNullException">stations が null。</exception>
        public FeatureScene(GameRulesDebugField field, IReadOnlyList<FeatureStation> stations)
        {
            Field = field;
            Stations = stations ?? throw new ArgumentNullException(nameof(stations));
        }

        public GameRulesDebugField Field { get; }

        public IReadOnlyList<FeatureStation> Stations { get; }
    }

    /// <summary>試し場所をどこに置くか。</summary>
    public enum FeaturePlacement
    {
        /// <summary>TPS の射撃場と同じ位置。出現位置や標的ダミーとの位置関係に意味がある置く物 (遮蔽物・怯みの敵など)。</summary>
        InPlace,

        /// <summary>
        /// 出現位置の右前。置く物の範囲の左端を <see cref="FeatureSceneBuilder.BesideSpawnMinX"/>、手前の端を
        /// <see cref="FeatureSceneBuilder.BesideSpawnMinZ"/> に合わせ、標的ダミーの列にかからないようにする。
        /// </summary>
        BesideSpawn,
    }

    /// <summary>機能ごとのシーンの試し場所 1 つ。TPS の射撃場の置く物と、それを置く場所。</summary>
    public readonly struct FeatureStation
    {
        /// <exception cref="ArgumentNullException">fixture が null。</exception>
        public FeatureStation(ITpsRangeFixture fixture, FeaturePlacement placement)
        {
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            Placement = placement;
        }

        public ITpsRangeFixture Fixture { get; }

        public FeaturePlacement Placement { get; }

        /// <summary>置く物を移す量。水平だけ。<see cref="FeaturePlacement.InPlace"/> なら 0。</summary>
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

        /// <summary>移した後の、置く物が床の上で占める範囲 (ワールド座標)。</summary>
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

    /// <summary>はしごの付いた台。はしごは出現位置の側 (-X) に立てる。高さは機能ごとに変える。</summary>
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

    /// <summary>壊せる小屋と、その破片の置き場。壊れるかはルールの Destruction で決まる。</summary>
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

    /// <summary>Bootstrap の射撃場と同じ低い遮蔽物と高い遮蔽物 (<see cref="BootstrapSceneBuilder.Covers"/>)。出現位置と標的の間に置く。</summary>
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

    /// <summary>乗り越え用の低い壁。TPS の射撃場の壁と同じ高さと厚さで、出現位置の前に +Z へ越える向きに置く。</summary>
    internal sealed class FeatureVaultWallFixture : ITpsRangeFixture
    {
        /// <summary>壁の中心の z (m)。出現位置と標的ダミーの間。</summary>
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
