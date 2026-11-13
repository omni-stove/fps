using System;
using System.Globalization;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="MotorState"/> に乗せる、1 tick 分の回避の状態。<see cref="DodgeMotion"/> の中身 (<see cref="DodgeSnapshot"/>) を、
    /// 予測の巻き戻しとサーバーからの確定状態 (StateRpc) で同じ値として使う。<c>default</c> は転がっておらず待ち時間も無い。
    /// </summary>
    /// <remarks>
    /// 転がっていない間の向きは持たない (次に転がるときは入力から決め直すので、シミュレーションの答えを変えない)。
    /// 持つと最後に転がった向きの分だけ予測と確定状態の値が食い違いうる。
    /// </remarks>
    public struct NetDodgeState : INetworkSerializable, IEquatable<NetDodgeState>
    {
        private bool _rolling;
        private Vector2 _direction;
        private float _elapsed;
        private float _cooldown;

        public NetDodgeState(DodgeSnapshot snapshot)
        {
            _rolling = snapshot.IsRolling;
            _direction = snapshot.IsRolling ? new Vector2(snapshot.Direction.X, snapshot.Direction.Y) : Vector2.zero;
            _elapsed = snapshot.IsRolling ? snapshot.Elapsed : 0f;
            _cooldown = snapshot.CooldownRemaining;
        }

        /// <summary>転がっておらず待ち時間も無い。</summary>
        public static NetDodgeState Ready => default;

        public bool IsRolling => _rolling;

        /// <summary>転がっている向き (ワールドの水平、X がワールドの X、Y がワールドの Z)。転がっていなければ 0。</summary>
        public Vector2 Direction => _direction;

        /// <summary>今の転がりを始めてからの秒数。転がっていなければ 0。</summary>
        public float Elapsed => _elapsed;

        /// <summary>次を始められるまでの残りの秒数。</summary>
        public float CooldownRemaining => _cooldown;

        /// <summary><see cref="DodgeMotion.Restore"/> に渡す中身。</summary>
        public DodgeSnapshot Snapshot => new DodgeSnapshot(_rolling, new System.Numerics.Vector2(_direction.x, _direction.y), _elapsed, _cooldown);

        /// <summary>数値がすべて有限で <see cref="DodgeMotion.Restore"/> に渡せるか。ネットワークから受け取った値の検査に使う。</summary>
        public bool IsValid => Snapshot.IsValid;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref _rolling);
            serializer.SerializeValue(ref _cooldown);
            if (!_rolling)
            {
                // 転がっていない間は向きと進みを使わないので送らない (確定状態は毎 tick 送るため)
                if (serializer.IsReader)
                {
                    _direction = Vector2.zero;
                    _elapsed = 0f;
                }

                return;
            }

            serializer.SerializeValue(ref _direction);
            serializer.SerializeValue(ref _elapsed);
        }

        public bool Equals(NetDodgeState other)
        {
            return _rolling == other._rolling
                && _direction.Equals(other._direction)
                && _elapsed.Equals(other._elapsed)
                && _cooldown.Equals(other._cooldown);
        }

        public override bool Equals(object obj)
        {
            return obj is NetDodgeState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_rolling, _direction, _elapsed, _cooldown);
        }

        public override string ToString()
        {
            return "rolling=" + (_rolling ? "true" : "false")
                + " elapsed=" + _elapsed.ToString("F2", CultureInfo.InvariantCulture)
                + " cooldown=" + _cooldown.ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}
