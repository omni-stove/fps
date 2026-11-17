using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    internal sealed partial class ServerVehicles
    {
        private readonly List<int> _ejected = new List<int>();

        // Unity の Vehicle.Explode が自分のコライダーを外して爆発させるのと同じく、爆発している乗り物は自分の爆風を遮らない
        private int _exploding = -1;

        public int Damage(int vehicle, int amount, int? attackerId)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            var car = _cars[vehicle];
            if (car.Exploded)
            {
                return 0;
            }

            Damage(car, amount, attackerId, true);
            return amount;
        }

        public int ApplyExplosion(Vector3 center, ExplosionDamage explosion, int? attackerId)
        {
            ArgumentNullException.ThrowIfNull(explosion);
            var damaged = 0;
            foreach (var car in _cars)
            {
                if (car.Exploded)
                {
                    continue;
                }

                var distance = car.Map.DistanceToHull(car.Position, car.Rotation, center);
                if (!explosion.Reaches(distance))
                {
                    continue;
                }

                var target = car.Map.Center(car.Position, car.Rotation);
                var toTarget = target - center;
                var length = toTarget.Length();
                var obstructed = length > 1e-4f && _context.World.Raycast(center, toTarget / length, length, ServerHitboxes.RaycastLayers, out _);
                var amount = explosion.At(distance, obstructed);
                if (amount <= 0)
                {
                    continue;
                }

                damaged++;
                Damage(car, amount, attackerId, true);
            }

            return damaged;
        }

        /// <summary>
        /// 撃った人が見ていた tick (補間の割合を含む) まで巻き戻した車体の箱に線を引き、いちばん近い当たりを返す。
        /// 撃った人の乗っている乗り物には当てない (座席から撃つ弾が自分の車体に当たらないように)。壊れた残骸にも当たる。
        /// </summary>
        public bool Raycast(
            Vector3 origin, Vector3 unit, float maxDistance, float tick, int shooterId, out int vehicle, out float distance, out Vector3 normal,
            out float damageMultiplier)
        {
            vehicle = -1;
            distance = maxDistance;
            normal = default;
            damageMultiplier = 1f;
            var own = _riders.TryGetValue(shooterId, out var riding) ? riding.Car.Index : -1;
            foreach (var car in _cars)
            {
                if (car.Index == own || car.Index == _exploding)
                {
                    continue;
                }

                if (!car.History.TrySample(tick, out var position, out var rotation))
                {
                    position = car.Position;
                    rotation = car.Rotation;
                }

                if (!car.Map.RaycastHull(position, rotation, origin, unit, distance, out var hit, out var hitNormal, out var box) || hit > distance)
                {
                    continue;
                }

                vehicle = car.Index;
                distance = hit;
                normal = hitNormal;
                damageMultiplier = car.Map.PartMultiplier(box);
            }

            return vehicle >= 0;
        }

        public VehicleSeatFiring FiringOf(int playerId, out int vehicle, out int seat, out int weapon)
        {
            weapon = -1;
            if (!TryGetSeat(playerId, out vehicle, out seat))
            {
                return VehicleSeatFiring.OwnWeapon;
            }

            var spec = _cars[vehicle].Map.Spec;
            var firing = spec.FiringOf(seat);
            if (firing == VehicleSeatFiring.VehicleWeapon)
            {
                weapon = spec.WeaponsOfSeat(seat)[0];
            }

            return firing;
        }

        public bool AllowsOwnWeapon(int vehicle, int seat, Vector3 direction)
        {
            var car = _cars[vehicle];
            var local = Vector3.Transform(direction, Quaternion.Inverse(car.Rotation));
            if (local.X == 0f && local.Z == 0f)
            {
                return car.Map.Spec.Seats[seat].OwnWeaponArcDegrees >= VehicleSeatSpec.MaxOwnWeaponArcDegrees;
            }

            var yaw = MathF.Atan2(local.X, local.Z) * (180f / MathF.PI);
            return car.Map.Spec.Seats[seat].AllowsOwnWeaponAt(yaw - car.Map.Seats[seat].SitYawDegrees);
        }

        // 車体は .fpsmap に入っていないので、爆風と火炎の遮りは世界の当たりとは別にここで見る (Unity では車体のコライダーが遮る)
        public bool BlocksLine(Vector3 from, Vector3 to, float tick, int shooterId)
        {
            var offset = to - from;
            var length = offset.Length();
            return length >= 1e-4f && Raycast(from, offset / length, length, tick, shooterId, out _, out _, out _, out _);
        }

        public bool AllowsVehicleWeapon(int vehicle, int weapon, Vector3 direction)
        {
            var car = _cars[vehicle];
            var local = Vector3.Transform(direction, Quaternion.Inverse(car.Rotation));
            return new VehicleWeaponAim(car.Map.Spec.Weapons[weapon]).CanReach(local);
        }

        private void Damage(Car car, int amount, int? attackerId, bool report)
        {
            if (car.Exploded || amount <= 0)
            {
                return;
            }

            if (attackerId.HasValue)
            {
                car.LastAttacker = attackerId;
            }

            var max = car.Map.Spec.MaxHealth;
            car.Health = Math.Max(0, car.Health - amount);
            if (report)
            {
                _log.WriteLine(VehicleLog.Damaged(car.Name, amount, car.Health, max));
            }

            if (car.Health == 0)
            {
                Explode(car);
                return;
            }

            if (car.Durability.Observe(car.Health, max))
            {
                _log.WriteLine(VehicleLog.Stage(car.Name, car.Durability.Stage, car.Health, max));
            }
        }

        // 乗っている人を先に降ろさないのは、爆発に巻き込むため。生き残った人だけを後で降りる位置へ置き直す。
        private void Explode(Car car)
        {
            car.Exploded = true;
            car.Durability.MarkDestroyed();
            car.Velocity = Vector3.Zero;
            var riders = 0;
            foreach (var occupant in car.Occupants)
            {
                if (occupant != PlayerIds.None)
                {
                    riders++;
                }
            }

            var attacker = car.LastAttacker.HasValue && _match.TryGetPlayer(car.LastAttacker.Value, out _) ? car.LastAttacker : null;
            var center = car.Map.Center(car.Position, car.Rotation);
            var explosion = car.Map.Spec.Durability.Explosion;
            var exploding = _exploding;
            _exploding = car.Index;
            (int Damaged, int Killed) counts;
            try
            {
                counts = Combat != null
                    ? Combat.Explode(center, explosion, attacker ?? PlayerIds.None, VehicleSpec.ExplosionWeaponId)
                    : (Damaged: 0, Killed: 0);
            }
            finally
            {
                _exploding = exploding;
            }

            _log.WriteLine(VehicleLog.Exploded(car.Name, center.X, center.Y, center.Z, riders, counts.Damaged, counts.Killed));
            _match.WriteVehicleExploded(car.Index, car.Map.Spec.Id, attacker, riders, counts.Damaged, counts.Killed);

            _ejected.Clear();
            foreach (var occupant in car.Occupants)
            {
                if (occupant != PlayerIds.None)
                {
                    _ejected.Add(occupant);
                }
            }

            foreach (var id in _ejected)
            {
                Release(id, CanRide(id) ? VehicleLeaveReason.Wrecked : VehicleLeaveReason.RiderDied, CanRide(id));
            }

            _log.WriteLine(VehicleLog.Wrecked(car.Name, riders));
        }
    }
}
