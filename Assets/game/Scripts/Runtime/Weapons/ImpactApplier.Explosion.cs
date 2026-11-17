using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static partial class ImpactApplier
    {
        public static void Detonate(ShotRequest request, DetonationTrigger cause, Vector3 point, Vector3 normal)
        {
            foreach (var order in ImpactResolver.ResolveDetonation(request.Impact, cause, point.ToNumerics()))
            {
                var center = ImpactResolver.DetonationPoint(order.Point, normal.ToNumerics()).ToUnity();
                Explosion.Detonate(center, order.Explosion, request.Shooter, request.WeaponId);
            }
        }

        private static void ApplyExplosion(ImpactOrder order, ShotRequest request, RaycastHit hit)
        {
            var center = ImpactResolver.DetonationPoint(order.Point, hit.normal.ToNumerics()).ToUnity();
            Explosion.Detonate(center, order.Explosion, request.Shooter, request.WeaponId);
        }
    }
}
