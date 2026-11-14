using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Core.Navigation;
using Fps.Runtime;
using Fps.Runtime.Traversal;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using NumericsVector3 = System.Numerics.Vector3;

namespace Fps.Editor
{
    public static class NavMeshExporter
    {
        public const string LogTag = "[NavMeshExporter]";

        // CalculateTriangulation は読み込まれているシーンすべての NavMesh を返すので、scene だけを開いて呼ぶ。
        public static NavMeshFile Capture(Scene scene)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            var triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.indices == null || triangulation.indices.Length == 0)
            {
                throw new InvalidOperationException($"{LogTag} scene '{scene.path}' has no baked NavMesh loaded");
            }

            var agentTypeId = AgentTypeOf(scene);
            return FromTriangulation(
                triangulation.vertices, triangulation.indices, triangulation.areas, AreaCosts(), CollectLinks(scene, agentTypeId));
        }

        public static NavMeshFile FromTriangulation(
            IReadOnlyList<Vector3> vertices, IReadOnlyList<int> indices, IReadOnlyList<int> areas, IReadOnlyList<float> areaCosts, IReadOnlyList<NavLink> links)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            if (areas == null)
            {
                throw new ArgumentNullException(nameof(areas));
            }

            if (indices.Count % 3 != 0)
            {
                throw new ArgumentException($"{LogTag} {indices.Count} indices is not a whole number of triangles", nameof(indices));
            }

            var triangleCount = indices.Count / 3;
            if (areas.Count != triangleCount)
            {
                throw new ArgumentException($"{LogTag} {areas.Count} areas for {triangleCount} triangles", nameof(areas));
            }

            var points = new NumericsVector3[vertices.Count];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = vertices[i].ToNumerics();
            }

            var triangles = new NavTriangle[triangleCount];
            for (var i = 0; i < triangleCount; i++)
            {
                triangles[i] = new NavTriangle(indices[i * 3], indices[(i * 3) + 1], indices[(i * 3) + 2], areas[i]);
            }

            return new NavMeshFile(points, triangles, areaCosts, links);
        }

        public static float[] AreaCosts()
        {
            var costs = new float[NavMeshFile.AreaCount];
            for (var i = 0; i < costs.Length; i++)
            {
                costs[i] = NavMesh.GetAreaCost(i);
            }

            return costs;
        }

        // 種類の違う NavMeshSurface が混ざると、三角形がどの種類の分か分けられないので例外にする。
        public static int AgentTypeOf(Scene scene)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            int? found = null;
            foreach (var surface in Components<NavMeshSurface>(scene))
            {
                if (!surface.isActiveAndEnabled)
                {
                    continue;
                }

                if (found.HasValue && found.Value != surface.agentTypeID)
                {
                    throw new InvalidOperationException(
                        $"{LogTag} scene '{scene.path}' bakes NavMesh for more than one agent type ({found.Value} and {surface.agentTypeID})");
                }

                found = surface.agentTypeID;
            }

            return found ?? NavMesh.GetSettingsByIndex(0).agentTypeID;
        }

        public static List<NavLink> CollectLinks(Scene scene, int agentTypeId)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            var links = new List<NavLink>();
            foreach (var link in Components<NavMeshLink>(scene))
            {
                if (!link.isActiveAndEnabled || !link.activated || link.agentTypeID != agentTypeId)
                {
                    continue;
                }

                var (start, end) = WorldEnds(link);
                var kind = link.GetComponentInParent<Ladder>(true) != null ? NavLinkKind.Ladder : NavLinkKind.Walk;
                links.Add(new NavLink(start.ToNumerics(), end.ToNumerics(), link.bidirectional, link.area, kind));
            }

            var agentRadius = NavMesh.GetSettingsByID(agentTypeId).agentRadius;
            foreach (var ladder in Components<Ladder>(scene))
            {
                if (!ladder.isActiveAndEnabled || !ladder.CreatesNavMeshLinks || !ladder.TryGetLinkEnds(agentRadius, out var bottom, out var top))
                {
                    continue;
                }

                links.Add(new NavLink(bottom.ToNumerics(), top.ToNumerics(), true, 0, NavLinkKind.Ladder));
            }

            return links;
        }

        public static string Export(string scenePath, string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException($"{LogTag} output directory is empty", nameof(outputDirectory));
            }

            var mapName = MapNameOf(scenePath);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var file = Capture(scene);
            var path = Path.Combine(outputDirectory, mapName + NavMeshFile.Extension);
            Write(file, path);
            Debug.Log($"{LogTag} wrote {path} from {scenePath} ({file.Vertices.Count} vertices, {file.Triangles.Count} triangles, {file.Links.Count} links)");
            return path;
        }

        // 書く途中で失敗しても、コミット済みのファイルを半端に壊さないよう、全部できてから書く。
        public static void Write(NavMeshFile file, string path)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            using (var buffer = new MemoryStream())
            {
                file.Write(buffer);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(path, buffer.ToArray());
            }
        }

        public static string MapNameOf(string scenePath)
        {
            var name = Path.GetFileNameWithoutExtension(scenePath ?? string.Empty);
            if (!NetLaunchOptions.IsMapName(name))
            {
                throw new ArgumentException($"{LogTag} scene '{scenePath}' is not named like a map (letters, digits, _ and -)", nameof(scenePath));
            }

            return name;
        }

        // NavMeshLink が NavMesh に張るのと同じ、拡大を除いた位置と回転で端を世界の座標に直す。
        private static (Vector3 Start, Vector3 End) WorldEnds(NavMeshLink link)
        {
            var toWorld = Matrix4x4.TRS(link.transform.position, link.transform.rotation, Vector3.one);
            var start = link.startTransform != null ? link.startTransform.position : toWorld.MultiplyPoint3x4(link.startPoint);
            var end = link.endTransform != null ? link.endTransform.position : toWorld.MultiplyPoint3x4(link.endPoint);
            return (start, end);
        }

        private static IEnumerable<T> Components<T>(Scene scene)
            where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<T>(true))
                {
                    yield return component;
                }
            }
        }
    }
}
