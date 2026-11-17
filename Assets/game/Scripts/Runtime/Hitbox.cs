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
            var owner = Owner;
            if (owner == null)
            {
                throw new InvalidOperationException($"Hitbox '{name}' has no Damageable owner in itself or its parents");
            }

            var damage = DamageCalculator.Scale(
                DamageCalculator.Calculate(baseDamage, _zone, distance, falloff, owner.ZoneMultipliers), _damageMultiplier);
            return owner.ApplyResolvedHit(damage, _zone, sourcePosition, direction, DamageKind.Bullet);
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
