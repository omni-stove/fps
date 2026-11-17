using System.Collections.Generic;
using UnityEditor;

namespace Shooter.Editor
{
    [InitializeOnLoad]
    public static class ServerDataMaps
    {
        static ServerDataMaps()
        {
            MapCollisionExporter.UseMapScenes(Prepare);
        }

        // まだ生成していないマップがあっても書き出せるよう、無いシーンを作ってから渡す
        public static IReadOnlyList<string> Prepare()
        {
            EnsureScenes();
            return BuildSceneList.ServerScenes;
        }

        public static void EnsureScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NetSceneBuilder.ScenePath) == null)
            {
                NetSceneBuilder.Build();
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaSceneBuilder.ScenePath) == null)
            {
                ArenaSceneBuilder.BuildAt(ArenaSceneBuilder.ScenePath, ArenaSceneBuilder.NavMeshDataPath);
            }
        }
    }
}
