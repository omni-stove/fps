using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerHitboxes
    {
        // 当てた形の中から線を続けると同じ形の裏の面に当たることがあるので、その分だけ先へ進めてから次を探す。
        private const float _continueSkin = 1e-3f;

        // 厚さを測る線を向こう側から戻すとき、入った面そのものに当たって厚さ 0 と測らないよう手前で止める幅。
        private const float _thicknessSkin = 0.01f;

        private readonly List<ShotHit> _many = new List<ShotHit>();

        // Unity でも部位のコライダーの重なりで人の厚さを測れないので、プレイヤーの厚さは 0 とし、素材と枚数だけで抜けるかを決める。
        public float Thickness(in ShotHit hit, Vector3 direction, float maxThickness)
        {
            if (!(maxThickness >= 0f) || float.IsInfinity(maxThickness))
            {
                throw new ArgumentOutOfRangeException(nameof(maxThickness), maxThickness, "maxThickness must be a non-negative finite number");
            }

            if (hit.HitTarget)
            {
                return 0f;
            }

            // 車体の厚さは .fpsmap に無く測れないので、抜けない物として扱う
            // 置いた物はオフラインでも弾を受け止めて止める (Deployable.TryStopBullet) ので、抜けない物として扱う
            if (hit.HitVehicle || hit.HitDeployable)
            {
                return float.PositiveInfinity;
            }

            if (!IsFinite(direction) || direction.LengthSquared() < 1e-12f)
            {
                throw new ArgumentException("direction must be finite and not zero", nameof(direction));
            }

            // 向こう側から戻る線が出口の面に当たれば、その手前までが厚さ。始点が形の中なら当たらず、厚すぎることになる
            var unit = Vector3.Normalize(direction);
            var probe = maxThickness + _thicknessSkin;
            if (!_world.RaycastCollider(hit.ColliderId, hit.Point + (unit * probe), -unit, probe - (_thicknessSkin * 0.5f), out var exit))
            {
                return float.PositiveInfinity;
            }

            return Math.Max(0f, probe - exit.Distance);
        }

        partial void CastMany(
            Vector3 origin, Vector3 direction, float range, int maxHits, float tick, int shooterId, Func<int, bool> hittable,
            List<ShotHit> hits, ref bool handled)
        {
            ArgumentNullException.ThrowIfNull(hittable);
            if (!IsFinite(origin))
            {
                throw new ArgumentException("origin must be finite", nameof(origin));
            }

            if (!IsFinite(direction) || direction.LengthSquared() < 1e-12f)
            {
                throw new ArgumentException("direction must be finite and not zero", nameof(direction));
            }

            if (!(range > 0f) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must be positive and finite");
            }

            handled = true;
            var unit = Vector3.Normalize(direction);
            _many.Clear();
            AddWorldHits(origin, unit, range, maxHits);

            // 置いた物は弾を止めるので、その先の物は要らない。いちばん近い 1 つだけを足す
            var deployableHit = default(ShotHit);
            if (NearerDeployable(origin, unit, range, shooterId, ref deployableHit))
            {
                _many.Add(deployableHit);
            }

            var rewind = AddTargetHits(origin, unit, range, tick, shooterId, hittable);
            _many.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            var count = Math.Min(maxHits, _many.Count);
            for (var i = 0; i < count; i++)
            {
                hits.Add(_many[i].WithRewind(rewind));
            }

            _many.Clear();
        }

        // 線の上の世界の形を近い順に maxHits 個まで。後ろの人と合わせて近い方から取るので、これより多くは要らない
        private void AddWorldHits(Vector3 origin, Vector3 unit, float range, int maxHits)
        {
            var travelled = 0f;
            var found = 0;
            var lastCollider = -1;
            while (found < maxHits && travelled < range)
            {
                var start = origin + (unit * travelled);
                if (!_world.Raycast(start, unit, range - travelled, RaycastLayers, out var world))
                {
                    return;
                }

                var distance = travelled + world.Distance;
                if (world.ColliderId != lastCollider)
                {
                    _many.Add(ShotHit.World(new WorldHit(world.Point, world.Normal, distance, world.ColliderId, world.Layer)));
                    lastCollider = world.ColliderId;
                    found++;
                }

                travelled = distance + _continueSkin;
            }
        }

        // 当たった人を全員足し、巻き戻した tick 数を返す
        private float AddTargetHits(Vector3 origin, Vector3 unit, float range, float tick, int shooterId, Func<int, bool> hittable)
        {
            if (!HasHistory)
            {
                return 0f;
            }

            var clamped = _lag.ClampTick(tick, LastRecordedTick);
            var vehicleHit = default(ShotHit);
            if (NearerVehicle(origin, unit, range, clamped, shooterId, ref vehicleHit))
            {
                _many.Add(vehicleHit);
            }

            foreach (var (id, body) in RewindBodies(clamped, shooterId, hittable))
            {
                if (!RayCapsule(origin, unit, body.Bottom, body.Top, body.Radius, range, out var distance, out var normal))
                {
                    continue;
                }

                var point = origin + (unit * distance);
                _many.Add(ShotHit.Target(point, normal, distance, id, body.Position, body.Crouched, body.Zone(point)));
            }

            return LastRecordedTick - clamped;
        }
    }
}
