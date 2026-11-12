using System;
using System.IO;
using Fps.Core;
using Fps.Net;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 専用サーバー (Dedicated Server) をビルドして <c>Builds/server-&lt;platform&gt;/</c> に出力する。
    /// 入れるシーンは NetTest だけで、起動するとそのままサーバーとして待ち受ける (<see cref="NetStartup"/>)。
    /// Editor を閉じた状態で次のように呼ぶ (<c>make build-server</c> / <c>make build-server linux</c>):
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ServerBuilder.Build -serverPlatform macos|linux</code>
    /// 失敗したら例外を投げるので、batchmode の Unity は終了コード 1 で終わる。
    /// </summary>
    public static class ServerBuilder
    {
        public const string MenuPathMacOS = "Fps/Net/Build Dedicated Server (macOS)";
        public const string MenuPathLinux = "Fps/Net/Build Dedicated Server (Linux)";

        /// <summary>コマンドライン引数 <c>-serverPlatform</c> (既定は macos) で決まる OS 向けにビルドする。</summary>
        public static void Build()
        {
            if (!ServerBuildOptions.TryParse(
                    Environment.GetCommandLineArgs(), ServerPlatform.MacOS, out var options, out var error))
            {
                throw new ArgumentException("[Net] ServerBuilder: invalid arguments: " + error);
            }

            BuildFor(options);
        }

        [MenuItem(MenuPathMacOS)]
        private static void BuildMacOSFromMenu()
        {
            BuildFor(new ServerBuildOptions(ServerPlatform.MacOS));
        }

        [MenuItem(MenuPathLinux)]
        private static void BuildLinuxFromMenu()
        {
            BuildFor(new ServerBuildOptions(ServerPlatform.Linux));
        }

        /// <summary>
        /// NetTest シーンが無ければ <see cref="NetSceneBuilder.Build"/> で作り、専用サーバーをビルドする。
        /// </summary>
        public static BuildReport BuildFor(ServerBuildOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var target = ToBuildTarget(options.Platform);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                throw new InvalidOperationException(
                    "[Net] ServerBuilder: " + target + " is not installed for this Unity editor; add the "
                    + ServerBuildOptions.PlatformName(options.Platform)
                    + " Build Support and Dedicated Server Build Support modules in Unity Hub");
            }

            EnsureNetScene();

            Directory.CreateDirectory(options.OutputDirectory);
            var buildOptions = new BuildPlayerOptions
            {
                // サーバーには一人称カメラの Bootstrap は要らない。NetTest を最初のシーンにしてそのまま待ち受ける
                scenes = new[] { NetBootstrap.ScenePath },
                locationPathName = options.ExecutablePath,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.StrictMode,
            };

            Debug.Log("[Net] ServerBuilder: building dedicated server " + options);
            var report = BuildPipeline.BuildPlayer(buildOptions);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "[Net] ServerBuilder: build " + summary.result + " for " + options
                    + " with " + summary.totalErrors + " error(s); see the log above");
            }

            Debug.Log("[Net] ServerBuilder: wrote " + summary.outputPath + " (" + summary.totalSize + " bytes, "
                + summary.totalWarnings + " warning(s), " + summary.totalTime + ")");
            return report;
        }

        /// <summary>BuildPlayerOptions に渡すターゲット。どちらも Standalone で、subtarget に Server を指定して使う。</summary>
        public static BuildTarget ToBuildTarget(ServerPlatform platform)
        {
            switch (platform)
            {
                case ServerPlatform.MacOS:
                    return BuildTarget.StandaloneOSX;
                case ServerPlatform.Linux:
                    return BuildTarget.StandaloneLinux64;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform");
            }
        }

        /// <summary>
        /// NetTest シーンとプレイヤーのプレハブが無ければ生成する。既にあれば作り直さない
        /// (プレハブを作り直すと、すでに配ったクライアントのビルドと食い違うおそれがあるため)。
        /// </summary>
        private static void EnsureNetScene()
        {
            var hasScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(NetBootstrap.ScenePath) != null;
            var hasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetSceneBuilder.PlayerPrefabPath) != null;
            if (hasScene && hasPrefab)
            {
                return;
            }

            Debug.Log("[Net] ServerBuilder: " + NetBootstrap.ScenePath + " or " + NetSceneBuilder.PlayerPrefabPath
                + " is missing; running NetSceneBuilder.Build (commit the generated files)");
            NetSceneBuilder.Build();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NetBootstrap.ScenePath) == null)
            {
                throw new InvalidOperationException(
                    "[Net] ServerBuilder: " + NetBootstrap.ScenePath + " was not created by NetSceneBuilder.Build");
            }
        }
    }
}
