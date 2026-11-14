using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 陣取り系のモードの調整値のアセット (<see cref="DominationDefinition"/> と <see cref="HardpointDefinition"/>) を、
    /// シーンを組まずに用意する。置き場所と作り方は <see cref="ModeDefinitionAssets"/> に従う。値が不正なら例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ModeTuningAssetBuilder.Build</code>
    /// </summary>
    public static class ModeTuningAssetBuilder
    {
        public const string MenuPath = "Fps/Build Mode Tuning Assets";
        public const string LogTag = "[ModeTuningAssetBuilder]";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            var domination = DominationSceneBuilder.LoadOrCreateDefinition(DominationSceneBuilder.DefinitionPath);
            var hardpoint = HardpointSceneBuilder.LoadOrCreateDefinition(HardpointSceneBuilder.DefinitionPath);
            Debug.Log($"{LogTag} ready '{AssetDatabase.GetAssetPath(domination)}', '{AssetDatabase.GetAssetPath(hardpoint)}'");
        }
    }
}
