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
        public const string MenuPathWebGL = "Shooter/Build Client (WebGL)";

        // EOS の SDK にはブラウザ向けのネイティブライブラリが無いので、WebGL では EOS と Shooter.NetEos を外す。
        public const string EosDisableDefine = "EOS_DISABLE";

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

        [MenuItem(MenuPathWebGL)]
        private static void BuildWebGLFromMenu()
        {
            BuildVersionStamper.BuildWithResolvedVersion(() => BuildFor(new ClientBuildOptions(ClientPlatform.WebGL)));
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

        public static IReadOnlyList<string> ListedScenes(ClientPlatform platform)
        {
            switch (platform)
            {
                case ClientPlatform.Windows:
                case ClientPlatform.MacOS:
                    return BuildSceneList.Scenes;
                case ClientPlatform.WebGL:
                    return BuildSceneList.WebScenes;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }
        }

        public static BuildTarget ToBuildTarget(ClientPlatform platform)
        {
            switch (platform)
            {
                case ClientPlatform.Windows:
                    return BuildTarget.StandaloneWindows64;
                case ClientPlatform.MacOS:
                    return BuildTarget.StandaloneOSX;
                case ClientPlatform.WebGL:
                    return BuildTarget.WebGL;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }
        }

        public static BuildTargetGroup ToBuildTargetGroup(ClientPlatform platform)
        {
            switch (platform)
            {
                case ClientPlatform.Windows:
                case ClientPlatform.MacOS:
                    return BuildTargetGroup.Standalone;
                case ClientPlatform.WebGL:
                    return BuildTargetGroup.WebGL;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }
        }

        // PlayerSettings のシンボルに書くと他のターゲットや Editor にも効いてしまうので、このビルドだけに足す。
        public static string[] ExtraScriptingDefines(ClientPlatform platform)
        {
            switch (platform)
            {
                case ClientPlatform.Windows:
                case ClientPlatform.MacOS:
                    return Array.Empty<string>();
                case ClientPlatform.WebGL:
                    return new[] { EosDisableDefine };
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }
        }

        public static string RequiredModuleName(ClientPlatform platform)
        {
            switch (platform)
            {
                case ClientPlatform.Windows:
                    return "Windows Build Support (Mono)";
                case ClientPlatform.MacOS:
                    return "Mac Build Support (Mono)";
                case ClientPlatform.WebGL:
                    return "Web Build Support";
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
            var group = ToBuildTargetGroup(options.Platform);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                throw new InvalidOperationException(
                    LogTag + " ClientBuilder: " + target + " is not installed for this Unity editor; add the "
                    + RequiredModuleName(options.Platform) + " module in Unity Hub");
            }

            var scenes = RequireScenes(options.Platform);

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
                targetGroup = group,
                options = BuildOptions.StrictMode,
                extraScriptingDefines = ExtraScriptingDefines(options.Platform),
            };
            if (group == BuildTargetGroup.Standalone)
            {
                buildOptions.subtarget = (int)StandaloneBuildSubtarget.Player;
            }

            Debug.Log(LogTag + " ClientBuilder: building " + options + " version " + PlayerSettings.bundleVersion
                + " with " + string.Join(", ", scenes));
            var report = options.Platform == ClientPlatform.WebGL
                ? WithStaticHostCompression(() => BuildPipeline.BuildPlayer(buildOptions))
                : BuildPipeline.BuildPlayer(buildOptions);
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

        // GitHub Pages は Content-Encoding を付けられないので、Brotli ではなく gzip をブラウザ側で展開する設定にする。
        // ProjectSettings に差分を残さないよう、ビルドが終わったら戻す。
        private static BuildReport WithStaticHostCompression(Func<BuildReport> build)
        {
            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            try
            {
                return build();
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
            }
        }

        // 起動時のシーンが無いと起動しても何も出ないビルドになるので例外を投げるが、他に無いシーンは BuildSceneList.RegisterAll と同じく警告を出して飛ばす。
        // MainMenu は git に無いので batchmode (CI) では作ってから入れ、Editor からは手元にシーンが黙って増えないよう作らない。
        private static string[] RequireScenes(ClientPlatform platform)
        {
            Func<string, bool> exists = path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
            if (Application.isBatchMode && !exists(BuildSceneList.MainMenuScene))
            {
                Debug.Log(LogTag + " ClientBuilder: " + BuildSceneList.MainMenuScene + " is missing; building it with MenuSceneBuilder");
                MenuSceneBuilder.BuildAt(MenuSceneBuilder.ScenePath);
            }

            var skipped = new List<string>();
            var scenes = ClientScenePaths(ListedScenes(platform), exists, skipped);

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
