using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;

namespace Fps.Server
{
    /// <summary>
    /// 試合に出ている各プレイヤーの体 (位置・向き・姿勢・ダウン)。位置の持ち主は移動のモジュールで、入力を処理するたびにここへ写す。
    /// ルールのモジュールは出現 (<see cref="Teleport"/>) とダウン (<see cref="SetDowned"/>) をここに書き、移動のモジュールがそれを受ける。
    /// 1 つにまとめるのは、どのモジュールも同じ tick の同じ位置で判定するため。
    /// </summary>
    /// <remarks>位置は Unity の NetPlayer の transform.position と同じ点 (カプセルの寸法の原点) で、スナップショットにもこの値を載せる。</remarks>
    internal sealed class ServerBodies
    {
        private readonly SortedDictionary<int, ServerBody> _bodies = new SortedDictionary<int, ServerBody>();

        public ServerBodies(StanceGeometry geometry)
        {
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        /// <summary>置き直した直後に呼ばれる。移動のモジュールはこれで予測の照合や速度を捨てる。</summary>
        public event Action<ServerBody> Teleported;

        /// <summary><see cref="ServerBody.IsDowned"/> が変わった直後に呼ばれる。移動のモジュールはこれでダウン中の動きを止める。</summary>
        public event Action<ServerBody> DownedChanged;

        public StanceGeometry Geometry { get; }

        public int Count => _bodies.Count;

        public IEnumerable<ServerBody> All => _bodies.Values;

        public ServerBody Add(int playerId)
        {
            if (_bodies.ContainsKey(playerId))
            {
                throw new ArgumentException("player " + playerId + " already has a body", nameof(playerId));
            }

            var body = new ServerBody(playerId, Geometry);
            _bodies.Add(playerId, body);
            return body;
        }

        public bool Remove(int playerId)
        {
            return _bodies.Remove(playerId);
        }

        public bool TryGet(int playerId, out ServerBody body)
        {
            return _bodies.TryGetValue(playerId, out body);
        }

        public void Teleport(int playerId, Vector3 position, float yaw, long tick)
        {
            if (!_bodies.TryGetValue(playerId, out var body))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no body for this player");
            }

            body.Teleport(position, yaw, tick);
            Teleported?.Invoke(body);
        }

        public void SetDowned(int playerId, bool downed)
        {
            if (!_bodies.TryGetValue(playerId, out var body))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no body for this player");
            }

            if (body.IsDowned == downed)
            {
                return;
            }

            body.IsDowned = downed;
            DownedChanged?.Invoke(body);
        }
    }

    internal sealed class ServerBody
    {
        private readonly StanceGeometry _geometry;

        public ServerBody(int playerId, StanceGeometry geometry)
        {
            PlayerId = playerId;
            _geometry = geometry;
        }

        public int PlayerId { get; }

        public Vector3 Position { get; private set; }

        public float Yaw { get; private set; }

        public bool IsDowned { get; internal set; }

        public PlayerStance Stance { get; private set; }

        /// <summary>つかまっているはしごの、マップのはしごの並びでの番号。つかまっていなければ <see cref="CharacterLadderState.NoLadder"/>。</summary>
        public int Ladder { get; private set; } = CharacterLadderState.NoLadder;

        public bool IsClimbing => Ladder >= 0;

        public long TeleportTick { get; private set; } = -1;

        public bool HasSpawned => TeleportTick >= 0;

        public StanceShape Shape => _geometry.Shape(Stance);

        public float Radius => _geometry.Radius;

        public Vector3 EyePosition => Position + new Vector3(0f, Shape.EyeY, 0f);

        public Vector3 Center => Position + new Vector3(0f, Shape.CenterY, 0f);

        public void Capsule(out Vector3 bottom, out Vector3 top)
        {
            _geometry.SphereCenters(Shape, out var bottomY, out var topY);
            bottom = Position + new Vector3(0f, bottomY, 0f);
            top = Position + new Vector3(0f, topY, 0f);
        }

        public void SetPose(Vector3 position, float yaw, PlayerStance stance)
        {
            SetPose(position, yaw, stance, CharacterLadderState.NoLadder);
        }

        public void SetPose(Vector3 position, float yaw, PlayerStance stance, int ladder)
        {
            if (!IsFinite(position) || float.IsNaN(yaw) || float.IsInfinity(yaw))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position and yaw must be finite");
            }

            Position = position;
            Yaw = yaw;
            Stance = stance;
            Ladder = ladder < 0 ? CharacterLadderState.NoLadder : ladder;
        }

        internal void Teleport(Vector3 position, float yaw, long tick)
        {
            SetPose(position, yaw, PlayerStance.Standing);
            TeleportTick = tick;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
