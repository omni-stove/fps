using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 当たり判定の部位。コライダーと同じ GameObject に置き、命中した部位 (<see cref="HitZone"/>) と
    /// 持ち主の <see cref="Damageable"/> を結ぶ。ダメージ量は <see cref="DamageCalculator"/> が決める。
    /// 持ち主を設定しなければ親をたどって探す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Hitbox : MonoBehaviour
    {
        [SerializeField, Tooltip("この当たり判定の部位")]
        private HitZone _zone = HitZone.Body;

        [SerializeField, Tooltip("ダメージを受ける持ち主。空なら親から探す")]
        private Damageable _owner;

        public HitZone Zone => _zone;

        /// <summary>持ち主。未設定なら自分か親の Damageable。見つからなければ null。</summary>
        public Damageable Owner
        {
            get
            {
                if (_owner == null)
                {
                    _owner = GetComponentInParent<Damageable>();
                }

                return _owner;
            }
        }

        /// <summary>部位と持ち主を設定する。</summary>
        public void Initialize(HitZone zone, Damageable owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (!Enum.IsDefined(typeof(HitZone), zone))
            {
                throw new ArgumentOutOfRangeException(nameof(zone), zone, "unknown hit zone");
            }

            _zone = zone;
            _owner = owner;
        }

        /// <summary>
        /// 命中 1 回分のダメージを <see cref="DamageCalculator.Calculate"/> で求めて持ち主に渡し、与えたダメージを返す。
        /// 持ち主がいない、または既に死んでいれば何もせず 0 を返す。
        /// </summary>
        /// <param name="baseDamage">武器の基本ダメージ。0 以上。</param>
        /// <param name="distance">射手から命中点までの距離。0 以上。</param>
        /// <param name="falloff">距離減衰の設定。</param>
        public int ApplyHit(int baseDamage, float distance, DamageFalloff falloff)
        {
            var damage = DamageCalculator.Calculate(baseDamage, _zone, distance, falloff);

            var owner = Owner;
            if (owner == null)
            {
                Debug.LogWarning($"{Damageable.LogTag} hitbox '{name}' has no Damageable owner; the hit is ignored", this);
                return 0;
            }

            if (owner.IsDead)
            {
                return 0;
            }

            owner.ApplyDamage(damage);
            Debug.Log($"{Damageable.LogTag} hit zone={_zone} dmg={damage} id={owner.Id} hp={owner.Health.Current}", this);
            return damage;
        }

        /// <summary>コライダーから Hitbox を探す。コライダー自身になければ親をたどる。</summary>
        public static bool TryFind(Collider collider, out Hitbox hitbox)
        {
            hitbox = collider == null ? null : collider.GetComponentInParent<Hitbox>();
            return hitbox != null;
        }
    }
}
