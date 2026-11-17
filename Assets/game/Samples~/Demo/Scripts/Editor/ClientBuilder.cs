using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Shooter.Editor
{
    public static class ClientBuilder
    {
        public const string LogTag = "[Build]";
        public const string MenuPathWindows = "Shooter/Build Client (Windows)";
        public const string MenuPathMacOS = "Shooter/Build Client (macOS)";

        public static void Build()
        {
            if (!ClientBuildOptions.TryParse(
                    Environment.GetCommandLineArgs(), ClientPlatform.MacOS, out var options, out var error))
            {
                throw new ArgumentException(LogTag + " ClientBuilder: invalid arguments: " + error);
            }

            BuildVersionStamper.BuildWithResolvedVersion(() => BuildFor(options));
        }

        [MenuItem(MenuPathWindows)]
        private static void BuildWindowsFromMenu()
        {
            BuildVersionStamper.BuildWithResolvedVersion(() => BuildFor(new ClientBuildOptions(ClientPlatform.Windows)));
        }

        [MenuItem(MenuPathMacOS)]
        private static void BuildMacOSFromMenu()
        {
            BuildVersionStamper.BuildWithResolvedVersion(() => BuildFor(new ClientBuildOptions(ClientPlatform.MacOS)));
        }

        // Build Settings にはテンプレートの残りのシーンも入っているので、そちらではなく一覧から決める。
        public static string[] ClientScenePaths(IReadOnlyList<string> listed, Func<string, bool> exists, ICollection<string> skipped)
        {
            if (listed == null)
            {
                throw new ArgumentNullException(nameof(listed));
            }

            if (exists == null)
            {
                throw new ArgumentNullException(nameof(exists));
            }

            var paths = new List<string>();
            foreach (var path in listed)
            {
                if (string.IsNullOrEmpty(path) || paths.Contains(path))
                {
                    continue;
                }

                if (exists(path))
                {
                    paths.Add(path);
                }
                else
                {
                    skipped?.Add(path);
                }
            }

            return paths.ToArray();
        }

        public static BuildTarget ToBuildTarget(ClientPlatform platform)
        {
            switch (platform)
            {
                case ClientPlatform.Windows:
                    return BuildTarget.StandaloneWindows64;
                case ClientPlatform.MacOS:
                    return BuildTarget.StandaloneOSX;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }
        }

        // 前のビルドの残り (消えたプラグインなど) を配る zip に混ぜないよう、出力先のディレクトリを空にしてから書き出す。
        public static BuildReport BuildFor(ClientBuildOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var target = ToBuildTarget(options.Platform);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                throw new InvalidOperationException(
                    LogTag + " ClientBuilder: " + target + " is not installed for this Unity editor; add the "
                    + (options.Platform == ClientPlatform.Windows ? "Windows Build Support (Mono)" : "Mac Build Support (Mono)")
                    + " module in Unity Hub");
            }

            var scenes = RequireScenes();

            if (Directory.Exists(options.OutputDirectory))
            {
                Debug.Log(LogTag + " ClientBuilder: removing the previous build in " + options.OutputDirectory);
                Directory.Delete(options.OutputDirectory, true);
            }

            Directory.CreateDirectory(options.OutputDirectory);
            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = options.ExecutablePath,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.StrictMode,
            };

            Debug.Log(LogTag + " ClientBuilder: building " + options + " version " + PlayerSettings.bundleVersion
                + " with " + string.Join(", ", scenes));
            var report = BuildPipeline.BuildPlayer(buildOptions);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    LogTag + " ClientBuilder: build " + summary.result + " for " + options
                    + " with " + summary.totalErrors + " error(s); see the log above");
            }

            if (!File.Exists(options.ExecutablePath) && !Directory.Exists(options.ExecutablePath))
            {
                throw new InvalidOperationException(
                    LogTag + " ClientBuilder: the build reported success but " + options.ExecutablePath + " does not exist");
            }

            Debug.Log(LogTag + " ClientBuilder: wrote " + summary.outputPath + " (" + summary.totalSize + " bytes, "
                + summary.totalWarnings + " warning(s), " + summary.totalTime + ")");
            return report;
        }

        // 起動時のシーンが無いと起動しても何も出ないビルドになるので例外を投げるが、他に無いシーンは BuildSceneList.RegisterAll と同じく警告を出して飛ばす。
        private static string[] RequireScenes()
        {
            var skipped = new List<string>();
            var scenes = ClientScenePaths(
                BuildSceneList.Scenes, path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null, skipped);

            if (scenes.Length == 0 || scenes[0] != BuildSceneList.MainMenuScene)
            {
                throw new InvalidOperationException(
                    LogTag + " ClientBuilder: the start scene " + BuildSceneList.MainMenuScene
                    + " does not exist; run MenuSceneBuilder.Build and commit the scene");
            }

            if (skipped.Count > 0)
            {
                Debug.LogWarning(LogTag + " ClientBuilder: skipped scene(s) that have not been built yet: " + string.Join(", ", skipped));
            }

            return scenes;
        }
    }
}
