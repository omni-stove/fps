using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // Bot が固定銃座を探し・乗り・座席の武器を向け・降りる口。乗り降りは人の要求と同じ Board・Leave を通し、乗り方の決まりを人と分けない
    internal sealed partial class ServerVehicles
    {
        // 固定銃座の座席 (MountedGunShape が認める形の唯一の座席)
        public const int MountedGunSeat = 0;

        // サーバーは乗り物の部品 (IVehicleMotor) を知らないので、動き方を持つかは運転席の有無 (MountedGunShape の運転席の確かめ) だけで見る
        public bool IsMountedGun(int vehicle)
        {
            return vehicle >= 0 && vehicle < _cars.Count && MountedGunShape.Problem(_cars[vehicle].Map.Spec, false) == null;
        }

        // 乗る位置 (座席の最初の乗る点。無ければ座る位置)。オフラインの MountedGun.BoardPoint と同じ
        public Vector3 BoardPointOf(int vehicle)
        {
            var car = _cars[vehicle];
            var seat = car.Map.Seats[MountedGunSeat];
            return seat.EnterPoints.Count > 0
                ? MapVehicle.ToWorld(seat.EnterPoints[0], car.Position, car.Rotation)
                : car.Map.SeatFeet(MountedGunSeat, car.Position, car.Rotation);
        }

        // 乗る距離は Board と同じ測り方で見る。届かないうちに頼むと、断られたログが tick ごとに出る
        public bool CanBoardFrom(int vehicle, Vector3 feet)
        {
            var car = _cars[vehicle];
            return car.Map.DistanceToEnter(MountedGunSeat, car.Position, car.Rotation, feet) <= car.Map.Spec.EnterRange;
        }

        // 目標 (target) へ座席の武器を向けられる、空いた固定銃座のうち、乗る位置が feet にいちばん近いもの。
        // 向けられない銃座に乗っても撃てず、降りるまで座っているだけになるため、向けられるものだけを選ぶ (オフラインの EnemyMountedGunner と同じ)
        public bool TryFindMountedGun(Vector3 feet, Vector3 target, out int vehicle, out Vector3 boardPoint)
        {
            vehicle = -1;
            boardPoint = default;
            var best = float.PositiveInfinity;
            foreach (var car in _cars)
            {
                if (car.Exploded || car.Occupants[MountedGunSeat] != PlayerIds.None || !IsMountedGun(car.Index)
                    || !CanAim(car, SeatEye(car), target))
                {
                    continue;
                }

                var point = BoardPointOf(car.Index);
                var distance = new Vector2(point.X - feet.X, point.Z - feet.Z).Length();
                if (distance < best)
                {
                    best = distance;
                    vehicle = car.Index;
                    boardPoint = point;
                }
            }

            return vehicle >= 0;
        }

        // Bot が乗っている固定銃座の座席の武器と、狙いを決める向き・目の位置。固定銃座に乗っていなければ false
        public bool TryGetMannedGun(int playerId, out ServerMannedGun gun)
        {
            gun = default;
            if (!_riders.TryGetValue(playerId, out var riding) || !IsMountedGun(riding.Car.Index) || riding.Car.Exploded)
            {
                return false;
            }

            var car = riding.Car;
            var spec = car.Map.Spec;
            var weapon = spec.WeaponsOfSeat(riding.Index)[0];
            gun = new ServerMannedGun(car.Index, weapon, spec.Weapons[weapon], car.Rotation, SeatEye(car));
            return true;
        }

        public bool BotBoard(int playerId, int vehicle)
        {
            if (!BotRoster.IsBotId(playerId))
            {
                throw new ArgumentException("player " + playerId + " is not a bot", nameof(playerId));
            }

            return IsMountedGun(vehicle) && Board(playerId, vehicle, MountedGunSeat);
        }

        public bool BotLeave(int playerId)
        {
            if (!BotRoster.IsBotId(playerId))
            {
                throw new ArgumentException("player " + playerId + " is not a bot", nameof(playerId));
            }

            return Leave(playerId);
        }

        // 銃口の位置はシーンにしか無いので、座った人の目から向きを測る。人の射撃の検査 (AllowsVehicleWeapon) も向きだけを見る
        private Vector3 SeatEye(Car car)
        {
            var crouched = car.Map.Spec.Seats[MountedGunSeat].CrouchesRider;
            var feet = car.Map.SeatFeet(MountedGunSeat, car.Position, car.Rotation);
            var shape = _bodies.Geometry.Shape(crouched);
            return MapVehicle.RiderOrigin(feet, _bodies.Geometry, crouched) + new Vector3(0f, shape.EyeY, 0f);
        }

        private static bool CanAim(Car car, Vector3 eye, Vector3 target)
        {
            var spec = car.Map.Spec;
            var weapon = spec.WeaponsOfSeat(MountedGunSeat)[0];
            var local = Vector3.Transform(target - eye, Quaternion.Inverse(car.Rotation));
            return new VehicleWeaponAim(spec.Weapons[weapon]).CanReach(local);
        }
    }

    internal readonly struct ServerMannedGun
    {
        public ServerMannedGun(int vehicle, int weapon, VehicleWeaponSpec spec, Quaternion rotation, Vector3 eye)
        {
            Vehicle = vehicle;
            Weapon = weapon;
            Spec = spec;
            Rotation = rotation;
            Eye = eye;
        }

        public int Vehicle { get; }

        public int Weapon { get; }

        public VehicleWeaponSpec Spec { get; }

        public Quaternion Rotation { get; }

        public Vector3 Eye { get; }

        public Vector3 ToLocal(Vector3 world)
        {
            return Vector3.Transform(world, Quaternion.Inverse(Rotation));
        }

        public Vector3 ToWorld(Vector3 local)
        {
            return Vector3.Transform(local, Rotation);
        }
    }
}
