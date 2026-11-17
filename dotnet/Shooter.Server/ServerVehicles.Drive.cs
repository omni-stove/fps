using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed partial class ServerVehicles
    {
        private void Move(int playerId, VehicleMoveMessage move)
        {
            if (move.Vehicle >= _cars.Count)
            {
                Drop("#" + Text(move.Vehicle), playerId, VehicleMoveRejection.NotDriver);
                return;
            }

            var car = _cars[move.Vehicle];
            var driverSeat = car.Map.Spec.DriverSeatIndex;
            if (driverSeat < 0 || car.Occupants[driverSeat] != playerId)
            {
                Drop(car.Name, playerId, VehicleMoveRejection.NotDriver);
                return;
            }

            if (car.Exploded)
            {
                Drop(car.Name, playerId, VehicleMoveRejection.Wrecked);
                return;
            }

            // 直した後に届く、直す前に送られた姿は、直す知らせを受け取れば送り直されるので、もう一度直さずに捨てる
            if (move.CorrectionGeneration != car.Generation)
            {
                return;
            }

            if (move.Sequence <= car.LastSequence)
            {
                return;
            }

            car.LastSequence = move.Sequence;
            // 成分が有限でも、長さ 0 や float を溢れる長さの回転は正規化で NaN になるので、姿に入れる前に直す
            var rotationLength = move.Rotation.Length();
            if (float.IsNaN(rotationLength) || float.IsInfinity(rotationLength) || rotationLength < 1e-6f)
            {
                Correct(car, playerId, VehicleMoveRejection.Blocked);
                return;
            }

            var rejection = CheckMove(car, move.Position);
            if (rejection != VehicleMoveRejection.None)
            {
                Correct(car, playerId, rejection);
                return;
            }

            car.Position = move.Position;
            car.Rotation = Quaternion.Normalize(move.Rotation);
            car.Velocity = car.Drive.ClampVelocity(move.Velocity);
        }

        private VehicleMoveRejection CheckMove(Car car, Vector3 to)
        {
            var from = car.Map.Center(car.Position, car.Rotation);
            var target = car.Map.Center(to, car.Rotation);
            var path = target - from;
            var length = path.Length();
            if (length > 1e-4f && _context.World.Raycast(from, path / length, length, ServerHitboxes.RaycastLayers, out _))
            {
                return VehicleMoveRejection.Blocked;
            }

            return car.Drive.Check(car.Position, to);
        }

        private void Correct(Car car, int driverId, VehicleMoveRejection reason)
        {
            // 許容量を戻すと、外れた姿を送って直させるたびに満タンの距離を進められるので、時間で貯まる分だけにする
            car.Generation++;
            car.Velocity = Vector3.Zero;
            _log.WriteLine(VehicleLog.Corrected(
                car.Name, Text(driverId), reason, car.Generation, car.Position.X, car.Position.Y, car.Position.Z));
            SendCorrection(car, driverId);
        }

        // 運転を代わった人は、前の運転手の姿を知らないので、今の姿と直す番号を渡してからその人の予測を受け入れる。
        private void HandOver(Car car, int driverId)
        {
            car.Generation++;
            car.LastSequence = -1;
            SendCorrection(car, driverId);
        }

        private void SendCorrection(Car car, int driverId)
        {
            if (!_match.TryGetPlayer(driverId, out var driver))
            {
                return;
            }

            _corrected.Vehicle = car.Index;
            _corrected.Generation = car.Generation;
            _corrected.Position = car.Position;
            _corrected.Rotation = car.Rotation;
            _corrected.Velocity = car.Velocity;
            _context.Messenger.Send(driver.Connection, TransportChannel.Reliable, _corrected);
        }

        private void Drop(string vehicle, int playerId, VehicleMoveRejection reason)
        {
            if (CanWarn())
            {
                _log.WriteLine(VehicleLog.MoveDropped(vehicle, Text(playerId), reason));
            }
        }
    }
}
