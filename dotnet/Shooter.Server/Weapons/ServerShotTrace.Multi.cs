using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerShotTrace
    {
        static partial void ChooseMaxHits(RoundSpec round, ref int maxHits)
        {
            var penetration = PenetrationRule.For(round);
            if (penetration != null)
            {
                // 抜けられる枚数の先にあるもう 1 つで弾が止まる
                maxHits = penetration.MaxPenetrations + 1;
            }
        }

        // hit を抜けて先へ進むなら passed と damageMultiplier を進めて true。.fpsmap は材質を持たないので、どの面も既定の材質で決める
        private bool TryPassThrough(RoundSpec round, in ShotHit hit, Vector3 direction, ref int passed, ref float damageMultiplier)
        {
            var penetration = PenetrationRule.For(round);
            if (penetration == null)
            {
                return false;
            }

            const string material = ServerCombat.DefaultSurfaceMaterialId;
            var thickness = _hitboxes.Thickness(hit, direction, PenetrationRule.MaxThicknessFor(penetration, material));
            if (!PenetrationRule.TryPassThrough(penetration, passed, material, thickness, out var multiplier))
            {
                return false;
            }

            passed++;
            damageMultiplier *= multiplier;
            return true;
        }
    }
}
