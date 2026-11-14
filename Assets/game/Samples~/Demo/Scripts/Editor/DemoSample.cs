using System.IO;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 取り込んだサンプルのフォルダ (<see cref="Root"/>) を、Editor の読み込み時に出力先ルートの既定 (<see cref="FpsKitRoot.UseDefaultOutput"/>) にする。
    /// サンプルは Package Manager から取り込むと <c>Assets/Samples/&lt;パッケージ&gt;/&lt;版&gt;/&lt;サンプル&gt;</c> に置かれ、場所が決まっていないので、
    /// このアセンブリの asmdef の場所から決める。
    /// </summary>
    [InitializeOnLoad]
    public static class DemoSample
    {
        public const string AssemblyName = "Fps.Samples.Editor";
        public const string LogTag = "[DemoSample]";

        /// <summary>asmdef から見たサンプルのルートの位置 (<c>&lt;ルート&gt;/Scripts/Editor/</c>)。</summary>
        private const int AsmdefDepth = 2;

        /// <summary>メニューのシーンの設定 (<see cref="MenuSceneSettings"/>) を置く、ルートの下のパス。</summary>
        public const string MenuSceneSettingsRelativePath = "Resources/" + MenuSceneSettings.ResourcePath + ".asset";

        static DemoSample()
        {
            var root = Root;
            if (root != null)
            {
                FpsKitRoot.UseDefaultOutput(root);
            }
        }

        /// <summary>サンプルのルート (例: <c>Assets/Samples/FpsKitDemo</c>)。asmdef が Assets/ の下に見つからなければ null。</summary>
        public static string Root => RootOf(CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(AssemblyName));

        /// <summary>asmdefPath (<c>&lt;ルート&gt;/Scripts/Editor/*.asmdef</c>) のルート。Assets/ の下でなければ null。</summary>
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

        /// <summary>
        /// 出力先ルートの <see cref="MenuSceneSettingsRelativePath"/> に、シーンをそのルートから読む <see cref="MenuSceneSettings"/> を作るか上書きする。
        /// シーンの名前は既定のまま。
        /// </summary>
        public static void WriteMenuSceneSettings()
        {
            var root = FpsKitRoot.Output;
            var path = KitPaths.Combine(root, MenuSceneSettingsRelativePath);
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
