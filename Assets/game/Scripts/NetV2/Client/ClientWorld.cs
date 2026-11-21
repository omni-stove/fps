using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 部品を指す番号は書き出し (MapCollisionExporter) が付けた形の組 (MapColliderGroups.Piece) で、サーバーも同じ番号で送る。
    // 次の試合で建物が直るので、壊れていないと届いた部品は当たりに戻す。
    public sealed class ClientWorld
    {
        private readonly Dictionary<int, List<int>> _pieces = new Dictionary<int, List<int>>();
        private readonly HashSet<int> _broken = new HashSet<int>();
        private readonly Dictionary<int, GrenadeTrack> _grenades = new Dictionary<int, GrenadeTrack>();
        private readonly List<int> _goneGrenades = new List<int>();

        public ClientWorld(string map, WorldMap world, NetMapLadders ladders, NetMapWater water = null)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            Ladders = ladders ?? throw new ArgumentNullException(nameof(ladders));
            Water = water ?? NetMapWater.None;
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

        // スナップショットははしごをこの並びの番号で指すので、サーバーと同じファイルから読む。
        public NetMapLadders Ladders { get; }

        // 泳ぐかは水面で毎 tick 決まるので、サーバーと同じファイルから読まないと予測が泳ぎの出入りで毎回巻き戻る。
        public NetMapWater Water { get; }

        public int StructureCount => _pieces.Count;

        public int BrokenPieceCount => _broken.Count;

        public IEnumerable<int> GrenadeIds => _grenades.Keys;

        public string LogLine => "[Net] map collision " + Map
            + " colliders=" + ColliderCount.ToString(CultureInfo.InvariantCulture)
            + " structures=" + StructureCount.ToString(CultureInfo.InvariantCulture)
            + " ladders=" + Ladders.Ladders.Count.ToString(CultureInfo.InvariantCulture)
            + " water=" + Water.Water.Count.ToString(CultureInfo.InvariantCulture);

        // サーバーと同じく、はしごと水のファイルが無いマップははしごと水が無いものとして動かす。
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

            var path = Path.Combine(directory, map + ShooterMapFile.Extension);
            if (!File.Exists(path))
            {
                error = "map " + map + " has no collision: " + path
                    + " does not exist; export it with make export-server-data (Shooter/Net/Export Server Data) and rebuild";
                return false;
            }

            WorldMap geometry;
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    geometry = ShooterMapFile.Read(stream);
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

            var water = NetMapWater.None;
            var waterPath = Path.Combine(directory, map + NetMapWater.Extension);
            if (File.Exists(waterPath))
            {
                try
                {
                    water = NetMapWater.Parse(File.ReadAllText(waterPath), waterPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ServerDataJsonException)
                {
                    error = "cannot read " + waterPath + ": " + e.Message;
                    return false;
                }
            }

            try
            {
                world = new ClientWorld(map, geometry, ladders, water);
            }
            catch (ArgumentException e)
            {
                error = "cannot build map collision " + map + " from " + path + ": " + e.Message;
                return false;
            }

            error = null;
            return true;
        }

        public CharacterMotorSimulation CreateSimulation(NetMotorSettings motor, int tickRate, float killPlaneY, KnockbackSpec knockback = null)
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
                Water = Water,
                Knockback = knockback ?? KnockbackSpec.Default,
                Swim = motor.Swim,
                WallRun = motor.WallRun,
                Parachute = motor.Parachute,
                Slide = motor.Slide,
            };
        }

        // 書き出しの古いマップでも動けるよう、知らない建物の知らせは何もせず false を返す。
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

        public void ApplyGrenades(SnapshotMessage snapshot, ICollection<int> removed)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (removed == null)
            {
                throw new ArgumentNullException(nameof(removed));
            }

            _goneGrenades.Clear();
            foreach (var id in _grenades.Keys)
            {
                if (snapshot.Grenades.IndexOf(id) < 0)
                {
                    _goneGrenades.Add(id);
                }
            }

            foreach (var id in _goneGrenades)
            {
                _grenades.Remove(id);
                removed.Add(id);
            }

            for (var i = 0; i < snapshot.Grenades.Count; i++)
            {
                ref var row = ref snapshot.Grenades[i];
                if (!_grenades.TryGetValue(row.GrenadeId, out var track))
                {
                    track = new GrenadeTrack();
                    _grenades.Add(row.GrenadeId, track);
                }

                track.Record(snapshot.Tick, row.Position, row.Rotation);
                track.TypeId = row.TypeId ?? string.Empty;
                var carrier = row.HasStuckTo ? snapshot.Players.IndexOf(row.StuckToId) : -1;

                // 隠れた人の位置は空なので、そこからのずれを取らずにグレネードの位置をそのまま使う
                if (carrier < 0 || snapshot.Players[carrier].Concealed)
                {
                    track.HasCarrier = false;
                    continue;
                }

                ref var player = ref snapshot.Players[carrier];
                var unturn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -player.Yaw);
                track.HasCarrier = true;
                track.CarrierId = row.StuckToId;
                track.CarrierOffset = Vector3.Transform(row.Position - player.Position, unturn);
                track.CarrierRotation = Quaternion.Normalize(unturn * row.Rotation);
            }
        }

        public bool TryShowGrenade(int grenadeId, float renderTick, GrenadeCarrier carrier, out Vector3 position, out Quaternion rotation)
        {
            if (!_grenades.TryGetValue(grenadeId, out var track))
            {
                position = default;
                rotation = Quaternion.Identity;
                return false;
            }

            // 人とグレネードの補間の遅れが違っても離れて見えないよう、くっついた人は見せている位置に付ける
            if (track.HasCarrier && carrier != null && carrier(track.CarrierId, out var carrierPosition, out var carrierYaw))
            {
                var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, carrierYaw);
                position = carrierPosition + Vector3.Transform(track.CarrierOffset, turn);
                rotation = Quaternion.Normalize(turn * track.CarrierRotation);
                return true;
            }

            track.Sample(renderTick, out position, out rotation);
            return true;
        }

        public string GrenadeTypeId(int grenadeId)
        {
            return _grenades.TryGetValue(grenadeId, out var track) ? track.TypeId : string.Empty;
        }

        public void ClearGrenades()
        {
            _grenades.Clear();
        }

        private sealed class GrenadeTrack
        {
            private const int _capacity = 8;

            private readonly int[] _ticks = new int[_capacity];
            private readonly Vector3[] _positions = new Vector3[_capacity];
            private readonly Quaternion[] _rotations = new Quaternion[_capacity];
            private int _start;
            private int _count;

            public string TypeId { get; set; } = string.Empty;

            public bool HasCarrier { get; set; }

            public int CarrierId { get; set; }

            public Vector3 CarrierOffset { get; set; }

            public Quaternion CarrierRotation { get; set; }

            public void Record(int tick, Vector3 position, Quaternion rotation)
            {
                if (_count > 0 && tick <= _ticks[At(_count - 1)])
                {
                    return;
                }

                if (_count == _capacity)
                {
                    _start = (_start + 1) % _capacity;
                    _count--;
                }

                var slot = At(_count);
                _ticks[slot] = tick;
                _positions[slot] = position;
                _rotations[slot] = rotation;
                _count++;
            }

            public void Sample(float tick, out Vector3 position, out Quaternion rotation)
            {
                var newer = 0;
                while (newer < _count && _ticks[At(newer)] < tick)
                {
                    newer++;
                }

                if (newer == 0 || newer == _count)
                {
                    var only = At(newer == 0 ? 0 : _count - 1);
                    position = _positions[only];
                    rotation = _rotations[only];
                    return;
                }

                var a = At(newer - 1);
                var b = At(newer);
                var t = (tick - _ticks[a]) / (_ticks[b] - _ticks[a]);
                position = Vector3.Lerp(_positions[a], _positions[b], t);
                rotation = Quaternion.Slerp(_rotations[a], _rotations[b], t);
            }

            private int At(int index)
            {
                return (_start + index) % _capacity;
            }
        }
    }

    public delegate bool GrenadeCarrier(int playerId, out Vector3 position, out float yaw);
}
