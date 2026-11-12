using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 当たり判定 1 つ分の部位。同じ GameObject か親にあるコライダーへの命中を、持ち主の <see cref="Damageable"/> に
    /// <see cref="Zone"/> 付きで渡す。持ち主は <see cref="Initialize"/> で渡すか、未設定なら親階層から探す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Hitbox : MonoBehaviour
    {
        [SerializeField, Tooltip("この当たり判定の部位")]
        private HitZone _zone = HitZone.Body;

        [SerializeField, Tooltip("ダメージを受ける持ち主。空なら親階層から探す")]
        private Damageable _owner;

        public HitZone Zone => _zone;

        /// <summary>持ち主。未設定なら親階層 (自分を含む) から探して覚える。見つからなければ null。</summary>
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

        /// <summary>部位と持ち主をコードで設定する。</summary>
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

        /// <summary>
        /// この部位への命中として持ち主にダメージを渡し、計算したダメージ量を返す。
        /// 持ち主が見つからなければ <see cref="InvalidOperationException"/> を投げる。
        /// </summary>
        public int ApplyHit(int baseDamage, float distance, DamageFalloff falloff)
        {
            var owner = Owner;
            if (owner == null)
            {
                throw new InvalidOperationException($"Hitbox '{name}' has no Damageable owner in itself or its parents");
            }

            return owner.ApplyHit(baseDamage, _zone, distance, falloff);
        }

        /// <summary>コライダーの GameObject か、その親階層にある Hitbox を返す。無ければ null。</summary>
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
