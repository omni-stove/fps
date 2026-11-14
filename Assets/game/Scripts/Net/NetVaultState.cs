using System;
using System.Globalization;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="MotorState"/> に乗せる、1 tick 分の乗り越えの状態。たどっている軌跡 (<see cref="VaultPath"/>) と、始めてからの秒数。
    /// 予測の巻き戻しとサーバーからの確定状態 (StateRpc) で同じ値を使う。<c>default</c> は乗り越えていない。
    /// </summary>
    /// <remarks>
    /// 軌跡は <see cref="VaultProbe.PlanPath"/> が作る形 (始点が 0 の 4 頂点、速さ <see cref="VaultProbe.Speed"/>) に限り、始点以外の 3 頂点だけを持つ。
    /// 軌跡を作るときの物理の検査は始めた tick にしか行わないので、巻き戻した tick から再シミュレーションしても頂点を持っていれば同じ道をたどれる。
    /// </remarks>
    public struct NetVaultState : INetworkSerializable, IEquatable<NetVaultState>
    {
        /// <summary>持てる軌跡の頂点の数 (<see cref="VaultProbe.PlanPath"/> と同じ)。</summary>
        public const int PointCount = 4;

        private bool _active;
        private Vector3 _rise;
        private Vector3 _cross;
        private Vector3 _land;
        private float _elapsed;

        /// <param name="path">たどっている軌跡。<see cref="VaultProbe.PlanPath"/> が作る形であること。</param>
        /// <param name="elapsed">始めてからの秒数。0 以上の有限値。</param>
        /// <exception cref="ArgumentNullException">path が null のとき。</exception>
        /// <exception cref="ArgumentException">path の形が違うとき。</exception>
        /// <exception cref="ArgumentOutOfRangeException">elapsed が負か有限でないとき。</exception>
        public NetVaultState(VaultPath path, float elapsed)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var points = path.Points;
            if (points.Count != PointCount || points[0] != System.Numerics.Vector3.Zero || !path.Speed.Equals(VaultProbe.Speed))
            {
                throw new ArgumentException("path must be planned by VaultProbe.PlanPath", nameof(path));
            }

            if (!(elapsed >= 0f) || float.IsInfinity(elapsed))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "elapsed must be finite and not negative");
            }

            _active = true;
            _rise = NetMath.ToUnity(points[1]);
            _cross = NetMath.ToUnity(points[2]);
            _land = NetMath.ToUnity(points[3]);
            _elapsed = elapsed;
        }

        /// <summary>乗り越えていない。</summary>
        public static NetVaultState None => default;

        public static NetVaultState FromCore(CharacterVaultState state)
        {
            if (!state.IsVaulting)
            {
                return None;
            }

            var vault = default(NetVaultState);
            vault._active = true;
            vault._rise = NetMath.ToUnity(state.Rise);
            vault._cross = NetMath.ToUnity(state.Cross);
            vault._land = NetMath.ToUnity(state.Land);
            vault._elapsed = state.Elapsed;
            return vault;
        }

        public CharacterVaultState ToCore()
        {
            return _active
                ? CharacterVaultState.FromPoints(NetMath.ToNumerics(_rise), NetMath.ToNumerics(_cross), NetMath.ToNumerics(_land), _elapsed)
                : CharacterVaultState.None;
        }

        public bool IsVaulting => _active;

        /// <summary>始めてからの秒数。乗り越えていなければ 0。</summary>
        public float Elapsed => _elapsed;

        /// <summary>上がりきった所 (始点からの変位)。</summary>
        public Vector3 Rise => _rise;

        /// <summary>越えきった所 (始点からの変位)。</summary>
        public Vector3 Cross => _cross;

        /// <summary>降りきった所 (始点からの変位)。乗り越えた向きはこの水平の成分。</summary>
        public Vector3 Land => _land;

        /// <summary>数値がすべて有限で、秒数が負でないか。ネットワークから受け取った値の検査に使う。</summary>
        public bool IsValid => NetMath.IsFinite(_rise)
            && NetMath.IsFinite(_cross)
            && NetMath.IsFinite(_land)
            && NetMath.IsFinite(_elapsed)
            && _elapsed >= 0f;

        /// <summary>同じ軌跡で、始めてからの秒数だけ違う状態。</summary>
        public NetVaultState WithElapsed(float elapsed)
        {
            if (!_active)
            {
                throw new InvalidOperationException("not vaulting");
            }

            if (!(elapsed >= 0f) || float.IsInfinity(elapsed))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "elapsed must be finite and not negative");
            }

            var copy = this;
            copy._elapsed = elapsed;
            return copy;
        }

        /// <summary>持っている頂点が path と同じか (作り直さずに使い回してよいか)。</summary>
        public bool HasSamePath(VaultPath path)
        {
            if (!_active || path == null || path.Points.Count != PointCount)
            {
                return false;
            }

            var points = path.Points;
            return NetMath.ToUnity(points[1]).Equals(_rise)
                && NetMath.ToUnity(points[2]).Equals(_cross)
                && NetMath.ToUnity(points[3]).Equals(_land);
        }

        /// <summary>持っている頂点から軌跡を作る。乗り越えていなければ InvalidOperationException。</summary>
        public VaultPath ToPath()
        {
            if (!_active)
            {
                throw new InvalidOperationException("not vaulting");
            }

            var points = new[]
            {
                System.Numerics.Vector3.Zero,
                NetMath.ToNumerics(_rise),
                NetMath.ToNumerics(_cross),
                NetMath.ToNumerics(_land),
            };
            return new VaultPath(points, VaultProbe.Speed);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref _active);
            if (!_active)
            {
                // 乗り越えていない間は残りの値を使わないので送らない (確定状態は毎 tick 送るため)
                if (serializer.IsReader)
                {
                    this = None;
                }

                return;
            }

            serializer.SerializeValue(ref _rise);
            serializer.SerializeValue(ref _cross);
            serializer.SerializeValue(ref _land);
            serializer.SerializeValue(ref _elapsed);
        }

        public bool Equals(NetVaultState other)
        {
            return _active == other._active
                && _rise.Equals(other._rise)
                && _cross.Equals(other._cross)
                && _land.Equals(other._land)
                && _elapsed.Equals(other._elapsed);
        }

        public override bool Equals(object obj)
        {
            return obj is NetVaultState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_active, _rise, _cross, _land, _elapsed);
        }

        public override string ToString()
        {
            return _active
                ? "vaulting=true elapsed=" + _elapsed.ToString("F2", CultureInfo.InvariantCulture)
                : "vaulting=false";
        }
    }
}
