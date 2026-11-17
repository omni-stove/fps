using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Shooter.Editor
{
    // サンプルは Package Manager から取り込むと Assets/Samples/<パッケージ>/<版>/<サンプル> に置かれ場所が決まっていないので、
    // ルートはこのアセンブリの asmdef の場所から決める。
    [InitializeOnLoad]
    public static class DemoSample
    {
        public const string AssemblyName = "Shooter.Samples.Editor";
        public const string LogTag = "[DemoSample]";

        private const int AsmdefDepth = 2;

        public const string MenuSceneSettingsRelativePath = "Resources/" + MenuSceneSettings.ResourcePath + ".asset";

        static DemoSample()
        {
            var root = Root;
            if (root != null)
            {
                ShooterKitRoot.UseDefaultOutput(root);
            }
        }

        public static string Root => RootOf(CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(AssemblyName));

        public static string RootOf(string asmdefPath)
        {
            if (string.IsNullOrWhiteSpace(asmdefPath))
            {
                return null;
            }

            var folder = asmdefPath.Replace('\\', '/');
            for (var i = 0; i <= AsmdefDepth; i++)
            {
                var slash = folder.LastIndexOf('/');
                if (slash <= 0)
                {
                    return null;
                }

                folder = folder.Substring(0, slash);
            }

            return folder.StartsWith("Assets/", System.StringComparison.Ordinal) ? folder : null;
        }

        public static void WriteMenuSceneSettings()
        {
            var root = ShooterKitRoot.Output;
            // 出力先がサンプルのルートと違っても、同梱の設定そのものを更新する。別の場所に作ると同じ Resources パスが 2 つになり、どちらが読まれるか決まらない。
            var path = KitPaths.Combine(Root ?? root, MenuSceneSettingsRelativePath);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            SceneBuildHelpers.CreateOrUpdateAsset<MenuSceneSettings>(path, s =>
            {
                s.ResetToDefaults();
                s.SceneRoot = root;
            });
            Debug.Log($"{LogTag} saved '{path}' (scene root '{root}')");
        }
    }
}
