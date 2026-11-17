using UnityEngine;

namespace Shooter.Runtime
{
    public readonly struct ShotOutcome
    {
        public ShotOutcome(bool hit, RaycastHit raycastHit, Hitbox hitbox, Damageable owner, int damage, bool wasAlive, bool killed)
        {
            Hit = hit;
            RaycastHit = raycastHit;
            Hitbox = hitbox;
            Owner = owner;
            Damage = damage;
            WasAlive = wasAlive;
            Killed = killed;
        }

        public static ShotOutcome Miss => default;

        public bool Hit { get; }

        public RaycastHit RaycastHit { get; }

        public Hitbox Hitbox { get; }

        public Damageable Owner { get; }

        // 死んでいる相手に当たったときも計算した値を残すのは、撃った側のログが当たった量を出すため。
        public int Damage { get; }

        public bool WasAlive { get; }

        public bool Killed { get; }

        public static ShotOutcome Stopped(RaycastHit raycastHit)
        {
            return new ShotOutcome(true, raycastHit, null, null, 0, false, false);
        }
    }
}
