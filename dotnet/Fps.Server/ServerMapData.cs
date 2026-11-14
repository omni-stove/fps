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

        public ServerMapData(
            string name, WorldMap world, NavMeshFile navMesh, string directory, NetMapPoints points = null, NetMapStructures structures = null)
        {
            Points = points;
            Structures = structures;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ArgumentNullException.ThrowIfNull(world);
            NavMesh = navMesh ?? throw new ArgumentNullException(nameof(navMesh));
            Directory = directory ?? "";
            ColliderCount = world.Colliders.Count;
            Source = world;
            Collision = new BvhWorldCollision(world);
            Navigation = new MeshNavigation(navMesh);
        }

        public string Name { get; }

        public string Directory { get; }

        public int ColliderCount { get; }

        public NavMeshFile NavMesh { get; }

        public IWorldCollision World => Collision;

        /// <summary>壊れた建物の部品を当たりから外すために、形の組 (<see cref="MapColliderGroups"/>) を切り替えられる型で持つ。</summary>
        public BvhWorldCollision Collision { get; }

        /// <summary>.fpsmap から読んだ形。爆発が届く部品を探すのに使う。</summary>
        public WorldMap Source { get; }

        public INavigation Navigation { get; }

        /// <summary>出現地点と拠点。ルールを動かさないサーバー (テストなど) では要らないので、ファイルが無ければ null。</summary>
        public NetMapPoints Points { get; }

        /// <summary>壊せる建物の部品の並び。書き出していないマップでは null で、部品は壊れない壁として残る。</summary>
        public NetMapStructures Structures { get; }

        public string LogLine => ServerProgram.LogTag + " map " + Name
            + " colliders=" + ColliderCount.ToString(CultureInfo.InvariantCulture)
            + " navTriangles=" + NavMesh.Triangles.Count.ToString(CultureInfo.InvariantCulture)
            + " navLinks=" + NavMesh.Links.Count.ToString(CultureInfo.InvariantCulture)
            + " spawnPoints=" + (Points == null ? "none" : Points.SpawnPoints.Count.ToString(CultureInfo.InvariantCulture))
            + " controlPoints=" + (Points == null ? "none" : Points.ControlPoints.Count.ToString(CultureInfo.InvariantCulture))
            + " structures=" + (Structures == null ? "none" : Structures.Structures.Count.ToString(CultureInfo.InvariantCulture))
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

            NetMapPoints points = null;
            var pointsPath = Path.Combine(directory, map + NetMapPoints.Extension);
            if (File.Exists(pointsPath))
            {
                try
                {
                    points = NetMapPoints.Parse(File.ReadAllText(pointsPath), pointsPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ServerDataJsonException)
                {
                    error = "cannot read " + pointsPath + ": " + e.Message;
                    return false;
                }
            }

            NetMapStructures structures = null;
            var structuresPath = Path.Combine(directory, map + NetMapStructures.Extension);
            if (File.Exists(structuresPath))
            {
                try
                {
                    structures = NetMapStructures.Parse(File.ReadAllText(structuresPath), structuresPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ServerDataJsonException)
                {
                    error = "cannot read " + structuresPath + ": " + e.Message;
                    return false;
                }
            }

            try
            {
                data = new ServerMapData(map, world, navMesh, directory, points, structures);
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
