using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Protocol;

namespace Shooter.NetV2
{
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

        public string KindId { get; }

        public Vector3 Center { get; }

        public float Radius { get; }

        public float RemainingSeconds { get; }
    }
}
