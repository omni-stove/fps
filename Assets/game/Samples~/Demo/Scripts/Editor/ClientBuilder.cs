using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Shooter.Editor
{
    /// <summary>
    /// クライアント (プレイヤー) をビルドして <c>Builds/client-&lt;platform&gt;/</c> に出力する。
    /// 入れるシーンは <see cref="ListedScenes"/> のうちプロジェクトにあるもので、先頭の <see cref="BuildSceneList.MainMenuScene"/> が起動時のシーンになる。
    /// 版は <see cref="BuildVersionStamper"/> が <c>-buildVersion</c> か git describe から決め、ビルドの間だけ bundleVersion に入れる。
    /// Editor を閉じた状態で次のように呼ぶ (<c>make build</c> / <c>make build windows</c> / <c>make build webgl</c>):
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Shooter.Editor.ClientBuilder.Build -clientPlatform windows|macos|webgl [-buildVersion v0.2.0]</code>
    /// 失敗したら例外を投げるので、batchmode の Unity は終了コード 1 で終わる。
    /// </summary>
    public static class ClientBuilder
    {
        public const string LogTag = "[Build]";
        public const string MenuPathWindows = "Shooter/Build Client (Windows)";
        public const string MenuPathMacOS = "Shooter/Build Client (macOS)";
        public const string MenuPathWebGL = "Shooter/Build Client (WebGL)";

        /// <summary>
        /// WebGL のビルドで EOS のパッケージ (と、それに頼る Shooter.NetEos) を外すシンボル。
        /// EOS の SDK にはブラウザ向けのネイティブライブラリが無い。
        /// </summary>
        public const string EosDisableDefine = "EOS_DISABLE";

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

        [MenuItem(MenuPathWebGL)]
        private static void BuildWebGLFromMenu()
        {
            BuildVersionStamper.BuildWithResolvedVersion(() => BuildFor(new ClientBuildOptions(ClientPlatform.WebGL)));
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

        /// <summary>
        /// platform のビルドに入れる候補のシーン。WebGL はオンラインを使えないので、メニューから行けるオフラインのものだけ
        /// (<see cref="BuildSceneList.WebScenes"/>)。ほかは <see cref="BuildSceneList.Scenes"/>。
        /// </summary>
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

        /// <summary>BuildPlayerOptions に渡すターゲット。</summary>
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

        /// <summary>BuildPlayerOptions に渡すターゲットのグループ。</summary>
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

        /// <summary>このビルドだけに足すスクリプトのシンボル。PlayerSettings に書かないので、ほかのターゲットや Editor には効かない。</summary>
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

        /// <summary>そのターゲットでビルドするために Unity Hub で入れるモジュールの名前。</summary>
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

        /// <summary>
        /// GitHub Pages のような、Content-Encoding を付けられない静的ホストでも読めるように、gzip で圧縮し
        /// ブラウザ側で展開する (decompressionFallback) 設定でビルドする。Brotli のほうが小さいが、展開の手段が無いホストでは読めない。
        /// 終わったら元の設定に戻し、ProjectSettings に差分を残さない。
        /// </summary>
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

        /// <summary>
        /// ビルドに入れるシーン。起動時のシーンが無ければ、起動しても何も出ないビルドになる。
        /// batchmode (CI) では <see cref="MenuSceneBuilder.BuildAt"/> で作ってから入れ、Editor からは作らずに例外を投げる
        /// (作ったシーンが手元に黙って増えないように)。他に無いシーンは警告を出して飛ばす (<see cref="BuildSceneList.RegisterAll"/> と同じ扱い)。
        /// </summary>
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
