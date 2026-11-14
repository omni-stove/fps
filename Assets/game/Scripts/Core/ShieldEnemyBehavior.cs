using System;
using System.Numerics;

namespace Fps.Core
{
    // 盾の上げ下げと振りの時間は読むだけで進めない。MeleeEnemyBehavior と同じく、当たりの瞬間や盾の切り替えを Runtime の 1 か所で扱うため。
    // 種類が Shield でない間は何も上書きしない。段階で種類を変えるボスにも付けたままにできるように。
    public sealed class ShieldEnemyBehavior : IEnemyBehavior
    {
        private readonly ShieldGuard _guard;
        private readonly MeleeSwing _bash;

        public ShieldEnemyBehavior(ShieldGuard guard, MeleeSwing bash)
        {
            _guard = guard ?? throw new ArgumentNullException(nameof(guard));
            _bash = bash ?? throw new ArgumentNullException(nameof(bash));
        }

        public ShieldGuard Guard => _guard;

        public MeleeSwing Bash => _bash;

        public bool Enabled { get; set; } = true;

        public bool WantsBash { get; private set; }

        public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
        {
            if (brain == null)
            {
                throw new ArgumentNullException(nameof(brain));
            }

            WantsBash = false;
            decision = planned;
            if (!Enabled || brain.IsDead || brain.Spec.Archetype != EnemyArchetype.Shield)
            {
                return false;
            }

            var spec = brain.Spec.Shield;
            var target = perception.Target;

            // 下ろしている間に近づき続けると、崩した隙に撃ち込む間が無くなる。
            if (!_guard.IsRaised)
            {
                decision = new EnemyDecision(null, planned.LookAt, false, false);
                return true;
            }

            if (_bash.IsSwinging)
            {
                decision = new EnemyDecision(null, target ?? planned.LookAt, false, false);
                return true;
            }

            var seen = target.HasValue
                && perception.HasLineOfSight
                && perception.Awareness >= EnemyPerception.FullAwareness
                && Vector3.Distance(perception.Self, target.Value) <= brain.Spec.SightRange;

            // 盾は前にしか無いので、隠れてしゃがんだり撃ったりはさせない。見えていない間の行き先 (捜索・巡回) は判断のまま。
            if (!seen)
            {
                decision = new EnemyDecision(planned.MoveTo, planned.LookAt, false, false);
                return true;
            }

            var distance = HorizontalDistance(perception.Self, target.Value);
            WantsBash = distance <= spec.Bash.Reach;
            decision = new EnemyDecision(distance > spec.HoldDistance ? target : null, target, false, false);
            return true;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt((dx * dx) + (dz * dz));
        }
    }
}
