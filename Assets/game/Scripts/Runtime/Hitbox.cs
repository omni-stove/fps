using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class Hitbox : MonoBehaviour
    {
        [SerializeField, Tooltip("この当たり判定の部位")]
        private HitZone _zone = HitZone.Body;

        [SerializeField, Tooltip("ダメージを受ける持ち主。空なら親階層から探す")]
        private Damageable _owner;

        [SerializeField, Min(0f), Tooltip("部位の倍率の後に掛ける倍率 (乗り物のエンジンや燃料など)。1 なら変えない")]
        private float _damageMultiplier = 1f;

        // 持ち主の Damaged には当たった当たり判定が載らない (持ち主は 1 つで当たり判定は幾つもある) ので、乗り物の部位のように当たり判定ごとに受けたい側はここで受ける。
        public event Action<Hitbox, int> Hit;

        public HitZone Zone => _zone;

        // 部位 (HitZone) の倍率は持ち主ごとに決まるので、同じ持ち主の中で当たり判定ごとに変える倍率 (乗り物の部位) は別に持つ。
        public float DamageMultiplier
        {
            get => _damageMultiplier;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "DamageMultiplier must not be negative and must be finite");
                }

                _damageMultiplier = value;
            }
        }

        public Damageable Owner
        {
            get
            {
                if (_owner == null)
                {
                    _owner = GetComponentInParent<Damageable>(true);
                }

                return _owner;
            }
        }

        public void Initialize(HitZone zone, Damageable owner)
        {
            if (!Enum.IsDefined(typeof(HitZone), zone))
            {
                throw new ArgumentOutOfRangeException(nameof(zone), zone, "unknown hit zone");
            }

            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            _zone = zone;
            _owner = owner;
        }

        public int ApplyHit(int baseDamage, float distance, DamageFalloff falloff, Vector3? sourcePosition = null, Vector3? direction = null)
        {
            var owner = RequireOwner();
            var damage = DamageCalculator.Scale(
                DamageCalculator.Calculate(baseDamage, _zone, distance, falloff, owner.ZoneMultipliers), _damageMultiplier);
            return ApplyResolvedHit(damage, sourcePosition, direction, DamageKind.Bullet);
        }

        // 部位の倍率と DamageMultiplier を呼ぶ側が済ませた量を受ける。持ち主へ直に渡さずここを通すのは、Hit を流すため。
        public int ApplyResolvedHit(int damage, Vector3? sourcePosition, Vector3? direction, DamageKind kind)
        {
            var owner = RequireOwner();
            var wasAlive = !owner.IsDead;
            var dealt = owner.ApplyResolvedHit(damage, _zone, sourcePosition, direction, kind);
            if (wasAlive && dealt > 0)
            {
                Hit?.Invoke(this, dealt);
            }

            return dealt;
        }

        private Damageable RequireOwner()
        {
            var owner = Owner;
            if (owner == null)
            {
                throw new InvalidOperationException($"Hitbox '{name}' has no Damageable owner in itself or its parents");
            }

            return owner;
        }

        public static Hitbox FindFor(Collider collider)
        {
            if (collider == null)
            {
                throw new ArgumentNullException(nameof(collider));
            }

            return collider.GetComponentInParent<Hitbox>(true);
        }
    }
}
