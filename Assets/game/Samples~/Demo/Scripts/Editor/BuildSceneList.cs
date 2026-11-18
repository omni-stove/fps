using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public static class BuildSceneList
    {
        public const string MenuPath = "Shooter/Register Build Scenes";
        public const string LogTag = "[BuildSceneList]";

        // シーンはどれも出力先ルート (ShooterKitRoot.Output) の Scenes の下に置く。ルートは引数と EditorPrefs で変わるので、読むたびに組む。

        public static string MainMenuScene => ShooterKitRoot.OutputScene(Shooter.Core.MenuRoute.MainMenuSceneName);

        public static string BootstrapScene => ShooterKitRoot.OutputScene(Shooter.Core.MenuRoute.RangeSceneName);

        public static string EnemyAiTestScene => ShooterKitRoot.OutputScene("EnemyAiTest");
        public static string MissionTestScene => ShooterKitRoot.OutputScene("MissionTest");
        public static string DestructionTestScene => ShooterKitRoot.OutputScene("DestructionTest");
        public static string VehicleRangeScene => ShooterKitRoot.OutputScene("VehicleRange");
        public static string CampaignScene => ShooterKitRoot.OutputScene(Shooter.Core.MenuRoute.CampaignSceneName);

        public static string Campaign02Scene => ShooterKitRoot.OutputScene("Campaign02");

        public static string TpsRangeScene => ShooterKitRoot.OutputScene("TpsRange");

        public const string FeatureScenePrefix = "Feature_";

        public static string FeatureScene(Shooter.Core.GameRulesDebugField field) => ShooterKitRoot.OutputScene(FeatureScenePrefix + field);

        public static IReadOnlyList<string> FeatureScenes
        {
            get
            {
                var scenes = new List<string>();
                foreach (var feature in FeatureSceneBuilder.All())
                {
                    scenes.Add(FeatureScene(feature.Field));
                }

                return scenes;
            }
        }

        // ルールの入り切りでなく移動の調整値で試す機能のシーン。名前は同じ Feature_ で並べる (ルールの名前と重ならないことはテストで確かめる)。
        public static string TraversalFeatureScene(TraversalFeature feature) => ShooterKitRoot.OutputScene(FeatureScenePrefix + feature);

        public static IReadOnlyList<string> TraversalFeatureScenes
        {
            get
            {
                var scenes = new List<string>();
                foreach (var feature in TraversalFeatureSceneBuilder.Features)
                {
                    scenes.Add(TraversalFeatureScene(feature));
                }

                return scenes;
            }
        }

        public static string DominationScene => ShooterKitRoot.OutputScene("Domination");

        public static string WaveTestScene => ShooterKitRoot.OutputScene("WaveTest");

        // 対戦・PvE モード。名前とルールはそれぞれ GameRulesAssetBuilder.ModeNames と <名前>Path に揃える (一致はテストで確かめる)。
        public static string FreeForAllScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.FreeForAllName);

        public static string KillConfirmedScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.KillConfirmedName);

        public static string GunGameScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.GunGameName);

        public static string HardpointScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.HardpointName);

        public static string GuardianScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.GuardianName);
        public static string ExecutionScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.ExecutionName);
        public static string SearchAndDestroyScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.SearchAndDestroyName);

        public static string CaptureTheFlagScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.CaptureTheFlagName);

        public static string HordeScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.HordeName);
        public static string BattleRoyaleScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.BattleRoyaleName);
        public static string InfectedScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.InfectedName);
        public static string ExtractionScene => ShooterKitRoot.OutputScene(GameRulesAssetBuilder.ExtractionName);

        public static string ModeTestScene => ShooterKitRoot.OutputScene("ModeTest");

        public static string NetTestScene => ShooterKitRoot.OutputScene(Shooter.Core.MenuRoute.NetSceneName);

        // この Editor のアセンブリは Shooter.Samples.Net.Editor を参照しないので、ArenaSceneBuilder のマップ名を文字列で持つ (一致はテストで確かめる)。
        public static string NetArena01Scene => ShooterKitRoot.OutputScene("NetArena01");

        // この順に Build Settings へ並べるので、先頭は起動時のシーンにする。
        public static IReadOnlyList<string> Scenes
        {
            get
            {
                var scenes = new List<string>
                {
                    MainMenuScene,
                    BootstrapScene,
                    EnemyAiTestScene,
                    MissionTestScene,
                    DestructionTestScene,
                    VehicleRangeScene,
                    CampaignScene,
                    Campaign02Scene,
                    TpsRangeScene,
                    DominationScene,
                    WaveTestScene,
                    FreeForAllScene,
                    KillConfirmedScene,
                    GunGameScene,
                    HardpointScene,
                    GuardianScene,
                    ExecutionScene,
                    SearchAndDestroyScene,
                    CaptureTheFlagScene,
                    HordeScene,
                    BattleRoyaleScene,
                    InfectedScene,
                    ExtractionScene,
                    ModeTestScene,
                    NetArena01Scene,
                    NetTestScene,
                };

                var features = new List<string>(FeatureScenes);
                features.AddRange(TraversalFeatureScenes);
                scenes.InsertRange(scenes.IndexOf(TpsRangeScene) + 1, features);
                return scenes;
            }
        }

        // オンラインはブラウザで使えないので、メニューから行けるオフラインのシーンだけにして、たどり着けないシーンで重くしない。
        public static IReadOnlyList<string> WebScenes => new[]
        {
            MainMenuScene,
            BootstrapScene,
            CampaignScene,
            Campaign02Scene,
        };

        public static string ModeScene(string name)
        {
            switch (name)
            {
                case GameRulesAssetBuilder.FreeForAllName:
                    return FreeForAllScene;
                case GameRulesAssetBuilder.KillConfirmedName:
                    return KillConfirmedScene;
                case GameRulesAssetBuilder.GunGameName:
                    return GunGameScene;
                case GameRulesAssetBuilder.HardpointName:
                    return HardpointScene;
                case GameRulesAssetBuilder.GuardianName:
                    return GuardianScene;
                case GameRulesAssetBuilder.ExecutionName:
                    return ExecutionScene;
                case GameRulesAssetBuilder.SearchAndDestroyName:
                    return SearchAndDestroyScene;
                case GameRulesAssetBuilder.CaptureTheFlagName:
                    return CaptureTheFlagScene;
                case GameRulesAssetBuilder.HordeName:
                    return HordeScene;
                case GameRulesAssetBuilder.BattleRoyaleName:
                    return BattleRoyaleScene;
                case GameRulesAssetBuilder.InfectedName:
                    return InfectedScene;
                case GameRulesAssetBuilder.ExtractionName:
                    return ExtractionScene;
                default:
                    throw new ArgumentException(
                        $"'{name}' is not one of the mode names ({string.Join(", ", GameRulesAssetBuilder.ModeNames)})", nameof(name));
            }
        }

        // 先頭が -map を省いたときのマップになる。出力先ルートが変わるので、読むたびに組む。
        public static IReadOnlyList<string> ServerScenes => new[] { NetTestScene, NetArena01Scene };

        public static void RegisterStartScene(string path)
        {
            EditorBuildSettings.scenes = WithStartScene(EditorBuildSettings.scenes, path);
        }

        public static void Register(string path)
        {
            EditorBuildSettings.scenes = WithScene(EditorBuildSettings.scenes, path);
        }

        [MenuItem(MenuPath)]
        public static void RegisterAll()
        {
            var missing = new List<string>();
            EditorBuildSettings.scenes = Arrange(EditorBuildSettings.scenes, path =>
            {
                var exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
                if (!exists)
                {
                    missing.Add(path);
                }

                return exists;
            });

            if (missing.Count > 0)
            {
                Debug.LogWarning($"{LogTag} skipped scene(s) that have not been built yet: {string.Join(", ", missing)}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{LogTag} registered {EditorBuildSettings.scenes.Length} scene(s)");
        }

        public static EditorBuildSettingsScene[] WithStartScene(IReadOnlyList<EditorBuildSettingsScene> current, string path)
        {
            path = BootstrapSceneBuilder.ValidateScenePath(path);

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            if (current != null)
            {
                foreach (var scene in current)
                {
                    if (scene == null || string.IsNullOrEmpty(scene.path) || SamePath(scene.path, path))
                    {
                        continue;
                    }

                    scenes.Add(scene);
                }
            }

            return scenes.ToArray();
        }

        public static EditorBuildSettingsScene[] WithScene(IReadOnlyList<EditorBuildSettingsScene> current, string path)
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

                    if (SamePath(scene.path, path))
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

        public static EditorBuildSettingsScene[] Arrange(IReadOnlyList<EditorBuildSettingsScene> current, Func<string, bool> exists)
        {
            if (exists == null)
            {
                throw new ArgumentNullException(nameof(exists));
            }

            var scenes = new List<EditorBuildSettingsScene>();
            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in Scenes)
            {
                listed.Add(path);
                if (exists(path))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            if (current != null)
            {
                var kept = new HashSet<string>(StringComparer.Ordinal);
                foreach (var scene in current)
                {
                    if (scene == null || string.IsNullOrEmpty(scene.path))
                    {
                        continue;
                    }

                    var path = scene.path.Replace('\\', '/');
                    if (listed.Contains(path) || !kept.Add(path))
                    {
                        continue;
                    }

                    scenes.Add(scene);
                }
            }

            return scenes.ToArray();
        }

        private static bool SamePath(string a, string b)
        {
            return string.Equals(a.Replace('\\', '/'), b, StringComparison.Ordinal);
        }
    }
}
