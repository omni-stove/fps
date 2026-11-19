using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Server
{
    internal sealed partial class ServerHitboxes
    {
        // 太い弾も 1 発で止まる。貫通の複数の当たり (CastMany) は細い線だけが拾う。
        partial void CastSphere(
            Vector3 origin, Vector3 direction, float radius, float range, int maxHits, float tick, int shooterId, Func<int, bool> hittable,
            List<ShotHit> hits, ref bool handled)
        {
            Validate(origin, direction, range, hittable);
            handled = true;
            if (Nearest(origin, Vector3.Normalize(direction), radius, range, tick, shooterId, hittable, true, out var hit))
            {
                hits.Add(hit);
            }
        }

        // 球を動かす判定は始めから重なっている物を拾わない (Unity の SphereCast と同じ) ので、真正面の壁は先に引いた細い線に任せ、
        // それより手前で球が触れる物だけを当たりに替える。
        private bool NearerWorldSphere(Vector3 origin, Vector3 unit, float radius, float limit, ref ShotHit hit)
        {
            if (!(limit > 0f) || !_world.CapsuleCast(origin, origin, radius, unit, limit, RaycastLayers, out var world)
                || !(world.Distance < limit))
            {
                return false;
            }

            hit = ShotHit.World(world);
            return true;
        }
    }
}
