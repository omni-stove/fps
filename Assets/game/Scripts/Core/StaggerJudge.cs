using System;
using System.Collections.Generic;

namespace Fps.Core
{
    public sealed class StaggerJudge
    {
        private readonly Queue<(double At, float Amount)> _recent = new Queue<(double At, float Amount)>();
        private double _clock;
        private float _accumulated;
        private float _immunityRemaining;

        public StaggerJudge(StaggerSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public StaggerSpec Spec { get; }

        public StaggerKind Current { get; private set; }

        public float SecondsRemaining { get; private set; }

        public bool IsStaggered => Current != StaggerKind.None;

        public bool IsImmune => _immunityRemaining > 0f;

        public float ImmunityRemaining => _immunityRemaining;

        public float AccumulatedDamage => _accumulated;

        // 部位を持たない銃弾 (部位の無い当たり判定) や落下などは、たまったダメージにだけ数えて怯みは起こさない。
        public static StaggerKind Classify(HitZone? zone, DamageKind kind)
        {
            if (!Enum.IsDefined(typeof(DamageKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a defined DamageKind");
            }

            if (zone.HasValue && !Enum.IsDefined(typeof(HitZone), zone.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(zone), zone, "zone is not a defined HitZone");
            }

            switch (kind)
            {
                case DamageKind.Melee:
                    return StaggerKind.Stumble;
                case DamageKind.Explosion:
                    return StaggerKind.Blast;
            }

            if (!zone.HasValue)
            {
                return StaggerKind.None;
            }

            switch (zone.Value)
            {
                case HitZone.Head:
                    return StaggerKind.Flinch;
                case HitZone.Limb:
                    return StaggerKind.Kneel;
                default:
                    return StaggerKind.None;
            }
        }

        // 怯みにくさは、要るダメージを増やすだけでなく長さも縮める。要るダメージが 1 の近接や爆発では、増やすだけでは差が出ないため。
        public static float Threshold(float baseDamage, float resistance)
        {
            RequireResistance(resistance);
            return baseDamage * (1f + resistance);
        }

        public static float Duration(float baseSeconds, float resistance)
        {
            RequireResistance(resistance);
            return baseSeconds / (1f + resistance);
        }

        public StaggerVerdict Hit(HitZone? zone, DamageKind kind, int amount, float resistance)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            RequireResistance(resistance);
            var reaction = Classify(zone, kind);
            var baseSeconds = Spec.SecondsFor(reaction);
            var reacts = reaction != StaggerKind.None && baseSeconds > 0f;
            var threshold = reacts ? Threshold(Spec.DamageFor(reaction), resistance) : 0f;

            // 怯んでいる間と直後のダメージを数えると、解けた瞬間にまた怯み、怯みが途切れなくなる。
            if (IsImmune)
            {
                return new StaggerVerdict(
                    reacts ? StaggerOutcome.Immune : StaggerOutcome.NoReaction, reaction, 0f, _accumulated, threshold);
            }

            if (amount > 0)
            {
                _recent.Enqueue((_clock, amount));
                _accumulated += amount;
            }

            if (!reacts)
            {
                return new StaggerVerdict(StaggerOutcome.NoReaction, reaction, 0f, _accumulated, threshold);
            }

            if (amount == 0 || _accumulated < threshold)
            {
                return new StaggerVerdict(StaggerOutcome.BelowThreshold, reaction, 0f, _accumulated, threshold);
            }

            var seconds = Duration(baseSeconds, resistance);
            var accumulated = _accumulated;
            Current = reaction;
            SecondsRemaining = seconds;
            _immunityRemaining = seconds + Spec.ImmunitySeconds;
            ClearDamage();
            return new StaggerVerdict(StaggerOutcome.Staggered, reaction, seconds, accumulated, threshold);
        }

        // 怯みが解けた Tick にだけ、解けた種類を返す。
        public StaggerKind Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            _clock += deltaSeconds;
            while (_recent.Count > 0 && _clock - _recent.Peek().At >= Spec.WindowSeconds)
            {
                _accumulated -= _recent.Dequeue().Amount;
            }

            // 引き算を重ねた誤差で、空なのに 0 でなくなるのを防ぐ。
            if (_recent.Count == 0)
            {
                _accumulated = 0f;
            }

            _immunityRemaining = Math.Max(0f, _immunityRemaining - deltaSeconds);
            if (!IsStaggered)
            {
                return StaggerKind.None;
            }

            SecondsRemaining = Math.Max(0f, SecondsRemaining - deltaSeconds);
            if (SecondsRemaining > 0f)
            {
                return StaggerKind.None;
            }

            var ended = Current;
            Current = StaggerKind.None;
            return ended;
        }

        public void Reset()
        {
            Current = StaggerKind.None;
            SecondsRemaining = 0f;
            _immunityRemaining = 0f;
            ClearDamage();
        }

        private void ClearDamage()
        {
            _recent.Clear();
            _accumulated = 0f;
        }

        private static void RequireResistance(float resistance)
        {
            if (!(resistance >= 0f) || float.IsInfinity(resistance))
            {
                throw new ArgumentOutOfRangeException(nameof(resistance), resistance, "resistance must be non-negative and finite");
            }
        }
    }
}
