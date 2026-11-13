using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// macOS のプレイヤー (クライアント) をビルドして <see cref="OutputPath"/> に出力する。入れるシーンは Build Settings で有効なもの
    /// (<see cref="BuildSceneList.RegisterAll"/> が並べる) で、先頭が起動時のシーンになる。
    /// 版は <see cref="BuildVersionStamper"/> が <c>-buildVersion</c> か git describe から決め、ビルドの間だけ bundleVersion に入れる。
    /// Editor を閉じた状態で次のように呼ぶ (<c>make build</c>):
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ClientBuilder.Build [-buildVersion v0.2.0]</code>
    /// 失敗したら例外を投げるので、batchmode の Unity は終了コード 1 で終わる。
    /// </summary>
    public static class ClientBuilder
    {
        public const string MenuPath = "Fps/Build Client (macOS)";

        /// <summary>出力先 (プロジェクトのルートからの相対パス、gitignore 済み)。</summary>
        public const string OutputPath = "Builds/fps.app";

        public static void Build()
        {
            BuildVersionStamper.BuildWithResolvedVersion(BuildMacOS);
        }

        [MenuItem(MenuPath)]
        private static void BuildFromMenu()
        {
            Build();
        }

        /// <summary>Build Settings で有効なシーンのパスを、並んでいる順に返す。</summary>
        public static string[] EnabledScenePaths(IReadOnlyList<EditorBuildSettingsScene> scenes)
        {
            var paths = new List<string>();
            if (scenes == null)
            {
                return paths.ToArray();
            }

            foreach (var scene in scenes)
            {
                if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    paths.Add(scene.path);
                }
            }

            return paths.ToArray();
        }

        private static BuildReport BuildMacOS()
        {
            const BuildTarget target = BuildTarget.StandaloneOSX;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                throw new InvalidOperationException(
                    "[Build] ClientBuilder: " + target + " is not installed for this Unity editor; add the Mac Build Support module in Unity Hub");
            }

            var scenes = EnabledScenePaths(EditorBuildSettings.scenes);
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException(
                    "[Build] ClientBuilder: no scene is enabled in the build settings; run " + BuildSceneList.MenuPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.StrictMode,
            };

            Debug.Log("[Build] ClientBuilder: building " + OutputPath + " version " + PlayerSettings.bundleVersion
                + " with " + string.Join(", ", scenes));
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "[Build] ClientBuilder: build " + summary.result + " with " + summary.totalErrors + " error(s); see the log above");
            }

            Debug.Log("[Build] ClientBuilder: wrote " + summary.outputPath + " (" + summary.totalSize + " bytes, "
                + summary.totalWarnings + " warning(s), " + summary.totalTime + ")");
            return report;
        }
    }
}
