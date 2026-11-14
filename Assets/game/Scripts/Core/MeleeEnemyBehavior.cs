using System;
using System.Numerics;

namespace Fps.Core
{
    // 振る・当てるは Runtime が行い、ここは「今振りたいか」と、その間の移動・向き・射撃を決めるだけにする。
    // 振りの時間 (MeleeSwing) は読むだけで進めない。Runtime がプレイヤーと同じ決まりで進め、当たりの瞬間を 1 か所で扱うため。
    public sealed class MeleeEnemyBehavior : IEnemyBehavior
    {
        private readonly MeleeSwing _swing;

        public MeleeEnemyBehavior(MeleeSwing swing)
        {
            _swing = swing ?? throw new ArgumentNullException(nameof(swing));
        }

        public MeleeSwing Swing => _swing;

        public bool Enabled { get; set; } = true;

        // 直前の TryOverride で、目標が届く距離にいて振りたかったか。待ち時間で振れないときも true。
        public bool WantsSwing { get; private set; }

        public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
        {
            if (brain == null)
            {
                throw new ArgumentNullException(nameof(brain));
            }

            WantsSwing = false;
            decision = planned;
            if (!Enabled || brain.IsDead)
            {
                return false;
            }

            var target = perception.Target;

            // 振っている間は、目標が離れても撃ち始めず、その場で振り終える。
            if (_swing.IsSwinging)
            {
                decision = new EnemyDecision(null, target ?? planned.LookAt, false, false);
                return true;
            }

            if (!target.HasValue
                || !perception.HasLineOfSight
                || perception.Awareness < EnemyPerception.FullAwareness
                || HorizontalDistance(perception.Self, target.Value) > _swing.Spec.Reach)
            {
                return false;
            }

            WantsSwing = true;
            decision = new EnemyDecision(null, target, false, false);
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
