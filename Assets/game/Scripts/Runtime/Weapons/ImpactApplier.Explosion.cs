using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public static partial class ImpactApplier
    {
        private static void ApplyExplosion(ImpactOrder order, ShotRequest request, RaycastHit hit)
        {
            var center = ImpactResolver.DetonationPoint(order.Point, hit.normal.ToNumerics()).ToUnity();
            Explosion.Detonate(center, order.Explosion, request.Shooter, request.WeaponId);
        }
    }
}
