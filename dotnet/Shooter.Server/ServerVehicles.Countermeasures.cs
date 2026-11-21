using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 撒く数・間隔・効き目は CountermeasurePod に任せ (Unity の VehicleCountermeasures と同じ)、ここは座席の要求を確かめて撒くことと、
    // ロックオン (ServerLockOn) と誘導弾 (ServerProjectiles) が乗り物を狙い、そのフレアを問い合わせる口だけを持つ。
    internal sealed partial class ServerVehicles
    {
        private static readonly Vector3 _flareGravity = new Vector3(0f, ServerGrenadeFlight.Gravity, 0f);

        // 撒くのは操縦する人だけにする。同乗者が押しても操縦する人の残りの数が減らないようにするため (Unity の VehicleCountermeasures と同じ)。
        public bool DeployCountermeasure(int playerId)
        {
            if (!_riders.TryGetValue(playerId, out var riding))
            {
                _log.WriteLine(ServerCountermeasureLog.Refused("-", playerId, VehicleLog.ReasonNotRiding));
                return false;
            }

            var car = riding.Car;
            var refusal = riding.Index != car.Map.Spec.DriverSeatIndex ? ServerCountermeasureLog.ReasonNotDriver
                : car.Exploded ? VehicleLog.ReasonWrecked
                : car.Flares == null ? ServerCountermeasureLog.ReasonNoFlares
                : null;
            if (refusal != null)
            {
                _log.WriteLine(ServerCountermeasureLog.Refused(car.Name, playerId, refusal));
                return false;
            }

            var pod = car.Flares;
            var flares = car.Map.Countermeasures;
            var verdict = pod.TryDeploy(
                flares.EjectPoint(car.Position, car.Rotation), flares.EjectVelocity(car.Rotation, car.Velocity), out var flareId);
            if (verdict != CountermeasureVerdict.Deployed)
            {
                _log.WriteLine(ServerCountermeasureLog.Refused(car.Name, playerId, verdict, pod.Remaining, pod.Spec.Count, pod.CooldownLeft));
                return false;
            }

            car.SyncFlares();
            _log.WriteLine(ServerCountermeasureLog.Deployed(car.Name, playerId, flareId, pod.Remaining, pod.Spec.Count));
            return true;
        }

        public int CountermeasuresLeft(int vehicle)
        {
            return IsKnown(vehicle) && _cars[vehicle].Flares != null ? _cars[vehicle].Flares.Remaining : 0;
        }

        public IReadOnlyList<CountermeasureFlare> FlaresOf(int vehicle)
        {
            return IsKnown(vehicle) && _cars[vehicle].Flares != null ? _cars[vehicle].Flares.Flares : System.Array.Empty<CountermeasureFlare>();
        }

        // 乗っている人を狙った弾も、その人の乗り物のフレアに誘われる (Unity の VehicleCountermeasures.For と同じ)。
        // 壊れた乗り物のフレアも、空にいる間は燃え尽きるまで弾を誘う。
        public ILockOnDecoy DecoyOf(ServerLockTarget target)
        {
            if (target.IsVehicle)
            {
                return IsKnown(target.Id) ? _cars[target.Id].Flares : null;
            }

            return _riders.TryGetValue(target.Id, out var riding) ? riding.Car.Flares : null;
        }

        public bool IsLockable(int vehicle)
        {
            return IsKnown(vehicle) && !_cars[vehicle].Exploded;
        }

        public bool IsAboard(int playerId, int vehicle)
        {
            return _riders.TryGetValue(playerId, out var riding) && riding.Car.Index == vehicle;
        }

        // 撃った人は少し前の乗り物を見て捉えているので、射撃の当たりと同じく見ていた tick の姿で車体の中ほどを出す。
        public bool TryRewindCenter(int vehicle, float tick, out Vector3 center)
        {
            if (!IsLockable(vehicle))
            {
                center = default;
                return false;
            }

            var car = _cars[vehicle];
            if (!car.History.TrySample(tick, out var position, out var rotation))
            {
                position = car.Position;
                rotation = car.Rotation;
            }

            center = car.Map.Center(position, rotation);
            return true;
        }

        // 飛ぶ弾は巻き戻さずに今の位置で当たるので、追う先も乗り物の今の姿の中ほどにする。壊れた乗り物はもう追わない。
        public bool TryGetCenter(int vehicle, out Vector3 center)
        {
            if (!IsLockable(vehicle))
            {
                center = default;
                return false;
            }

            var car = _cars[vehicle];
            center = car.Map.Center(car.Position, car.Rotation);
            return true;
        }

        public void CollectOccupants(int vehicle, List<int> into)
        {
            if (!IsKnown(vehicle))
            {
                return;
            }

            foreach (var occupant in _cars[vehicle].Occupants)
            {
                if (occupant != PlayerIds.None)
                {
                    into.Add(occupant);
                }
            }
        }

        private void TickCountermeasures(float deltaSeconds)
        {
            foreach (var car in _cars)
            {
                if (car.Flares == null)
                {
                    continue;
                }

                // 間隔の残りも数えるので Tick は毎回呼ぶ。並びは空にフレアが無いあいだは作り直さず、燃え尽きた tick にだけ空へ戻す
                car.Flares.Tick(deltaSeconds, _flareGravity);
                if (car.Flares.Flares.Count > 0 || car.FlareSync.Count > 0)
                {
                    car.SyncFlares();
                }
            }
        }

        private bool IsKnown(int vehicle)
        {
            return vehicle >= 0 && vehicle < _cars.Count;
        }
    }
}
