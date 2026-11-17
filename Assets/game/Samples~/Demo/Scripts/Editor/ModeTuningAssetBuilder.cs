using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public static class ModeTuningAssetBuilder
    {
        public const string MenuPath = "Shooter/Build Mode Tuning Assets";
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
