using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // Vehicle に動き方を持たせず別の部品にするのは、固定銃座のように動き方の無い乗り物が、この部品を付けないだけで済むようにするため。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Vehicle))]
    public sealed class VehicleMotorLink : MonoBehaviour, IVehicleInputSink
    {
        private Vehicle _vehicle;
        private IVehicleMotor _motor;
        private bool _bound;

        public Vehicle Vehicle => _vehicle;

        public IVehicleMotor Motor => _motor;

        public bool IsBound => _bound;

        public IVehicleControlScheme ControlScheme { get; set; }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        // OnEnable の走らない Edit Mode のテストや、動き方の部品を後から足したときにも結び直せるよう public にする。
        public bool Bind()
        {
            if (_bound)
            {
                return true;
            }

            if (_vehicle == null)
            {
                _vehicle = GetComponent<Vehicle>();
            }

            _motor = GetComponent<IVehicleMotor>();
            if (_vehicle == null || _motor == null)
            {
                Debug.LogWarning(VehicleLog.MissingMotor(name), this);
                return false;
            }

            _vehicle.AddInputSink(this);
            _bound = true;
            return true;
        }

        public void Unbind()
        {
            if (!_bound)
            {
                return;
            }

            _bound = false;
            if (_vehicle != null)
            {
                _vehicle.RemoveInputSink(this);
            }

            if (_motor is Object unityMotor ? unityMotor != null : _motor != null)
            {
                _motor.SetInput(VehicleMotorInput.Idle);
            }
        }

        public void OnSeatInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            if (seat == null || seat.Role != VehicleSeatRole.Driver || _motor == null)
            {
                return;
            }

            _motor.SetInput(VehicleControlSchemes.For(gameObject).ToMotorInput(seat, input, deltaSeconds));
        }

        public void OnSeatVacated(VehicleSeat seat)
        {
            if (seat == null || seat.Role != VehicleSeatRole.Driver || _motor == null)
            {
                return;
            }

            _motor.SetInput(VehicleMotorInput.Idle);
            if (VehicleControlSchemes.For(gameObject) is IVehicleControlSchemeReset reset)
            {
                reset.ResetState();
            }
        }
    }
}
