using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Core;
using Fps.Core.Navigation;

namespace Fps.Server
{
    internal sealed class ServerMapData
    {
        public const string DataDirectoryName = "server-data";
        public const string MapsDirectoryName = "maps";
        public const string CollisionExtension = ".fpsmap";

        public ServerMapData(string name, WorldMap world, NavMeshFile navMesh, string directory)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ArgumentNullException.ThrowIfNull(world);
            NavMesh = navMesh ?? throw new ArgumentNullException(nameof(navMesh));
            Directory = directory ?? "";
            ColliderCount = world.Colliders.Count;
            World = new BvhWorldCollision(world);
            Navigation = new MeshNavigation(navMesh);
        }

        public string Name { get; }

        public string Directory { get; }

        public int ColliderCount { get; }

        public NavMeshFile NavMesh { get; }

        public IWorldCollision World { get; }

        public INavigation Navigation { get; }

        public string LogLine => ServerProgram.LogTag + " map " + Name
            + " colliders=" + ColliderCount.ToString(CultureInfo.InvariantCulture)
            + " navTriangles=" + NavMesh.Triangles.Count.ToString(CultureInfo.InvariantCulture)
            + " navLinks=" + NavMesh.Links.Count.ToString(CultureInfo.InvariantCulture)
            + " from=" + Directory;

        /// <summary>
        /// 作業ディレクトリを先に見るのは、リポジトリのルートから <c>dotnet run</c> したときにコミットした server-data をそのまま使うため。
        /// 実行ファイルの隣も見るのは、イメージでは実行ファイルと server-data を並べて入れるため。
        /// </summary>
        public static IReadOnlyList<string> DefaultSearchRoots()
        {
            var roots = new List<string> { Path.GetFullPath(System.IO.Directory.GetCurrentDirectory()) };
            var beside = Path.GetFullPath(AppContext.BaseDirectory);
            if (!roots.Contains(beside) && !roots.Contains(Path.TrimEndingDirectorySeparator(beside)))
            {
                roots.Add(beside);
            }

            return roots;
        }

        public static bool TryLoad(string map, IReadOnlyList<string> searchRoots, out ServerMapData data, out string error)
        {
            ArgumentNullException.ThrowIfNull(searchRoots);

            data = null;
            if (!NetLaunchOptions.IsMapName(map))
            {
                error = "map name '" + map + "' is not a scene name of letters, digits, '_' or '-'";
                return false;
            }

            var collisionName = map + CollisionExtension;
            var navigationName = map + NavMeshFile.Extension;
            string directory = null;
            var looked = new List<string>();
            foreach (var root in searchRoots)
            {
                var candidate = Path.GetFullPath(Path.Combine(root, DataDirectoryName, MapsDirectoryName));
                looked.Add(candidate);
                if (File.Exists(Path.Combine(candidate, collisionName)))
                {
                    directory = candidate;
                    break;
                }
            }

            if (directory == null)
            {
                error = "map " + map + " has no collision: " + collisionName + " is not in " + string.Join(" or ", looked)
                    + "; export it from Unity with make export-server-data";
                return false;
            }

            var navigationPath = Path.Combine(directory, navigationName);
            if (!File.Exists(navigationPath))
            {
                error = "map " + map + " has no navmesh: " + navigationName + " is not in " + directory
                    + "; export it from Unity with make export-server-data";
                return false;
            }

            var collisionPath = Path.Combine(directory, collisionName);
            WorldMap world;
            NavMeshFile navMesh;
            try
            {
                using (var stream = File.OpenRead(collisionPath))
                {
                    world = FpsMapFile.Read(stream);
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                error = "cannot read " + collisionPath + ": " + e.Message;
                return false;
            }

            try
            {
                using (var stream = File.OpenRead(navigationPath))
                {
                    navMesh = NavMeshFile.Read(stream);
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                error = "cannot read " + navigationPath + ": " + e.Message;
                return false;
            }

            try
            {
                data = new ServerMapData(map, world, navMesh, directory);
            }
            catch (ArgumentException e)
            {
                error = "cannot build map " + map + " from " + directory + ": " + e.Message;
                return false;
            }

            error = null;
            return true;
        }
    }
}
