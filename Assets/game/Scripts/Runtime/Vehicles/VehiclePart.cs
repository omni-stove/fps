using System;
using UnityEngine;

namespace Shooter.Runtime
{
    // 部位の倍率を Hitbox の HitZone に寄せないのは、エンジンや燃料のような部位が乗り物ごとに違い、決まった種類に収まらないため。
    [DisallowMultipleComponent]
    public sealed class VehiclePart : MonoBehaviour
    {
        [SerializeField, Tooltip("乗り物の部位の Id (VehicleDefinition の部位)。この GameObject と子のコライダーへの命中に、その部位の倍率を掛ける")]
        private string _partId = string.Empty;

        public string PartId
        {
            get => _partId;
            set => _partId = value ?? string.Empty;
        }

        public Hitbox EnsureHitbox(Damageable owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var hitbox = GetComponent<Hitbox>();
            if (hitbox == null)
            {
                hitbox = gameObject.AddComponent<Hitbox>();
            }

            hitbox.Initialize(hitbox.Zone, owner);
            return hitbox;
        }
    }
}
