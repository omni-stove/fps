using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Core.Navigation;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;

namespace Fps.Editor
{
    public static class MapCollisionExporter
    {
        public const string MenuPath = "Fps/Net/Export Server Data";
        public const string LogTag = "[MapCollision]";
        public const string ScenesArgument = "-mapScenes";
        public const string OutputFolder = MapCollisionFiles.ServerDataFolder;
        public const string FileExtension = FpsMapFile.Extension;

        private static Func<IReadOnlyList<string>> _mapScenes;

        public static string OutputDirectory => MapCollisionFiles.ServerDataDirectory;

        // マップの一覧はゲーム (取り込んだサンプルなど) が持つので、キットはその読み込み時に渡してもらう。
        // 渡す側はシーンが無ければ作ってから一覧を返してよい。
        public static void UseMapScenes(Func<IReadOnlyList<string>> provider)
        {
            _mapScenes = provider;
        }

        [MenuItem(MenuPath)]
        public static void Export()
        {
            // 一覧を作る側 (シーンの自動生成) は保存の確認なしにシーンを開き直すので、確認と構成の控えはその前に済ませる
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " export canceled");
                return;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scenes = ParseScenesArgument(Environment.GetCommandLineArgs()) ?? _mapScenes?.Invoke();
                if (scenes == null || scenes.Count == 0)
                {
                    throw new InvalidOperationException(
                        LogTag + " no map scenes to export; pass " + ScenesArgument + " <a.unity,b.unity> or register them with UseMapScenes");
                }

                var written = ExportScenes(scenes, OutputDirectory);
                RemoveStaleFiles(OutputDirectory, written);
            }
            finally
            {
                RestoreSetup(setup);
            }
        }

        private static void RestoreSetup(SceneSetup[] setup)
        {
            // Untitled のシーンは戻せないので、保存済みのシーンだけのときに開き直す
            if (setup.Length > 0 && Array.TrueForAll(setup, s => !string.IsNullOrEmpty(s.path)))
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        public static IReadOnlyList<string> ParseScenesArgument(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                return null;
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], ScenesArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException(LogTag + " " + ScenesArgument + " needs comma-separated scene paths", nameof(args));
                }

                var scenes = new List<string>();
                foreach (var part in args[i + 1].Split(','))
                {
                    if (!string.IsNullOrWhiteSpace(part))
                    {
                        scenes.Add(part.Trim());
                    }
                }

                return scenes;
            }

            return null;
        }

        public static string MapName(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath) || !scenePath.EndsWith(".unity", StringComparison.Ordinal))
            {
                throw new ArgumentException(LogTag + " '" + scenePath + "' is not a scene path", nameof(scenePath));
            }

            return Path.GetFileNameWithoutExtension(scenePath);
        }

        public static IReadOnlyList<string> ExportScenes(IReadOnlyList<string> scenePaths, string outputDirectory)
        {
            if (scenePaths == null)
            {
                throw new ArgumentNullException(nameof(scenePaths));
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("output directory must not be empty", nameof(outputDirectory));
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scenePath in scenePaths)
            {
                if (!names.Add(MapName(scenePath)))
                {
                    throw new ArgumentException(LogTag + " two scenes are named " + MapName(scenePath), nameof(scenePaths));
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    throw new FileNotFoundException(LogTag + " scene " + scenePath + " does not exist", scenePath);
                }
            }

            Directory.CreateDirectory(outputDirectory);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var written = new List<string>();
            try
            {
                foreach (var scenePath in scenePaths)
                {
                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    var map = Collect(scene);
                    var path = Path.Combine(outputDirectory, MapName(scenePath) + FileExtension);
                    WriteFile(path, map);
                    written.Add(path);
                    Debug.Log(LogTag + " wrote " + path + " from " + scenePath + " (" + Describe(map) + ")");

                    var navMesh = NavMeshExporter.Capture(scene);
                    var navMeshPath = Path.Combine(outputDirectory, MapName(scenePath) + NavMeshFile.Extension);
                    NavMeshExporter.Write(navMesh, navMeshPath);
                    written.Add(navMeshPath);
                    Debug.Log(NavMeshExporter.LogTag + " wrote " + navMeshPath + " from " + scenePath + " (" + navMesh.Vertices.Count
                        + " vertices, " + navMesh.Triangles.Count + " triangles, " + navMesh.Links.Count + " links)");

                    // サーバーは .fpsmap から部品の形しか分からないので、同じ耐久値と崩れ方で壊せるよう並びも書く
                    var structures = CollectStructures(scene.GetRootGameObjects());
                    var structuresPath = Path.Combine(outputDirectory, MapName(scenePath) + NetMapStructures.Extension);
                    File.WriteAllText(structuresPath, NetMapStructures.Write(structures));
                    written.Add(structuresPath);
                    Debug.Log(LogTag + " wrote " + structuresPath + " (" + structures.Structures.Count + " structures)");
                }
            }
            finally
            {
                RestoreSetup(setup);
            }

            return written;
        }

        public static void WriteFile(string path, WorldMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            // 書きかけのファイルをサーバーやテストが読まないよう、隣に書いてから置き換える
            var temporary = path + ".tmp";
            using (var stream = File.Create(temporary))
            {
                FpsMapFile.Write(stream, map);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temporary, path);
        }

        public static WorldMap ReadFile(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return FpsMapFile.Read(stream);
            }
        }

        public static IReadOnlyList<string> RemoveStaleFiles(string outputDirectory, IReadOnlyCollection<string> keep)
        {
            if (keep == null)
            {
                throw new ArgumentNullException(nameof(keep));
            }

            var removed = new List<string>();
            if (!Directory.Exists(outputDirectory))
            {
                return removed;
            }

            var kept = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in keep)
            {
                kept.Add(Path.GetFullPath(path));
            }

            var files = new List<string>(Directory.GetFiles(outputDirectory, "*" + FileExtension));
            files.AddRange(Directory.GetFiles(outputDirectory, "*" + NavMeshFile.Extension));
            files.AddRange(Directory.GetFiles(outputDirectory, "*" + NetMapStructures.Extension));
            foreach (var path in files)
            {
                if (kept.Contains(Path.GetFullPath(path)))
                {
                    continue;
                }

                File.Delete(path);
                removed.Add(path);
                Debug.Log(LogTag + " removed " + path + " (not one of the exported maps)");
            }

            return removed;
        }

        public static WorldMap Collect(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException(LogTag + " the scene is not loaded", nameof(scene));
            }

            return Collect(scene.GetRootGameObjects());
        }

        public static WorldMap Collect(IReadOnlyList<GameObject> roots, List<Collider> sourcesById = null)
        {
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            sourcesById?.Clear();
            var structures = new Dictionary<DestructibleStructure, int>();
            var bodies = new Dictionary<Rigidbody, int>();
            var colliders = new List<Collider>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                // 番号はサーバーが同じ並びで数え直せるよう、有効・無効を問わずシーンの並び順で振る
                foreach (var structure in root.GetComponentsInChildren<DestructibleStructure>(true))
                {
                    structures.Add(structure, structures.Count);
                }

                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    bodies.Add(body, bodies.Count);
                }

                colliders.AddRange(root.GetComponentsInChildren<Collider>(true));
            }

            var shapes = new List<WorldCollider>();
            var unsupported = new List<string>();
            foreach (var collider in colliders)
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger
                    || collider is CharacterController || collider is WheelCollider)
                {
                    continue;
                }

                var id = shapes.Count;
                var shape = ToWorldCollider(collider, id, GroupOf(collider, structures, bodies));
                if (shape == null)
                {
                    if (!(collider is MeshCollider) && !(collider is BoxCollider) && !(collider is SphereCollider) && !(collider is CapsuleCollider))
                    {
                        unsupported.Add(PathOf(collider.transform) + " (" + collider.GetType().Name + ")");
                    }

                    continue;
                }

                shapes.Add(shape);
                sourcesById?.Add(collider);
            }

            // 書き出さないと、サーバーでだけ通り抜けられる床や壁になる
            if (unsupported.Count > 0)
            {
                throw new InvalidOperationException(
                    LogTag + " these colliders cannot be exported (use Mesh/Box/Sphere/Capsule colliders): " + string.Join(", ", unsupported));
            }

            return new WorldMap(shapes);
        }

        // 番号は Collect と同じく、有効・無効を問わずシーンの並び順で振る
        public static NetMapStructures CollectStructures(IReadOnlyList<GameObject> roots)
        {
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            var layouts = new List<StructureLayout>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var structure in root.GetComponentsInChildren<DestructibleStructure>(true))
                {
                    layouts.Add(structure.Integrity.Layout);
                }
            }

            return new NetMapStructures(layouts);
        }

        private static int GroupOf(Collider collider, Dictionary<DestructibleStructure, int> structures, Dictionary<Rigidbody, int> bodies)
        {
            var piece = collider.GetComponentInParent<DestructiblePiece>(true);
            if (piece != null)
            {
                var structure = piece.GetComponentInParent<DestructibleStructure>(true);
                if (structure != null && structures.TryGetValue(structure, out var structureIndex))
                {
                    return MapColliderGroups.Piece(structureIndex, piece.Index);
                }
            }

            var body = collider.GetComponentInParent<Rigidbody>(true);
            if (body != null && bodies.TryGetValue(body, out var bodyIndex))
            {
                return MapColliderGroups.Body(bodyIndex);
            }

            return MapColliderGroups.Static;
        }

        private static WorldCollider ToWorldCollider(Collider collider, int id, int group)
        {
            var transform = collider.transform;
            var layer = collider.gameObject.layer;
            var scale = Abs(transform.lossyScale);
            switch (collider)
            {
                case BoxCollider box:
                    var half = Vector3.Scale(Abs(box.size), scale) * 0.5f;
                    return WorldCollider.Box(id, layer, group, ToNumerics(transform.TransformPoint(box.center)), ToNumerics(half), ToNumerics(transform.rotation));
                case SphereCollider sphere:
                    var sphereRadius = Mathf.Abs(sphere.radius) * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                    if (!(sphereRadius > 0f))
                    {
                        return null;
                    }

                    return WorldCollider.Sphere(id, layer, group, ToNumerics(transform.TransformPoint(sphere.center)), sphereRadius);
                case CapsuleCollider capsule:
                    return ToCapsule(capsule, id, layer, group, scale);
                case MeshCollider meshCollider:
                    return ToMesh(meshCollider, id, layer, group);
                default:
                    return null;
            }
        }

        private static WorldCollider ToCapsule(CapsuleCollider capsule, int id, int layer, int group, Vector3 scale)
        {
            var axis = Mathf.Clamp(capsule.direction, 0, 2);
            var axisScale = scale[axis];
            var radiusScale = Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]);
            var radius = Mathf.Abs(capsule.radius) * radiusScale;
            if (!(radius > 0f))
            {
                return null;
            }

            var halfSegment = Mathf.Max(0f, (Mathf.Abs(capsule.height) * axisScale * 0.5f) - radius);
            var localAxis = Vector3.zero;
            localAxis[axis] = 1f;
            var transform = capsule.transform;
            var center = transform.TransformPoint(capsule.center);
            var worldAxis = transform.rotation * localAxis;
            return WorldCollider.Capsule(
                id, layer, group, ToNumerics(center - (worldAxis * halfSegment)), ToNumerics(center + (worldAxis * halfSegment)), radius);
        }

        // convex の MeshCollider も元のメッシュの三角形のまま書き出す。凸なメッシュなら PhysX の凸包と同じ面になり、
        // 凸包を作り直すと PhysX の簡略化 (面の数の上限) と食い違うため
        private static WorldCollider ToMesh(MeshCollider meshCollider, int id, int layer, int group)
        {
            var mesh = meshCollider.sharedMesh;
            if (mesh == null)
            {
                return null;
            }

            var matrix = meshCollider.transform.localToWorldMatrix;
            var localVertices = mesh.vertices;
            var vertices = new NumericsVector3[localVertices.Length];
            for (var i = 0; i < localVertices.Length; i++)
            {
                vertices[i] = ToNumerics(matrix.MultiplyPoint3x4(localVertices[i]));
            }

            // 裏返しの拡大縮小では PhysX が面の向きを保つので、三角形の巡りを逆にして表を外へ向けたままにする
            var mirrored = matrix.determinant < 0f;
            var indices = new List<int>();
            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                {
                    continue;
                }

                var triangles = mesh.GetTriangles(subMesh);
                for (var i = 0; i + 2 < triangles.Length; i += 3)
                {
                    indices.Add(triangles[i]);
                    indices.Add(mirrored ? triangles[i + 2] : triangles[i + 1]);
                    indices.Add(mirrored ? triangles[i + 1] : triangles[i + 2]);
                }
            }

            return indices.Count == 0 ? null : WorldCollider.Mesh(id, layer, group, vertices, indices);
        }

        private static string Describe(WorldMap map)
        {
            var statics = 0;
            var pieces = 0;
            var bodies = 0;
            foreach (var collider in map.Colliders)
            {
                if (MapColliderGroups.IsPiece(collider.Group))
                {
                    pieces++;
                }
                else if (MapColliderGroups.IsBody(collider.Group))
                {
                    bodies++;
                }
                else
                {
                    statics++;
                }
            }

            return map.Colliders.Count + " colliders: " + statics + " static, " + pieces + " destructible pieces, " + bodies + " on rigidbodies";
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

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private static NumericsVector3 ToNumerics(Vector3 v)
        {
            return new NumericsVector3(v.x, v.y, v.z);
        }

        private static NumericsQuaternion ToNumerics(Quaternion q)
        {
            return NumericsQuaternion.Normalize(new NumericsQuaternion(q.x, q.y, q.z, q.w));
        }
    }
}
