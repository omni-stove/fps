using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using SceneZipline = Shooter.Runtime.Traversal.Zipline;

namespace Shooter.Editor
{
    // サーバーはシーンを読めないので、ネットのマップに置いたジップラインを server-data/maps/<マップ>.ziplines.json に書く。
    // 書くのは make export-server-data (MapCollisionExporter.Export) で、当たりやはしごと同じく毎晩の書き出し直しで食い違いに気づける。
    [InitializeOnLoad]
    public static class ServerDataZiplines
    {
        public const string LogTag = "[Net] ServerDataZiplines:";

        static ServerDataZiplines()
        {
            MapCollisionExporter.UseSceneData(NetMapZiplines.Extension, roots => NetMapZiplines.Write(Collect(roots)));
        }

        // 無効な線はプレイ中に掴まれない (Zipline.IsUsable が偽) ので書き出さない。
        // 掴めない線を書き出さずに黙って進めると、オフラインでだけ掴める線になるので、壊れた線があれば断る
        public static NetMapZiplines Collect(IReadOnlyList<GameObject> roots)
        {
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            var ziplines = new List<MapZipline>();
            var invalid = new List<string>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var line in root.GetComponentsInChildren<SceneZipline>(false))
                {
                    if (!line.enabled)
                    {
                        continue;
                    }

                    try
                    {
                        ziplines.Add(ToMapZipline(line));
                    }
                    catch (ArgumentException e)
                    {
                        invalid.Add(PathOf(line.transform) + " (" + e.Message + ")");
                    }
                }
            }

            if (invalid.Count > 0)
            {
                throw new InvalidOperationException(LogTag + " these ziplines have invalid settings: " + string.Join(", ", invalid));
            }

            return new NetMapZiplines(ziplines);
        }

        public static MapZipline ToMapZipline(SceneZipline line)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            return new MapZipline(line.StartPoint.ToNumerics(), line.EndPoint.ToNumerics(), line.CreateSpec());
        }

        private static string PathOf(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }
    }
}
