using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class StatusEffects
    {
        // 小さな刻みを足し重ねた誤差で、ちょうど区切りの回のダメージや時間切れを 1 回分取りこぼさないための幅。
        private const float _epsilon = 1e-4f;

        private readonly List<ActiveStatusEffect> _active = new List<ActiveStatusEffect>();

        public IReadOnlyList<ActiveStatusEffect> Active => _active;

        public int Count => _active.Count;

        public bool Has(string kindId)
        {
            return Find(kindId) != null;
        }

        public float RemainingSeconds(string kindId)
        {
            return Find(kindId)?.RemainingSeconds ?? 0f;
        }

        public ActiveStatusEffect Afflict(StatusEffectSpec spec, int? sourceId, string weaponId)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var effect = Find(spec.KindId);
            if (effect == null)
            {
                effect = new ActiveStatusEffect(spec.KindId);
                _active.Add(effect);
            }

            // 撃たれ続けても燃え続けるだけにし、時間を積み上げて何十秒も燃えないよう、足さずに長い方を取る。
            effect.RemainingSeconds = Math.Max(effect.RemainingSeconds, spec.DurationSeconds);
            effect.TickSeconds = spec.TickSeconds;
            effect.DamagePerTick = spec.DamagePerTick;
            effect.SourceId = sourceId;
            effect.WeaponId = weaponId ?? string.Empty;
            effect.VisualId = spec.VisualId;
            return effect;
        }

        public void Tick(float deltaSeconds, ICollection<StatusEffectTick> ticks, ICollection<string> ended)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (ticks == null)
            {
                throw new ArgumentNullException(nameof(ticks));
            }

            if (ended == null)
            {
                throw new ArgumentNullException(nameof(ended));
            }

            for (var i = 0; i < _active.Count; i++)
            {
                var effect = _active[i];
                var step = Math.Min(deltaSeconds, effect.RemainingSeconds);
                effect.RemainingSeconds -= step;
                effect.SinceTick += step;
                // 間隔が _epsilon 以下でも SinceTick が 0 まで下がったところで止まるよう、幅を間隔の半分までに抑える。
                var tolerance = Math.Min(_epsilon, effect.TickSeconds * 0.5f);
                while (effect.SinceTick + tolerance >= effect.TickSeconds)
                {
                    effect.SinceTick = Math.Max(0f, effect.SinceTick - effect.TickSeconds);
                    if (effect.DamagePerTick > 0)
                    {
                        ticks.Add(new StatusEffectTick(effect.KindId, effect.DamagePerTick, effect.SourceId, effect.WeaponId));
                    }
                }

                if (effect.RemainingSeconds <= _epsilon)
                {
                    _active.RemoveAt(i);
                    i--;
                    ended.Add(effect.KindId);
                }
            }
        }

        public bool Remove(string kindId)
        {
            var effect = Find(kindId);
            return effect != null && _active.Remove(effect);
        }

        public void Clear(ICollection<string> ended = null)
        {
            if (ended != null)
            {
                foreach (var effect in _active)
                {
                    ended.Add(effect.KindId);
                }
            }

            _active.Clear();
        }

        private ActiveStatusEffect Find(string kindId)
        {
            foreach (var effect in _active)
            {
                if (string.Equals(effect.KindId, kindId, StringComparison.Ordinal))
                {
                    return effect;
                }
            }

            return null;
        }
    }
}
