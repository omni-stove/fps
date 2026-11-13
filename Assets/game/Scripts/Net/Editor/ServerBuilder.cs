using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Net;
using Fps.Runtime;
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
        /// 専用サーバーのビルドに入れるシーン (<see cref="BuildSceneList.ServerScenes"/>)。
        /// 一人称カメラの Bootstrap は要らないので、NetTest を最初のシーンにしてそのまま待ち受ける。
        /// </summary>
        public static string[] ServerScenePaths()
        {
            var scenes = new string[BuildSceneList.ServerScenes.Count];
            for (var i = 0; i < scenes.Length; i++)
            {
                scenes[i] = BuildSceneList.ServerScenes[i];
            }

            return scenes;
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
            RequireLoadoutInBuild();

            Directory.CreateDirectory(options.OutputDirectory);
            var buildOptions = new BuildPlayerOptions
            {
                scenes = ServerScenePaths(),
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
        /// NetTest シーンとプレイヤーのプレハブが無いか、試合の進行役にロードアウトが配線されていなければ生成する。
        /// そろっていれば作り直さない (プレハブを作り直すと、すでに配ったクライアントのビルドと食い違うおそれがあるため)。
        /// </summary>
        private static void EnsureNetScene()
        {
            var hasScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(NetBootstrap.ScenePath) != null;
            var hasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetSceneBuilder.PlayerPrefabPath) != null;
            var hasLoadout = MatchLoadout() != null;
            if (hasScene && hasPrefab && hasLoadout)
            {
                return;
            }

            Debug.Log("[Net] ServerBuilder: " + NetBootstrap.ScenePath + " or " + NetSceneBuilder.PlayerPrefabPath
                + " is missing, or " + NetSceneBuilder.MatchPrefabPath + " has no usable loadout;"
                + " running NetSceneBuilder.Build (commit the generated files)");
            NetSceneBuilder.Build();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NetBootstrap.ScenePath) == null)
            {
                throw new InvalidOperationException(
                    "[Net] ServerBuilder: " + NetBootstrap.ScenePath + " was not created by NetSceneBuilder.Build");
            }

            if (MatchLoadout() == null)
            {
                throw new InvalidOperationException(
                    "[Net] ServerBuilder: " + NetSceneBuilder.MatchPrefabPath + " has no usable loadout after NetSceneBuilder.Build");
            }
        }

        /// <summary>
        /// 試合の進行役のプレハブに配線された、サーバーが起動時に読めるロードアウト。無い・読めなければ null。
        /// 無いままビルドすると専用サーバーは試合を始められずに終わるので、ビルドの前に確かめる。
        /// </summary>
        private static LoadoutDefinition MatchLoadout()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetSceneBuilder.MatchPrefabPath);
            var match = prefab != null ? prefab.GetComponent<NetTeamDeathmatch>() : null;
            if (match == null || !NetTeamDeathmatch.TryCreateLoadout(match.Loadout, out _, out _))
            {
                return null;
            }

            return match.Loadout;
        }

        /// <summary>
        /// 試合の進行役のロードアウトとその武器の定義が、ビルドに入れるシーン (<see cref="NetBootstrap.ScenePath"/>) から
        /// 参照をたどって届き、プレイヤーに含まれることを確かめる。届かなければ例外を投げる。
        /// </summary>
        private static void RequireLoadoutInBuild()
        {
            var loadout = MatchLoadout();
            if (loadout == null)
            {
                throw new InvalidOperationException(
                    "[Net] ServerBuilder: " + NetSceneBuilder.MatchPrefabPath + " has no usable loadout");
            }

            var missing = MissingFromScene(NetBootstrap.ScenePath, LoadoutAssetPaths(loadout));
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "[Net] ServerBuilder: " + string.Join(", ", missing) + " would not be in the build of " + NetBootstrap.ScenePath
                    + "; run NetSceneBuilder.Build");
            }

            Debug.Log("[Net] ServerBuilder: the build includes loadout " + AssetDatabase.GetAssetPath(loadout));
        }

        /// <summary>loadout のアセットと、そのスロットに入っている武器の定義のアセットのパス。保存されていないアセットは空文字になる。</summary>
        public static IReadOnlyList<string> LoadoutAssetPaths(LoadoutDefinition loadout)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            var paths = new List<string>();
            AddAssetPath(paths, loadout);
            foreach (var slot in loadout.Slots)
            {
                if (slot != null && slot.Weapon != null)
                {
                    AddAssetPath(paths, slot.Weapon);
                }
            }

            return paths;
        }

        /// <summary>
        /// required のうち、scenePath のシーンから参照をたどって届かない (ビルドに入らない) アセットのパス。
        /// 空のパスは保存されていないアセットなので、届かないものとして返す。
        /// </summary>
        public static IReadOnlyList<string> MissingFromScene(string scenePath, IEnumerable<string> required)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new ArgumentException("scenePath must not be empty", nameof(scenePath));
            }

            if (required == null)
            {
                throw new ArgumentNullException(nameof(required));
            }

            var dependencies = new HashSet<string>(AssetDatabase.GetDependencies(scenePath, true), StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (var path in required)
            {
                if (string.IsNullOrEmpty(path) || !dependencies.Contains(path))
                {
                    missing.Add(string.IsNullOrEmpty(path) ? "(unsaved asset)" : path);
                }
            }

            return missing;
        }

        private static void AddAssetPath(List<string> paths, UnityEngine.Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (!paths.Contains(path))
            {
                paths.Add(path);
            }
        }
    }
}
