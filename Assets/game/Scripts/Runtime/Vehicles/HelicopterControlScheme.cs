using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class HelicopterControlScheme : MonoBehaviour, IVehicleControlScheme
    {
        // 旋回を左右の移動でなく視線から取るのは、左右の移動が横への傾きに要り、ヘリには別に回すキーが無いため。
        public VehicleMotorInput ToMotorInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            var yaw = 0f;
            if (input.HasAim)
            {
                var rotation = transform.rotation;
                yaw = HelicopterFlight.YawToward(
                    Spec(),
                    (rotation * Vector3.forward).ToNumerics(),
                    (rotation * Vector3.up).ToNumerics(),
                    input.AimDirection.ToNumerics());
            }

            return VehicleMotorInput.Idle
                .With(HelicopterFlight.CollectiveAxis, input.Vertical)
                .With(HelicopterFlight.PitchAxis, input.Move.y)
                .With(HelicopterFlight.RollAxis, input.Move.x)
                .With(HelicopterFlight.YawAxis, yaw)
                .With(HelicopterFlight.EngineAxis, 1f);
        }

        // 値をモーターから引くのは、旋回の効き方の置き場を機体のアセット 1 つに揃えるため。毎回引き直すのは、モーターを後から付け外ししても次の入力から効くようにするため。
        private HelicopterSpec Spec()
        {
            return TryGetComponent<SimpleHelicopterMotor>(out var motor) ? motor.Spec : HelicopterSpec.Default;
        }
    }
}
