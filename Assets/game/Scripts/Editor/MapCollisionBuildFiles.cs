using System.IO;
using Fps.Runtime;
using UnityEditor.Build;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// Assets に写しを置かず、ビルドの StreamingAssets にだけ server-data/maps を入れる (<see cref="MapCollisionFiles"/>)。
    /// </summary>
    public sealed class MapCollisionBuildFiles : BuildPlayerProcessor
    {
        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            var directory = MapCollisionFiles.ServerDataDirectory;
            if (!Directory.Exists(directory))
            {
                Debug.LogWarning(MapCollisionExporter.LogTag + " " + directory
                    + " does not exist, so the build has no map collision and network players cannot move; run make export-server-data");
                return;
            }

            buildPlayerContext.AddAdditionalPathToStreamingAssets(directory, MapCollisionFiles.StreamingAssetsFolder);
            Debug.Log(MapCollisionExporter.LogTag + " added " + directory + " to StreamingAssets/" + MapCollisionFiles.StreamingAssetsFolder);
        }
    }
}
