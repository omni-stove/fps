using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 入力を受け取るだけにし、力を掛けるのは実装の FixedUpdate に任せる。座席の入力は Update で届くので、その場で力を掛けると描画の頻度で物理が変わるため。
    public interface IVehicleMotor
    {
        Rigidbody Body { get; }

        VehicleMotorInput Input { get; }

        void SetInput(VehicleMotorInput input);
    }
}
