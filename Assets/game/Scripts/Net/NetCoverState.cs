using System;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="MotorState"/> に乗せる、1 tick 分のカバーの状態。<see cref="CoverState"/> の中身 (<see cref="CoverSnapshot"/>) に、
    /// ブラインドファイアの始点を決めるための壁の上端と端、高い壁の端から覗くために実際に横へ出た量を足したもの。
    /// 予測の巻き戻しとサーバーからの確定状態 (StateRpc) で同じ値を使う。<c>default</c> はカバーの外。
    /// </summary>
    public struct NetCoverState : INetworkSerializable, IEquatable<NetCoverState>
    {
        private byte _phase;
        private byte _height;
        private byte _edges;
        private Vector3 _normal;
        private float _topY;
        private float _peekOffset;
        private float _appliedPeek;
        private float _enterElapsed;
        private float _backElapsed;
        private float _preferredSide;

        /// <param name="snapshot">カバーの状態の中身。</param>
        /// <param name="topY">張り付いている壁の上端の高さ (ワールドの y)。</param>
        /// <param name="edges">張り付いている壁が途切れる側。</param>
        /// <param name="appliedPeek">高い壁の端から覗くために今横に出ている量 (m、壁に向いて右が正)。</param>
        public NetCoverState(CoverSnapshot snapshot, float topY, CoverEdge edges, float appliedPeek)
        {
            _phase = (byte)snapshot.Phase;
            _height = (byte)snapshot.Height;
            _edges = (byte)edges;
            _normal = NetMath.ToUnity(snapshot.Normal);
            _topY = topY;
            _peekOffset = snapshot.PeekOffset;
            _appliedPeek = appliedPeek;
            _enterElapsed = snapshot.EnterElapsed;
            _backElapsed = snapshot.BackElapsed;
            _preferredSide = snapshot.PreferredSide;
        }

        /// <summary>カバーの外。</summary>
        public static NetCoverState Out => default;

        public CoverPhase Phase => (CoverPhase)_phase;

        public CoverHeight Height => (CoverHeight)_height;

        public CoverEdge Edges => (CoverEdge)_edges;

        /// <summary>張り付いている壁の、水平で長さ 1 のこちら向きの法線。</summary>
        public Vector3 Normal => _normal;

        /// <summary>張り付いている壁の上端の高さ (ワールドの y)。</summary>
        public float TopY => _topY;

        /// <summary>高い壁の端から覗くために今横に出ている量 (m、壁に向いて右が正)。</summary>
        public float AppliedPeek => _appliedPeek;

        /// <summary>両側とも端の壁で出る側。負なら左、それ以外は右。</summary>
        public float PreferredSide => _preferredSide;

        public bool IsInCover => Phase != CoverPhase.Out;

        public bool IsPeeking => Phase == CoverPhase.Peeking;

        /// <summary><see cref="CoverState.Restore"/> に渡す中身。</summary>
        public CoverSnapshot Snapshot => new CoverSnapshot(
            Phase, Height, NetMath.ToNumerics(_normal), _peekOffset, _enterElapsed, _backElapsed, _preferredSide);

        /// <summary>列挙が定義済みで、数値がすべて有限か。ネットワークから受け取った値の検査に使う。</summary>
        public bool IsValid => Snapshot.IsValid
            && _edges <= (byte)(CoverEdge.Left | CoverEdge.Right)
            && NetMath.IsFinite(_topY)
            && NetMath.IsFinite(_appliedPeek);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            // 覗く側の好みはカバーの外でも次に入ったときに効くので、外でも送る
            serializer.SerializeValue(ref _phase);
            serializer.SerializeValue(ref _preferredSide);
            if (_phase == (byte)CoverPhase.Out)
            {
                // カバーの外では残りの値を使わないので送らない (確定状態は毎 tick 送るため)
                if (serializer.IsReader)
                {
                    var side = _preferredSide;
                    this = Out;
                    _preferredSide = side;
                }

                return;
            }

            serializer.SerializeValue(ref _height);
            serializer.SerializeValue(ref _edges);
            serializer.SerializeValue(ref _normal);
            serializer.SerializeValue(ref _topY);
            serializer.SerializeValue(ref _peekOffset);
            serializer.SerializeValue(ref _appliedPeek);
            serializer.SerializeValue(ref _enterElapsed);
            serializer.SerializeValue(ref _backElapsed);
        }

        public bool Equals(NetCoverState other)
        {
            return _phase == other._phase
                && _height == other._height
                && _edges == other._edges
                && _normal.Equals(other._normal)
                && _topY.Equals(other._topY)
                && _peekOffset.Equals(other._peekOffset)
                && _appliedPeek.Equals(other._appliedPeek)
                && _enterElapsed.Equals(other._enterElapsed)
                && _backElapsed.Equals(other._backElapsed)
                && _preferredSide.Equals(other._preferredSide);
        }

        public override bool Equals(object obj)
        {
            return obj is NetCoverState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_phase, _height, _edges, _normal, _topY, _appliedPeek, _enterElapsed, _backElapsed);
        }

        public override string ToString()
        {
            return IsInCover
                ? "phase=" + Phase + " height=" + Height + " edges=" + Edges + " peek=" + _appliedPeek.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                : "phase=Out";
        }
    }
}
