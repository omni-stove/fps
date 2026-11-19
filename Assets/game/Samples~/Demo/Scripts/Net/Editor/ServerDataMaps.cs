using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    [InitializeOnLoad]
    public static class ServerDataMaps
    {
        public const string LogTag = "[Net] ServerDataMaps:";

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

            EnsureFlagBases();
        }

        // 旗の台を置く前に作ったシーンは、作り直すと NavMesh や壊れる建物まで変わってしまうので、台だけを足して書き出しに載せる。
        public static IReadOnlyList<string> EnsureFlagBases()
        {
            float? captureRadius = null;
            float Radius()
            {
                captureRadius ??= NetSceneBuilder.CaptureRadius(NetSceneBuilder.LoadOrBuildMatch());
                return captureRadius.Value;
            }

            var added = new List<string>();
            if (AddFlagBasesIfMissing(NetSceneBuilder.ScenePath, NetSceneBuilder.FlagBases, Radius))
            {
                added.Add(NetSceneBuilder.ScenePath);
            }

            if (AddFlagBasesIfMissing(ArenaSceneBuilder.ScenePath, ArenaSceneBuilder.FlagBases, () =>
                {
                    var radius = Radius();
                    var overlaps = ArenaSceneBuilder.FlagBaseOverlaps(radius);
                    if (overlaps.Count > 0)
                    {
                        throw new InvalidOperationException(LogTag + " flag base areas (capture radius " + radius + " m) overlap "
                            + ArenaSceneBuilder.ScenePath + ": " + string.Join("; ", overlaps));
                    }

                    return radius;
                }))
            {
                added.Add(ArenaSceneBuilder.ScenePath);
            }

            return added;
        }

        // 台が 1 つでもあれば、ゲームが置き直したものとみなして触らない。
        public static bool AddFlagBasesIfMissing(string scenePath, IReadOnlyList<(Side Side, Vector3 Position)> bases, Func<float> captureRadius)
        {
            if (captureRadius == null)
            {
                throw new ArgumentNullException(nameof(captureRadius));
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new FileNotFoundException(LogTag + " " + scenePath + " is missing", scenePath);
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(root => root.GetComponentsInChildren<FlagBase>(true).Length > 0))
            {
                return false;
            }

            var radius = captureRadius();
            NetSceneBuilder.CreateFlagBases(scene, bases, radius);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException(LogTag + " failed to save " + scenePath);
            }

            Debug.Log(LogTag + " added " + bases.Count + " flag base(s) (capture radius " + radius + " m) to " + scenePath + "; commit it");
            return true;
        }
    }
}
