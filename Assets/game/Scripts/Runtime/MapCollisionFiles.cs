using System.IO;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // エディタではコミットした server-data/maps を直接読み、ビルドでは StreamingAssets に入れた写しを読む。
    // 写しをコミットしないのは、書き出しのたびに 2 か所を揃える手間と食い違いをなくすため (写しはビルドのときだけ作る)。
    public static class MapCollisionFiles
    {
        public const string ServerDataFolder = "server-data/maps";

        public const string StreamingAssetsFolder = "maps";

        public static string ServerDataDirectory => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ServerDataFolder);

        public static string Directory => Application.isEditor
            ? ServerDataDirectory
            : Path.Combine(Application.streamingAssetsPath, StreamingAssetsFolder);

        public static string PathOf(string map)
        {
            return Path.Combine(Directory, map + ShooterMapFile.Extension);
        }
    }
}
