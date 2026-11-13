using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// クライアント (プレイヤー) をビルドして <c>Builds/client-&lt;platform&gt;/</c> に出力する。
    /// 入れるシーンは <see cref="BuildSceneList.Scenes"/> のうちプロジェクトにあるもので、先頭の <see cref="BuildSceneList.MainMenuScene"/> が起動時のシーンになる。
    /// 版は <see cref="BuildVersionStamper"/> が <c>-buildVersion</c> か git describe から決め、ビルドの間だけ bundleVersion に入れる。
    /// Editor を閉じた状態で次のように呼ぶ (<c>make build</c> / <c>make build windows</c>):
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ClientBuilder.Build -clientPlatform windows|macos [-buildVersion v0.2.0]</code>
    /// 失敗したら例外を投げるので、batchmode の Unity は終了コード 1 で終わる。
    /// </summary>
    public static class ClientBuilder
    {
        public const string LogTag = "[Build]";
        public const string MenuPathWindows = "Fps/Build Client (Windows)";
        public const string MenuPathMacOS = "Fps/Build Client (macOS)";

        /// <summary>
        /// コマンドライン引数 <c>-clientPlatform</c> (既定は macos) で決まる OS 向けに、
        /// <c>-buildVersion</c> (無ければ git describe) の版でビルドする。
        /// </summary>
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

        /// <summary>
        /// listed のうち exists が true のものを、この順に返す。無かったものは skipped に足す (null なら足さない)。
        /// Build Settings にはテンプレートの残りのシーンも入っているので、そちらではなく一覧から決める。
        /// </summary>
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

        /// <summary>BuildPlayerOptions に渡すターゲット。</summary>
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

        /// <summary>
        /// options の OS 向けにクライアントをビルドする。出力先のディレクトリは前のビルドの残り (消えたプラグインなど) を
        /// 配る zip に混ぜないよう、空にしてから書き出す。
        /// </summary>
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

        /// <summary>
        /// ビルドに入れるシーン。起動時のシーンが無ければ、起動しても何も出ないビルドになるので例外を投げる。
        /// 他に無いシーンは警告を出して飛ばす (<see cref="BuildSceneList.RegisterAll"/> と同じ扱い)。
        /// </summary>
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
