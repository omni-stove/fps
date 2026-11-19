using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public readonly struct AircraftPartDamageResult
    {
        // default では null になるので、読むときに空文字へ寄せる。
        private readonly string _partId;

        public AircraftPartDamageResult(string partId, AircraftDamageEffect effect, int dealt, int remaining, int durability, bool broke)
        {
            _partId = partId ?? string.Empty;
            Effect = effect;
            Dealt = dealt;
            Remaining = remaining;
            Durability = durability;
            Broke = broke;
        }

        public string PartId => _partId ?? string.Empty;

        public AircraftDamageEffect Effect { get; }

        public int Dealt { get; }

        public int Remaining { get; }

        public int Durability { get; }

        // この命中で残りが 0 になったときだけ true。壊れた知らせを一度きりにするため。
        public bool Broke { get; }
    }

    // 機体全体の体力 (Damageable) と分けて部位ごとの残りを持つのは、翼が折れても機体が飛び続け、落ちるまでの間に飛び方だけが崩れるようにするため。
    public sealed class AircraftDamage
    {
        private readonly Dictionary<string, int> _remaining = new Dictionary<string, int>(StringComparer.Ordinal);

        public AircraftDamage(AircraftDamageSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Repair();
        }

        public AircraftDamageSpec Spec { get; }

        public AircraftCondition Condition { get; private set; } = AircraftCondition.Intact;

        public bool Contains(string partId)
        {
            return partId != null && _remaining.ContainsKey(partId);
        }

        public int Remaining(string partId)
        {
            return _remaining[RequirePart(partId).Id];
        }

        public bool IsBroken(string partId)
        {
            return Remaining(partId) == 0;
        }

        // 飛び方に効かない部位 (燃料タンクなど) にも VehiclePart が付くので、知らない Id は例外にせず false を返す。
        public bool TryApply(string partId, int damage, out AircraftPartDamageResult result)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            var part = partId != null ? Spec.FindPart(partId) : null;
            if (part == null)
            {
                result = default;
                return false;
            }

            var before = _remaining[part.Id];
            var after = Math.Max(0, before - damage);
            if (after != before)
            {
                _remaining[part.Id] = after;
                Condition = Compute();
            }

            result = new AircraftPartDamageResult(part.Id, part.Effect, before - after, after, part.Durability, before > 0 && after == 0);
            return true;
        }

        // 運転する人へ壊れ具合を届ける同期や、修理で途中まで戻すときに、残りをそのまま書く。
        public void SetRemaining(string partId, int remaining)
        {
            var part = RequirePart(partId);
            if (remaining < 0 || remaining > part.Durability)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remaining), remaining, $"remaining must be between 0 and {part.Durability} for part '{part.Id}'");
            }

            _remaining[part.Id] = remaining;
            Condition = Compute();
        }

        public void Repair()
        {
            foreach (var part in Spec.Parts)
            {
                _remaining[part.Id] = part.Durability;
            }

            Condition = AircraftCondition.Intact;
        }

        // 同じ効き先の部位が幾つもある (エンジン 2 基など) ときは、残りの割合を平均して、1 つ壊れても半分は効くようにする。
        private AircraftCondition Compute()
        {
            return new AircraftCondition(
                Multiplier(AircraftDamageEffect.LeftLift),
                Multiplier(AircraftDamageEffect.RightLift),
                Multiplier(AircraftDamageEffect.Control),
                Multiplier(AircraftDamageEffect.Thrust));
        }

        private float Multiplier(AircraftDamageEffect effect)
        {
            var sum = 0f;
            var count = 0;
            foreach (var part in Spec.Parts)
            {
                if (part.Effect != effect)
                {
                    continue;
                }

                sum += (float)_remaining[part.Id] / part.Durability;
                count++;
            }

            if (count == 0)
            {
                return 1f;
            }

            var fraction = sum / count;
            if (fraction >= 1f)
            {
                return 1f;
            }

            var min = Spec.MinOf(effect);
            return Math.Min(1f, min + ((1f - min) * fraction));
        }

        private AircraftDamagePartSpec RequirePart(string partId)
        {
            var part = partId != null ? Spec.FindPart(partId) : null;
            if (part == null)
            {
                throw new ArgumentException($"part '{partId}' is not in the aircraft damage spec", nameof(partId));
            }

            return part;
        }
    }
}
