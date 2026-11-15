using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Protocol;

namespace Fps.NetV2
{
    /// <summary>
    /// サーバーが効かせている範囲の効果 (<see cref="AreaEffectMessage"/>)。出た・変わった・消えたを知らせ、見た目を出す側 (NetV2Client) がそれに合わせて出す。
    /// </summary>
    public sealed class ClientAreaEffects
    {
        private readonly Dictionary<int, AreaEffectState> _areas = new Dictionary<int, AreaEffectState>();

        public event Action<AreaEffectState> Started;

        public event Action<AreaEffectState> Updated;

        public event Action<AreaEffectState> Ended;

        public int Count => _areas.Count;

        public bool TryGet(int areaId, out AreaEffectState state)
        {
            return _areas.TryGetValue(areaId, out state);
        }

        public void Receive(AreaEffectMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var state = new AreaEffectState(message.AreaId, message.KindId ?? "", message.Center, message.Radius, message.RemainingSeconds);
            var known = _areas.ContainsKey(state.AreaId);
            if (state.RemainingSeconds <= 0f)
            {
                // 出たことを知らないまま消えた知らせ (つないだ直後など) は出していないので何もしない
                if (known)
                {
                    _areas.Remove(state.AreaId);
                    Ended?.Invoke(state);
                }

                return;
            }

            _areas[state.AreaId] = state;
            if (known)
            {
                Updated?.Invoke(state);
            }
            else
            {
                Started?.Invoke(state);
            }
        }

        /// <summary>つなぎ直しや試合の切り替えで、出していた範囲をすべて消す。</summary>
        public void Clear()
        {
            if (_areas.Count == 0)
            {
                return;
            }

            var ended = new List<AreaEffectState>(_areas.Values);
            _areas.Clear();
            foreach (var state in ended)
            {
                Ended?.Invoke(state);
            }
        }
    }

    public readonly struct AreaEffectState
    {
        public AreaEffectState(int areaId, string kindId, Vector3 center, float radius, float remainingSeconds)
        {
            AreaId = areaId;
            KindId = kindId;
            Center = center;
            Radius = radius;
            RemainingSeconds = remainingSeconds;
        }

        public int AreaId { get; }

        /// <summary>見た目を選ぶ種類 (AreaEffectSpec.VisualId)。空なら仮の形。</summary>
        public string KindId { get; }

        public Vector3 Center { get; }

        public float Radius { get; }

        public float RemainingSeconds { get; }
    }
}
