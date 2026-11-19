using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 機体でなく GameSignals.LocalPlayer から乗っている機体を辿るのは、どの機体に乗り換えても 1 つの部品で追え、機体ごとに付けずに済ませるため。
    [DisallowMultipleComponent]
    public sealed class AircraftStatusPublisher : MonoBehaviour
    {
        private AircraftStatus? _published;
        private GameObject _riderOwner;
        private PlayerVehicleRider _ownersRider;
        private AircraftDefinition _warningSource;
        private AircraftWarningSpec _warning;

        // テストや、LocalPlayer を出さない画面が差し込む。null なら GameSignals.LocalPlayer のプレイヤーに付いたものを使う。
        public PlayerVehicleRider Rider { get; set; }

        public AircraftStatus? Published => _published;

        public AircraftStatus? Refresh()
        {
            var motor = DrivenAircraft(ResolveRider());
            if (motor == null)
            {
                Unpublish();
                return null;
            }

            var status = AircraftStatus.From(
                motor.State,
                motor.transform.position.y,
                motor.Input[AircraftFlight.ThrottleAxis],
                WarningOf(motor));
            _published = status;
            // 同じ中身を出し直さないのは、止まっている間に UI が毎フレーム描き直さないようにするため。
            if (!(AircraftSignals.Status.Value is AircraftStatus current) || current != status)
            {
                AircraftSignals.Status.Publish(status);
            }

            return status;
        }

        public static SimpleAircraftMotor DrivenAircraft(PlayerVehicleRider rider)
        {
            if (rider == null || !rider.IsRiding)
            {
                return null;
            }

            var seat = rider.Seat;
            var vehicle = rider.Vehicle;
            if (seat == null || vehicle == null || seat.Role != VehicleSeatRole.Driver)
            {
                return null;
            }

            return vehicle.TryGetComponent<SimpleAircraftMotor>(out var motor) ? motor : null;
        }

        private void OnDisable()
        {
            Unpublish();
        }

        // 機体の FixedUpdate が State を書いた後に読むよう、Update でなく LateUpdate にする。
        private void LateUpdate()
        {
            Refresh();
        }

        private PlayerVehicleRider ResolveRider()
        {
            if (Rider != null)
            {
                return Rider;
            }

            var local = GameSignals.LocalPlayer.Value;
            var player = local != null ? local.Player : null;
            if (player == null)
            {
                _riderOwner = null;
                _ownersRider = null;
                return null;
            }

            if (player != _riderOwner || _ownersRider == null)
            {
                _riderOwner = player;
                _ownersRider = player.GetComponent<PlayerVehicleRider>();
            }

            return _ownersRider;
        }

        // 毎フレーム作り直さないよう、アセットが替わったときだけ値を引き直す。
        private AircraftWarningSpec WarningOf(SimpleAircraftMotor motor)
        {
            var definition = motor.Definition;
            if (_warning == null || definition != _warningSource)
            {
                _warningSource = definition;
                _warning = definition != null ? definition.ToWarningSpec() : AircraftWarningSpec.Default;
            }

            return _warning;
        }

        private void Unpublish()
        {
            if (_published.HasValue)
            {
                // 後から別の部品が出した値までは消さない。
                AircraftSignals.Status.ClearIfCurrent(_published);
            }

            _published = null;
        }
    }
}
