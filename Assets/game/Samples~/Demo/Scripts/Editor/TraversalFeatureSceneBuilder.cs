using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterComponent = Shooter.Runtime.Traversal.Water;

namespace Shooter.Editor
{
    public enum TraversalFeature
    {
        Lean,

        Swim,

        WallRun,

        DoubleJump,
    }

    // FeatureSceneBuilder と同じ床・GameBootstrap・置き方 (出現位置の横) で組み、違うのはルールの入り切りでなく移動の調整値で試すことだけにする。
    // 壁走りと二段ジャンプはキットの既定で切ってあるので、ゲーム全体の値 (Resources・入口) は変えず、このシーンの GameBootstrap にだけ調整値を渡す。
    public static class TraversalFeatureSceneBuilder
    {
        public const string MenuPath = "Shooter/Build Traversal Feature Scenes";
        public const string LogTag = "[TraversalFeatureSceneBuilder]";

        public const int DoubleJumpAirJumps = 1;

        public const string LeanCornerName = "LeanCorner";
        public const string CornerFrontWallName = "LeanCorner_Front";
        public const string CornerSideWallName = "LeanCorner_Side";
        public const string LowWallName = "LeanCorner_LowWall";

        // 立ち 2 m の頭を隠し、角の端から横へ出て覗く高さにする。
        public const float CornerWallHeight = 3f;

        // MotorSpec.DefaultCoverLowWallHeight より低くし、しゃがんだ頭 (1.2 m) が隠れて立てば上から見える高さにする。
        public const float LowWallHeight = 1f;

        public const float LeanWallThickness = 0.4f;
        public const float LeanWallLength = 4f;
        public const float LowWallX = 6f;

        public const string SwimPoolsName = "SwimPools";
        public const string ShallowWaterName = "Pool_ShallowWater";
        public const string DeepWaterName = "Pool_DeepWater";
        public const string HighRimWaterName = "Pool_HighRimWater";
        public const string DividerName = "Pool_Divider";

        // 胸 (足元から 1.3 m、SwimStep.ChestY) より低く、歩いて渡れる深さにする。
        public const float ShallowSurfaceHeight = 0.5f;

        // 頭 (2 m) より深く、立てずに泳ぐ深さにする。
        public const float DeepSurfaceHeight = 2.5f;

        public const float ShallowRimHeight = 0.6f;

        // 浮いた足元 (水面から胸の高さ 1.3 m 下) からの高さが、ジャンプ (1.2 m) では届かず崖のぼり (MotorSpec.DefaultMantleMaxHeight) で届く縁にする。
        public const float HighRimHeight = 3.3f;

        public const float PoolWidth = 6f;
        public const float ShallowLength = 4f;
        public const float DeepLength = 6f;
        public const float PoolWallThickness = 0.5f;

        // 縁の高いプールと深いプールの間の壁は、上に立ち 2 m・半径 0.5 m の体が乗れる厚さにし、そこから崖のぼりで出入りする。
        public const float DividerThickness = 1.2f;

        public const string WallRunCorridorName = "WallRunCorridor";
        public const string LeftRunWallName = "WallRunCorridor_Left";
        public const string RightRunWallName = "WallRunCorridor_Right";

        // ダッシュ (8 m/s) で WallRunTuning.DefaultMaxSeconds 走っても端に届かない長さにする。
        public const float RunWallLength = 24f;

        public const float RunWallHeight = 4f;
        public const float RunWallThickness = 0.5f;

        // 片方の壁から跳ぶ速さ (WallRunTuning.DefaultJumpOffSpeed) で、落ち切る前に向かいの壁に届く幅にする。
        public const float CorridorWidth = 3.5f;

        public const string DoubleJumpStepsName = "DoubleJumpSteps";
        public const string StepNamePrefix = "DoubleJumpStep_";
        public const int StepCount = 3;

        // ジャンプ 1 回 (MotorSpec.DefaultJumpHeight) では届かず、空中でもう 1 回跳ぶと届く段差にする。
        public const float StepRise = 2f;

        public const float StepWidth = 4f;
        public const float StepDepth = 3f;

        public static IReadOnlyList<TraversalFeature> Features { get; } = new[]
        {
            TraversalFeature.Lean,
            TraversalFeature.Swim,
            TraversalFeature.WallRun,
            TraversalFeature.DoubleJump,
        };

        public static string RulesPath(TraversalFeature feature) => GameRulesAssetBuilder.FeatureFolder + "/" + feature + ".asset";

        public static string MotorSettingsPath => ShooterKitRoot.OutputPath("Movement/" + BuildSceneList.FeatureScenePrefix + TraversalFeature.DoubleJump + ".asset");

        public static string TraversalTuningPath => ShooterKitRoot.OutputPath("Tuning/" + BuildSceneList.FeatureScenePrefix + TraversalFeature.WallRun + ".json");

        public static bool UsesMotorSettings(TraversalFeature feature) => feature == TraversalFeature.DoubleJump;

        public static bool UsesTraversalTuning(TraversalFeature feature) => feature == TraversalFeature.WallRun;

        // 水から縁へ上がるのはオフラインでは崖のぼり (PlayerController.TryClimbOutOfWater) なので、泳ぎのシーンだけ Mantle を入れる。
        // 二段ジャンプのシーンで入れないのは、崖のぼりで段を上がれて二段目を試さずに済んでしまうため。
        public static GameRules Rules(TraversalFeature feature)
        {
            RequireDefined(feature);
            var draft = new GameRulesDebugDraft(GameRulesAssetBuilder.FeatureBaseRules());
            if (feature == TraversalFeature.Swim)
            {
                draft.Set(GameRulesDebugField.Mantle, (int)Mantle.On);
            }

            return draft.Build();
        }

        public static IReadOnlyList<FeatureStation> Stations(TraversalFeature feature)
        {
            RequireDefined(feature);
            return new[] { new FeatureStation(Fixture(feature), FeaturePlacement.BesideSpawn) };
        }

        public static IReadOnlyList<LevelBox> Boxes(TraversalFeature feature)
        {
            switch (feature)
            {
                case TraversalFeature.Lean:
                    return LeanBoxes();
                case TraversalFeature.Swim:
                    return PoolBoxes();
                case TraversalFeature.WallRun:
                    return CorridorBoxes();
                case TraversalFeature.DoubleJump:
                    return StepBoxes();
                default:
                    throw Undefined(feature);
            }
        }

        public static IReadOnlyList<FeatureWater> Waters(TraversalFeature feature)
        {
            RequireDefined(feature);
            if (feature != TraversalFeature.Swim)
            {
                return Array.Empty<FeatureWater>();
            }

            var deepZ = ShallowLength + (DeepLength / 2f);
            var highRimX = PoolWidth + DividerThickness + (PoolWidth / 2f);
            return new[]
            {
                new FeatureWater(ShallowWaterName, new Vector3(PoolWidth / 2f, 0f, ShallowLength / 2f), new Vector3(PoolWidth, ShallowRimHeight, ShallowLength), ShallowSurfaceHeight),
                new FeatureWater(DeepWaterName, new Vector3(PoolWidth / 2f, 0f, deepZ), new Vector3(PoolWidth, DeepSurfaceHeight, DeepLength), DeepSurfaceHeight),
                new FeatureWater(HighRimWaterName, new Vector3(highRimX, 0f, deepZ), new Vector3(PoolWidth, HighRimHeight, DeepLength), DeepSurfaceHeight),
            };
        }

        public static string TraversalTuningJson()
        {
            return "{\n"
                + $"  \"{TraversalJson.WallRunItem}\": {{\n"
                + $"    \"{TraversalJson.EnabledItem}\": true\n"
                + "  }\n"
                + "}\n";
        }

        // ゲーム全体の移動の値 (入口・Resources) に合わせ、空中で跳べる回数だけを変える。
        public static void ConfigureDoubleJump(MotorSettings settings, MotorSettings source)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (source != null && source != settings)
            {
                var name = settings.name;
                EditorUtility.CopySerialized(source, settings);
                settings.name = name;
            }

            settings.AirJumps = DoubleJumpAirJumps;

            // 壊れた値を保存しないよう、ここで作れることを確かめる。
            settings.CreateSpec();
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

            foreach (var feature in Features)
            {
                BuildAt(feature, BuildSceneList.TraversalFeatureScene(feature));
            }
        }

        public static void BuildAt(TraversalFeature feature, string scenePath)
        {
            RequireDefined(feature);
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作ってから読む (FeatureSceneBuilder.BuildAt と同じ)。
            var weapons = TpsRangeSceneBuilder.LoadPlayerWeapons();
            var rules = LoadOrBuildRules(feature, RulesPath(feature));
            var motorSettings = UsesMotorSettings(feature) ? LoadOrBuildMotorSettings(MotorSettingsPath) : null;
            var traversal = UsesTraversalTuning(feature) ? LoadOrBuildTraversalTuning(TraversalTuningPath) : null;

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            PopulateScene(scene, weapons, rules, feature, motorSettings, traversal);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the {feature} feature scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogTag} saved '{scenePath}' for {feature} and registered it in Build Settings");
        }

        public static GameBootstrap PopulateScene(
            Scene scene,
            IReadOnlyList<WeaponDefinition> weapons,
            GameRulesDefinition rules,
            TraversalFeature feature,
            MotorSettings motorSettings,
            TextAsset traversal)
        {
            RequireDefined(feature);
            if (UsesMotorSettings(feature) && motorSettings == null)
            {
                throw new ArgumentNullException(nameof(motorSettings), $"the {feature} scene needs its MotorSettings");
            }

            if (UsesTraversalTuning(feature) && traversal == null)
            {
                throw new ArgumentNullException(nameof(traversal), $"the {feature} scene needs its traversal JSON");
            }

            var bootstrap = FeatureSceneBuilder.PopulateScene(scene, weapons, rules, Stations(feature));
            bootstrap.MotorSettings = UsesMotorSettings(feature) ? motorSettings : null;
            bootstrap.TraversalTuningJson = UsesTraversalTuning(feature) ? traversal : null;
            EditorSceneManager.MarkSceneDirty(scene);
            return bootstrap;
        }

        // ゲーム側で調整した値を残すよう、既にあれば値は変えない (GameRulesAssetBuilder.LoadOrBuildFeatureRules と同じ)。
        public static GameRulesDefinition LoadOrBuildRules(TraversalFeature feature, string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            if (AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path) != null)
            {
                return GameRulesAssetBuilder.LoadRules(path);
            }

            var rules = Rules(feature);
            var built = SceneBuildHelpers.CreateOrUpdateAsset<GameRulesDefinition>(path, d => GameRulesAssetBuilder.Configure(d, rules));
            Debug.Log($"{LogTag} saved '{path}'");
            return built;
        }

        public static MotorSettings LoadOrBuildMotorSettings(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            var existing = AssetDatabase.LoadAssetAtPath<MotorSettings>(path);
            if (existing != null)
            {
                existing.CreateSpec();
                return existing;
            }

            var source = MotorSettings.Load();
            var built = SceneBuildHelpers.CreateOrUpdateAsset<MotorSettings>(path, s => ConfigureDoubleJump(s, source));
            Debug.Log($"{LogTag} saved '{path}'");
            return built;
        }

        public static TextAsset LoadOrBuildTraversalTuning(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path, ".json");
            var existing = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (existing != null)
            {
                // 読めない JSON をシーンに配線すると、遊ぶときに黙ってゲーム全体の値へ戻るので、作るときに止める。
                TraversalJson.Parse(existing.text, path);
                return existing;
            }

            var json = TraversalTuningJson();
            TraversalJson.Parse(json, path);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path));
            File.WriteAllText(FullPath(path), json);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var built = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (built == null)
            {
                throw new IOException($"Failed to import the traversal JSON '{path}'");
            }

            Debug.Log($"{LogTag} saved '{path}'");
            return built;
        }

        private static string FullPath(string assetPath)
        {
            var project = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(project ?? string.Empty, assetPath);
        }

        private static ITpsRangeFixture Fixture(TraversalFeature feature)
        {
            switch (feature)
            {
                case TraversalFeature.Lean:
                    return new TraversalFeatureFixture(LeanCornerName, TraversalFeature.Lean);
                case TraversalFeature.Swim:
                    return new TraversalFeatureFixture(SwimPoolsName, TraversalFeature.Swim);
                case TraversalFeature.WallRun:
                    return new TraversalFeatureFixture(WallRunCorridorName, TraversalFeature.WallRun);
                case TraversalFeature.DoubleJump:
                    return new TraversalFeatureFixture(DoubleJumpStepsName, TraversalFeature.DoubleJump);
                default:
                    throw Undefined(feature);
            }
        }

        // 手前 (-Z) から近づき、正面の壁の右端 (+X) の角から覗く。低い壁は横に離して、しゃがんで隠れてから傾ける。
        private static IReadOnlyList<LevelBox> LeanBoxes()
        {
            return new[]
            {
                Corners(CornerFrontWallName, Vector3.zero, new Vector3(LeanWallLength, CornerWallHeight, LeanWallThickness)),
                Corners(
                    CornerSideWallName,
                    new Vector3(LeanWallLength - LeanWallThickness, 0f, LeanWallThickness),
                    new Vector3(LeanWallLength, CornerWallHeight, LeanWallLength)),
                Corners(
                    LowWallName,
                    new Vector3(LowWallX, 0f, 0f),
                    new Vector3(LowWallX + LeanWallLength, LowWallHeight, LeanWallThickness)),
            };
        }

        // 手前 (-Z) が開いた浅い所から奥の深い所へ歩いて入る。床は地面のままなので、深さは水面の高さで作る。
        // 縁の高いプールは仕切りの上から飛び込み、崖のぼりで仕切りへ上がって出る。
        private static IReadOnlyList<LevelBox> PoolBoxes()
        {
            var t = PoolWallThickness;
            var deepEnd = ShallowLength + DeepLength;
            var dividerEnd = PoolWidth + DividerThickness;
            var highRimEnd = dividerEnd + PoolWidth;
            return new[]
            {
                Corners("Pool_ShallowLeft", new Vector3(-t, 0f, 0f), new Vector3(0f, ShallowRimHeight, ShallowLength)),
                Corners("Pool_ShallowRight", new Vector3(PoolWidth, 0f, 0f), new Vector3(PoolWidth + t, ShallowRimHeight, ShallowLength - t)),
                Corners("Pool_DeepLeft", new Vector3(-t, 0f, ShallowLength), new Vector3(0f, DeepSurfaceHeight, deepEnd + t)),
                Corners("Pool_DeepBack", new Vector3(0f, 0f, deepEnd), new Vector3(PoolWidth, DeepSurfaceHeight, deepEnd + t)),
                Corners(DividerName, new Vector3(PoolWidth, 0f, ShallowLength - t), new Vector3(dividerEnd, HighRimHeight, deepEnd + t)),
                Corners("Pool_HighRimFront", new Vector3(dividerEnd, 0f, ShallowLength - t), new Vector3(highRimEnd + t, HighRimHeight, ShallowLength)),
                Corners("Pool_HighRimRight", new Vector3(highRimEnd, 0f, ShallowLength), new Vector3(highRimEnd + t, HighRimHeight, deepEnd + t)),
                Corners("Pool_HighRimBack", new Vector3(dividerEnd, 0f, deepEnd), new Vector3(highRimEnd, HighRimHeight, deepEnd + t)),
            };
        }

        // 前 (+Z) へ向かって通路を走り、左右どちらかの壁の脇で跳んで付く。
        private static IReadOnlyList<LevelBox> CorridorBoxes()
        {
            var rightX = RunWallThickness + CorridorWidth;
            return new[]
            {
                Corners(LeftRunWallName, Vector3.zero, new Vector3(RunWallThickness, RunWallHeight, RunWallLength)),
                Corners(RightRunWallName, new Vector3(rightX, 0f, 0f), new Vector3(rightX + RunWallThickness, RunWallHeight, RunWallLength)),
            };
        }

        // 奥 (+Z) へ上がる階段にし、どの段も前の段から二段ジャンプで上がる。
        private static IReadOnlyList<LevelBox> StepBoxes()
        {
            var boxes = new LevelBox[StepCount];
            for (var i = 0; i < StepCount; i++)
            {
                boxes[i] = Corners(
                    StepNamePrefix + (i + 1),
                    new Vector3(0f, 0f, i * StepDepth),
                    new Vector3(StepWidth, StepRise * (i + 1), (i + 1) * StepDepth));
            }

            return boxes;
        }

        private static LevelBox Corners(string name, Vector3 min, Vector3 max)
        {
            return new LevelBox(name, (min + max) / 2f, max - min);
        }

        private static void RequireDefined(TraversalFeature feature)
        {
            if (!Enum.IsDefined(typeof(TraversalFeature), feature))
            {
                throw Undefined(feature);
            }
        }

        private static ArgumentOutOfRangeException Undefined(TraversalFeature feature)
        {
            return new ArgumentOutOfRangeException(nameof(feature), feature, "not a defined TraversalFeature");
        }
    }

    public readonly struct FeatureWater
    {
        public FeatureWater(string name, Vector3 bottom, Vector3 size, float surfaceHeight)
        {
            Name = name;
            Bottom = bottom;
            Size = size;
            SurfaceHeight = surfaceHeight;
        }

        public string Name { get; }

        // Water と同じく、箱の底の真ん中。
        public Vector3 Bottom { get; }

        public Vector3 Size { get; }

        public float SurfaceHeight { get; }
    }

    // public にすると TPS の射撃場の生成 (ExtensionPoints) が拾って射撃場にも置くので internal にする (FeatureSceneBuilder の置く物と同じ)。
    internal sealed class TraversalFeatureFixture : ITpsRangeFixture
    {
        private readonly TraversalFeature _feature;

        public TraversalFeatureFixture(string name, TraversalFeature feature)
        {
            Name = name;
            _feature = feature;
        }

        public int Order => 0;

        public string Name { get; }

        public Bounds Footprint
        {
            get
            {
                var boxes = TraversalFeatureSceneBuilder.Boxes(_feature);
                var bounds = new Bounds(boxes[0].Center, boxes[0].Size);
                foreach (var box in boxes)
                {
                    bounds.Encapsulate(new Bounds(box.Center, box.Size));
                }

                foreach (var water in TraversalFeatureSceneBuilder.Waters(_feature))
                {
                    bounds.Encapsulate(new Bounds(water.Bottom + new Vector3(0f, water.Size.y / 2f, 0f), water.Size));
                }

                return bounds;
            }
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(Name);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                foreach (var box in TraversalFeatureSceneBuilder.Boxes(_feature))
                {
                    var placed = SceneBuildHelpers.CreateStaticBox(scene, root.transform, box.Name, box.Center, box.Size);
                    SceneBuildHelpers.SetSurfaceMaterial(placed, SurfaceImpactAssetStep.ConcreteId);
                }

                foreach (var water in TraversalFeatureSceneBuilder.Waters(_feature))
                {
                    var volume = new GameObject(water.Name);
                    volume.transform.SetParent(root.transform, false);
                    volume.transform.localPosition = water.Bottom;
                    var component = volume.AddComponent<WaterComponent>();
                    component.Size = water.Size;
                    component.SurfaceHeight = water.SurfaceHeight;
                }

                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }
    }
}
