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

        /// <summary>メインメニュー。起動時のシーンなので Build Settings の先頭に置く。実行時に読む側 (<see cref="Fps.Core.MenuRoute"/>) と同じ値。</summary>
        public const string MainMenuScene = Fps.Core.MenuRoute.MainMenuScene;

        /// <summary>射撃場。メニューから読む。</summary>
        public const string BootstrapScene = "Assets/game/Scenes/Bootstrap.unity";

        public const string EnemyAiTestScene = "Assets/game/Scenes/EnemyAiTest.unity";
        public const string MissionTestScene = "Assets/game/Scenes/MissionTest.unity";
        public const string DestructionTestScene = "Assets/game/Scenes/DestructionTest.unity";
        public const string CampaignScene = "Assets/game/Scenes/Campaign01.unity";

        /// <summary>キャンペーンの 2 本目のミッション。ミッションの一覧 (MissionCatalog) から読む。</summary>
        public const string Campaign02Scene = "Assets/game/Scenes/Campaign02.unity";

        /// <summary>TPS のアクションを試す射撃場。ルールは <see cref="GameRulesAssetBuilder.TpsRangePath"/>。</summary>
        public const string TpsRangeScene = "Assets/game/Scenes/TpsRange.unity";

        /// <summary>拠点を取り合うモード。ルールは <see cref="GameRulesAssetBuilder.DominationPath"/>。</summary>
        public const string DominationScene = "Assets/game/Scenes/Domination.unity";

        /// <summary>
        /// ネット対戦のシーン。実行時に読み込む側 (Fps.Net の NetBootstrap.ScenePath) と同じ値で、
        /// この Editor のアセンブリは Fps.Net を参照しないため文字列で持つ (一致はテストで確かめる)。
        /// </summary>
        public const string NetTestScene = "Assets/game/Scenes/NetTest.unity";

        /// <summary>ビルドに入れるシーン。この順に Build Settings へ並べる (先頭が起動時のシーン)。</summary>
        public static IReadOnlyList<string> Scenes { get; } = new[]
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
            NetTestScene,
        };

        /// <summary>専用サーバーのビルドに入れるシーン。メニューと一人称カメラの射撃場は要らないので、ネット対戦のシーンだけを最初に置く。</summary>
        public static IReadOnlyList<string> ServerScenes { get; } = new[] { NetTestScene };

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
