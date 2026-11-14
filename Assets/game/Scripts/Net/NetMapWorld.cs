using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net
{
    /// <summary>
    /// 移動を Unity の物理ではなく <c>.fpsmap</c> の世界で当てるのは、.NET のサーバーと同じ形・同じ計算で予測を合わせるため。
    /// 壊れた部品はシーンの <see cref="DestructiblePiece"/> から写す。部品の壊れ方は今の Netcode の同期 (<see cref="NetDestructibleStructure"/>) が
    /// サーバーとクライアントで揃えているので、それを見れば両方の世界も揃う。
    /// </summary>
    public sealed class NetMapWorld
    {
        public const string LogTag = "[Net]";

        // 同じシーンのプレイヤーで 1 つを使い回す。ファイルを読んで木を作るのはマップごとに 1 回で足りるため
        private static NetMapWorld _cached;
        private static Scene _cachedScene;

        private readonly BvhWorldCollision _collision;
        private readonly DestructiblePiece[] _pieces;
        private readonly int[] _pieceGroups;
        private readonly bool[] _pieceEnabled;

        public NetMapWorld(string map, WorldMap world, IReadOnlyList<GameObject> sceneRoots)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (sceneRoots == null)
            {
                throw new ArgumentNullException(nameof(sceneRoots));
            }

            ColliderCount = world.Colliders.Count;
            _collision = new BvhWorldCollision(world);

            var pieces = new List<DestructiblePiece>();
            var groups = new List<int>();
            CollectPieces(sceneRoots, pieces, groups);
            _pieces = pieces.ToArray();
            _pieceGroups = groups.ToArray();
            _pieceEnabled = new bool[_pieces.Length];
            for (var i = 0; i < _pieceEnabled.Length; i++)
            {
                _pieceEnabled[i] = true;
            }

            SyncDestruction();
        }

        public string Map { get; }

        public int ColliderCount { get; }

        public int PieceCount => _pieces.Length;

        public IWorldCollision Collision => _collision;

        public string LogLine => LogTag + " map collision " + Map
            + " colliders=" + ColliderCount.ToString(CultureInfo.InvariantCulture)
            + " pieces=" + PieceCount.ToString(CultureInfo.InvariantCulture);

        /// <summary>シーンの名前の <c>.fpsmap</c> を読む。同じシーンでは前に読んだものを返す。</summary>
        public static bool TryGet(Scene scene, out NetMapWorld world, out string error)
        {
            if (_cached != null && _cachedScene == scene && scene.IsValid())
            {
                world = _cached;
                error = null;
                return true;
            }

            if (!TryLoad(scene, MapCollisionFiles.PathOf(scene.name), out world, out error))
            {
                return false;
            }

            _cached = world;
            _cachedScene = scene;
            Debug.Log(world.LogLine);
            return true;
        }

        public static bool TryLoad(Scene scene, string path, out NetMapWorld world, out string error)
        {
            world = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = LogTag + " cannot load map collision: the scene is not loaded";
                return false;
            }

            if (!File.Exists(path))
            {
                error = LogTag + " map " + scene.name + " has no collision: " + path
                    + " does not exist; export it with make export-server-data (Fps/Net/Export Server Data) and rebuild";
                return false;
            }

            WorldMap map;
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    map = FpsMapFile.Read(stream);
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                error = LogTag + " cannot read " + path + ": " + e.Message;
                return false;
            }

            try
            {
                world = new NetMapWorld(scene.name, map, scene.GetRootGameObjects());
            }
            catch (ArgumentException e)
            {
                error = LogTag + " cannot build map collision " + scene.name + " from " + path + ": " + e.Message;
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>壊れている部品の当たりを外し、戻った部品の当たりを戻す。移動の前に呼ぶ。</summary>
        public void SyncDestruction()
        {
            for (var i = 0; i < _pieces.Length; i++)
            {
                var piece = _pieces[i];
                var enabled = piece != null && !piece.IsBroken;
                if (enabled == _pieceEnabled[i])
                {
                    continue;
                }

                _collision.SetGroupEnabled(_pieceGroups[i], enabled);
                _pieceEnabled[i] = enabled;
            }
        }

        // 書き出し (MapCollisionExporter) と同じく、建物の番号は有効・無効を問わずシーンの並び順で数える
        private static void CollectPieces(IReadOnlyList<GameObject> roots, List<DestructiblePiece> pieces, List<int> groups)
        {
            var structures = new Dictionary<DestructibleStructure, int>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var structure in root.GetComponentsInChildren<DestructibleStructure>(true))
                {
                    structures.Add(structure, structures.Count);
                }
            }

            foreach (var pair in structures)
            {
                foreach (var piece in pair.Key.GetComponentsInChildren<DestructiblePiece>(true))
                {
                    if (piece.GetComponentInParent<DestructibleStructure>(true) != pair.Key)
                    {
                        continue;
                    }

                    pieces.Add(piece);
                    groups.Add(MapColliderGroups.Piece(pair.Value, piece.Index));
                }
            }
        }
    }
}
