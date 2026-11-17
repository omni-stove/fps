using System.IO;
using Shooter.Runtime;
using UnityEditor.Build;
using UnityEngine;

namespace Shooter.Editor
{
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
