using System.Collections.Generic;
using UnityEditor;

namespace Fps.Editor
{
    [InitializeOnLoad]
    public static class ServerDataMaps
    {
        static ServerDataMaps()
        {
            MapCollisionExporter.UseMapScenes(Prepare);
        }

        // まだ生成していないマップがあっても書き出せるよう、サーバーのビルドと同じく無いシーンを作ってから渡す
        public static IReadOnlyList<string> Prepare()
        {
            ServerBuilder.EnsureServerScenes();
            return BuildSceneList.ServerScenes;
        }
    }
}
