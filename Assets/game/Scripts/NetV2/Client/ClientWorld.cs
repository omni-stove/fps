using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
{
    /// <summary>
    /// クライアントが自分の移動を予測する世界。サーバー (<c>ServerMapData</c>) と同じ <c>.fpsmap</c> と <c>.ladders.json</c> を読み、
    /// 壊れた建物の部品はサーバーの <see cref="StructureStateMessage"/> を受けて当たりから外す (<see cref="Apply"/>)。
    /// </summary>
    /// <remarks>
    /// 部品を指す番号は書き出し (MapCollisionExporter) が付けた形の組 (<see cref="MapColliderGroups.Piece"/>) で、サーバーも同じ番号で送る。
    /// 壊れていないと届いた部品は当たりに戻す (次の試合で建物が直るため)。
    /// </remarks>
    public sealed class ClientWorld
    {
        private readonly Dictionary<int, List<int>> _pieces = new Dictionary<int, List<int>>();
        private readonly HashSet<int> _broken = new HashSet<int>();

        public ClientWorld(string map, WorldMap world, NetMapLadders ladders)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            Ladders = ladders ?? throw new ArgumentNullException(nameof(ladders));
            ColliderCount = world.Colliders.Count;
            Collision = new BvhWorldCollision(world);
            foreach (var collider in world.Colliders)
            {
                if (!MapColliderGroups.TryGetPiece(collider.Group, out var structure, out var piece))
                {
                    continue;
                }

                if (!_pieces.TryGetValue(structure, out var pieces))
                {
                    pieces = new List<int>();
                    _pieces.Add(structure, pieces);
                }

                if (!pieces.Contains(piece))
                {
                    pieces.Add(piece);
                }
            }
        }

        public string Map { get; }

        public int ColliderCount { get; }

        public BvhWorldCollision Collision { get; }

        /// <summary>マップのはしご。スナップショットははしごをこの並びの番号で指すので、サーバーと同じファイルから読む。</summary>
        public NetMapLadders Ladders { get; }

        /// <summary>壊せる建物の数 (部品の当たりを持つもの)。</summary>
        public int StructureCount => _pieces.Count;

        /// <summary>今当たりから外している部品の数。</summary>
        public int BrokenPieceCount => _broken.Count;

        public string LogLine => "[Net] map collision " + Map
            + " colliders=" + ColliderCount.ToString(CultureInfo.InvariantCulture)
            + " structures=" + StructureCount.ToString(CultureInfo.InvariantCulture)
            + " ladders=" + Ladders.Ladders.Count.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// directory の <c>&lt;map&gt;.fpsmap</c> と (あれば) <c>&lt;map&gt;.ladders.json</c> を読む。
        /// はしごのファイルが無いマップは、サーバーと同じくはしごが無いものとして動かす。
        /// </summary>
        public static bool TryLoad(string directory, string map, out ClientWorld world, out string error)
        {
            if (directory == null)
            {
                throw new ArgumentNullException(nameof(directory));
            }

            world = null;
            if (!NetLaunchOptions.IsMapName(map))
            {
                error = "map name '" + map + "' is not a scene name";
                return false;
            }

            var path = Path.Combine(directory, map + FpsMapFile.Extension);
            if (!File.Exists(path))
            {
                error = "map " + map + " has no collision: " + path
                    + " does not exist; export it with make export-server-data (Fps/Net/Export Server Data) and rebuild";
                return false;
            }

            WorldMap geometry;
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    geometry = FpsMapFile.Read(stream);
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                error = "cannot read " + path + ": " + e.Message;
                return false;
            }

            var ladders = NetMapLadders.None;
            var laddersPath = Path.Combine(directory, map + NetMapLadders.Extension);
            if (File.Exists(laddersPath))
            {
                try
                {
                    ladders = NetMapLadders.Parse(File.ReadAllText(laddersPath), laddersPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ServerDataJsonException)
                {
                    error = "cannot read " + laddersPath + ": " + e.Message;
                    return false;
                }
            }

            try
            {
                world = new ClientWorld(map, geometry, ladders);
            }
            catch (ArgumentException e)
            {
                error = "cannot build map collision " + map + " from " + path + ": " + e.Message;
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// サーバーの移動と同じ値 (<see cref="MatchClient.Motor"/>) で、この世界を動く自分の体を作る。
        /// </summary>
        public CharacterMotorSimulation CreateSimulation(NetMotorSettings motor, int tickRate, float killPlaneY)
        {
            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "tickRate must be positive");
            }

            var body = new CharacterBody(Collision, motor.Mover, motor.Stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(motor.Motor), 1f / tickRate, killPlaneY, motor.Prone)
            {
                Ladders = Ladders.Ladders,
            };
        }

        /// <summary>
        /// 建物 1 つの壊れた部品をサーバーの知らせのとおりにする。知らない建物なら false (書き出しの古いマップでも動けるよう、何もしない)。
        /// </summary>
        /// <param name="message">建物の番号と壊れた部品の集合。</param>
        /// <param name="changed">当たりを外した・戻した部品の数。</param>
        public bool Apply(StructureStateMessage message, out int changed)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            changed = 0;
            if (!_pieces.TryGetValue(message.StructureId, out var pieces))
            {
                return false;
            }

            foreach (var piece in pieces)
            {
                var word = piece / 64;
                var broken = word < message.WordCount && (message[word] & (1UL << (piece % 64))) != 0;
                var group = MapColliderGroups.Piece(message.StructureId, piece);
                if (broken ? _broken.Add(group) : _broken.Remove(group))
                {
                    Collision.SetGroupEnabled(group, !broken);
                    changed++;
                }
            }

            return true;
        }
    }
}
