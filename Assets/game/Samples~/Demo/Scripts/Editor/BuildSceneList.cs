using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// ビルドに入れるシーンの一覧と、Build Settings への登録の仕方を 1 か所にまとめる。
    /// シーンの生成スクリプトは保存したシーンを <see cref="RegisterStartScene"/> (起動時のシーン) か <see cref="Register"/> で登録し、
    /// 専用サーバーのビルドは <see cref="ServerScenes"/> を使う。
    /// 一覧をまとめて登録し直すなら <see cref="RegisterAll"/> (メニューの <see cref="MenuPath"/>) を呼ぶ。
    /// </summary>
    public static class BuildSceneList
    {
        public const string MenuPath = "Fps/Register Build Scenes";
        public const string LogTag = "[BuildSceneList]";

        // シーンはどれも出力先ルート (FpsKitRoot.Output) の Scenes の下に置く。ルートは引数と EditorPrefs で変わるので、読むたびに組む。

        /// <summary>
        /// メインメニュー。起動時のシーンなので Build Settings の先頭に置く。実行時に読む側 (<see cref="Fps.Core.MenuRoute"/>) と同じ名前で、
        /// 出力先ルートが既定なら同じパス。
        /// </summary>
        public static string MainMenuScene => FpsKitRoot.OutputScene(Fps.Core.MenuRoute.MainMenuSceneName);

        /// <summary>射撃場。メニューから読む。</summary>
        public static string BootstrapScene => FpsKitRoot.OutputScene(Fps.Core.MenuRoute.RangeSceneName);

        public static string EnemyAiTestScene => FpsKitRoot.OutputScene("EnemyAiTest");
        public static string MissionTestScene => FpsKitRoot.OutputScene("MissionTest");
        public static string DestructionTestScene => FpsKitRoot.OutputScene("DestructionTest");
        public static string CampaignScene => FpsKitRoot.OutputScene(Fps.Core.MenuRoute.CampaignSceneName);

        /// <summary>キャンペーンの 2 本目のミッション。ミッションの一覧 (MissionCatalog) から読む。</summary>
        public static string Campaign02Scene => FpsKitRoot.OutputScene("Campaign02");

        /// <summary>TPS のアクションを試す射撃場。ルールは <see cref="GameRulesAssetBuilder.TpsRangePath"/>。</summary>
        public static string TpsRangeScene => FpsKitRoot.OutputScene("TpsRange");

        /// <summary>拠点を取り合うモード。ルールは <see cref="GameRulesAssetBuilder.DominationPath"/>。</summary>
        public static string DominationScene => FpsKitRoot.OutputScene("Domination");

        /// <summary>PvE の波で敵を出す部品 (WaveSpawner) を試すシーン。</summary>
        public static string WaveTestScene => FpsKitRoot.OutputScene("WaveTest");

        // 対戦・PvE モード。名前とルールはそれぞれ GameRulesAssetBuilder.ModeNames と <名前>Path に揃える (一致はテストで確かめる)。
        public static string FreeForAllScene => FpsKitRoot.OutputScene(GameRulesAssetBuilder.FreeForAllName);

        /// <summary>キルコンファームド。生成は <see cref="KillConfirmedSceneBuilder"/>。</summary>
        public static string KillConfirmedScene => FpsKitRoot.OutputScene(GameRulesAssetBuilder.KillConfirmedName);

        /// <summary>ガンゲーム。生成は <see cref="GunGameSceneBuilder"/>。</summary>
        public static string GunGameScene => FpsKitRoot.OutputScene(GameRulesAssetBuilder.GunGameName);

        /// <summary>ハードポイント。生成は <see cref="HardpointSceneBuilder"/>。</summary>
        public static string HardpointScene => FpsKitRoot.OutputScene(GameRulesAssetBuilder.HardpointName);

        public static string GuardianScene => FpsKitRoot.OutputScene(GameRulesAssetBuilder.GuardianName);
        public static string ExecutionScene => FpsKitRoot.OutputScene(GameRulesAssetBuilder.ExecutionName);
        public static string SearchAndDestroyScene => FpsKitRoot.OutputScene(GameRulesAssetBuilder.SearchAndDestroyName);
        public static string HordeScene => FpsKitRoot.OutputScene(GameRulesAssetBuilder.HordeName);
        public static string BattleRoyaleScene => FpsKitRoot.OutputScene(GameRulesAssetBuilder.BattleRoyaleName);

        /// <summary><see cref="ModeSceneBuilder"/> の動作確認用シーン (最小の TDM)。生成は <see cref="ModeTestSceneBuilder"/>。</summary>
        public static string ModeTestScene => FpsKitRoot.OutputScene("ModeTest");

        /// <summary>
        /// ネット対戦のシーン。実行時に読み込む側 (Fps.Net の NetBootstrap.ScenePath) と同じ名前で、出力先ルートが既定なら同じパス
        /// (一致はテストで確かめる)。
        /// </summary>
        public static string NetTestScene => FpsKitRoot.OutputScene(Fps.Core.MenuRoute.NetSceneName);

        /// <summary>
        /// ネット対戦のマップ。サーバーは <c>-map NetArena01</c> でこのシーンへ移る。生成は Fps.Samples.Net.Editor の ArenaSceneBuilder
        /// (この Editor のアセンブリは Fps.Samples.Net.Editor を参照しないため文字列で持つ。一致はテストで確かめる)。
        /// </summary>
        public static string NetArena01Scene => FpsKitRoot.OutputScene("NetArena01");

        /// <summary>ビルドに入れるシーン。この順に Build Settings へ並べる (先頭が起動時のシーン)。</summary>
        public static IReadOnlyList<string> Scenes => new[]
        {
            MainMenuScene,
            BootstrapScene,
            EnemyAiTestScene,
            MissionTestScene,
            DestructionTestScene,
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
            HordeScene,
            BattleRoyaleScene,
            ModeTestScene,
            NetArena01Scene,
            NetTestScene,
        };

        /// <summary>
        /// <see cref="GameRulesAssetBuilder.ModeNames"/> の name のモードのシーン (<c>&lt;出力先ルート&gt;/Scenes/&lt;名前&gt;.unity</c>)。
        /// 知らない名前なら <see cref="ArgumentException"/>。
        /// </summary>
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
                case GameRulesAssetBuilder.HordeName:
                    return HordeScene;
                case GameRulesAssetBuilder.BattleRoyaleName:
                    return BattleRoyaleScene;
                default:
                    throw new ArgumentException(
                        $"'{name}' is not one of the mode names ({string.Join(", ", GameRulesAssetBuilder.ModeNames)})", nameof(name));
            }
        }

        /// <summary>
        /// 専用サーバーのビルドに入れるシーン。メニューと一人称カメラの射撃場は要らないので、ネット対戦のシーンを最初に置き、
        /// <c>-map</c> で選べるマップを続ける。出力先ルートが変わるので、読むたびに組む。
        /// </summary>
        public static IReadOnlyList<string> ServerScenes => new[] { NetTestScene, NetArena01Scene };

        /// <summary>path を起動時のシーンとして Build Settings の先頭に有効な状態で置く (<see cref="WithStartScene"/>)。</summary>
        public static void RegisterStartScene(string path)
        {
            EditorBuildSettings.scenes = WithStartScene(EditorBuildSettings.scenes, path);
        }

        /// <summary>path を Build Settings に有効な状態で加える (<see cref="WithScene"/>)。</summary>
        public static void Register(string path)
        {
            EditorBuildSettings.scenes = WithScene(EditorBuildSettings.scenes, path);
        }

        /// <summary>
        /// <see cref="Scenes"/> のうちプロジェクトにあるものを、この順に有効な状態で Build Settings に並べ直す (<see cref="Arrange"/>)。
        /// 一覧に無いシーンは後ろに残す。無いシーンは警告を出して飛ばす。
        /// </summary>
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

        /// <summary>
        /// Build Settings のシーン一覧の先頭に path を有効な状態で置く。既にあるものは先頭へ移し、
        /// 他のシーンは順番と有効/無効をそのまま残す。
        /// </summary>
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

        /// <summary>
        /// Build Settings のシーン一覧に path を有効な状態で加える。既にあれば位置を変えずに有効にし (重複は落とす)、
        /// 他のシーンは順番と有効/無効をそのまま残す。
        /// </summary>
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

        /// <summary>
        /// <see cref="Scenes"/> のうち exists が true のものを、この順に有効な状態で先に並べ、current のうち一覧に無いシーンを
        /// 元の順番と有効/無効のまま後ろに残す。重複と空のパスは落とす。
        /// </summary>
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
