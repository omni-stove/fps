using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public sealed class ProjectileDelivery : IShotDelivery
    {
        public static readonly ProjectileDelivery Instance = new ProjectileDelivery();

        // 当たるのは弾が飛んだ後なので、ここではいつも外れを返し、当たりは ShotRequest.Landed で後から知らせる。
        public ShotOutcome Deliver(ShotRequest request, PlannedRound round)
        {
            if (request.Round.Projectile == null)
            {
                throw new ArgumentException("the round of a projectile delivery needs RoundSpec.Projectile", nameof(request));
            }

            // 弾を作れなかった (向きが 0 など) ときに撃ったことにしないよう、知らせるのは作った後にする。
            ProjectileBody.Launch(request, round);
            CombatEvents.RaiseShotFired(request.Shooter, round.Origin.ToUnity(), round.Direction.ToUnity(), request.WeaponId, tracer: false);
            return ShotOutcome.Miss;
        }
    }
}
