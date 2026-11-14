using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fps.Editor
{
    // make export-server-data: unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ServerDataExporter.Export [-serverMap <scene>]...
    public static class ServerDataExporter
    {
        public const string MenuPath = "Fps/Server Data/Export Maps";
        public const string MapArgumentName = "-serverMap";
        public const string OutputFolder = "server-data/maps";
        public const string LogTag = "[ServerDataExporter]";

        // どのシーンが対戦マップかはゲームごとに違うので、キットは決めず、ゲーム (サンプル) の Editor が読み込み時に登録する。
        private static Func<IReadOnlyList<string>> _maps;

        public static string OutputDirectory => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", OutputFolder);

        public static void UseMaps(Func<IReadOnlyList<string>> maps)
        {
            _maps = maps;
        }

        public static void Export()
        {
            Export(ResolveMaps(Environment.GetCommandLineArgs(), _maps), OutputDirectory);
        }

        public static IReadOnlyList<string> Export(IReadOnlyList<string> scenePaths, string outputDirectory)
        {
            if (scenePaths == null || scenePaths.Count == 0)
            {
                throw new ArgumentException($"{LogTag} no map scenes to export", nameof(scenePaths));
            }

            var written = new List<string>();
            foreach (var scenePath in scenePaths)
            {
                written.Add(NavMeshExporter.Export(scenePath, outputDirectory));
            }

            Debug.Log($"{LogTag} wrote {written.Count} file(s) to {outputDirectory}; commit them");
            return written;
        }

        public static IReadOnlyList<string> ResolveMaps(IReadOnlyList<string> args, Func<IReadOnlyList<string>> registered)
        {
            var fromArgs = new List<string>();
            for (var i = 0; args != null && i < args.Count; i++)
            {
                if (!string.Equals(args[i], MapArgumentName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{LogTag} {MapArgumentName} needs a scene path", nameof(args));
                }

                fromArgs.Add(args[i + 1]);
                i++;
            }

            if (fromArgs.Count > 0)
            {
                return fromArgs;
            }

            var maps = registered?.Invoke();
            if (maps == null || maps.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{LogTag} no map scenes: pass {MapArgumentName} <scene path>, or register them with {nameof(ServerDataExporter)}.{nameof(UseMaps)}");
            }

            return maps;
        }

        [MenuItem(MenuPath)]
        private static void ExportFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Export();
            }
            finally
            {
                if (setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }
        }
    }
}
