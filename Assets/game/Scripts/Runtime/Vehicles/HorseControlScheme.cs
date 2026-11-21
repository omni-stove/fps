using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class HorseControlScheme : MonoBehaviour, IVehicleControlScheme
    {
        // 旋回を視線でなく左右の移動から取るのは、馬上では横を向いて撃つことが多く、視線に向きを合わせると狙うたびに進む向きが変わるため。
        public VehicleMotorInput ToMotorInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            return VehicleMotorInput.Idle
                .With(HorseStride.ThrottleAxis, input.Move.y)
                .With(HorseStride.TurnAxis, input.Move.x)
                .With(HorseStride.GallopAxis, input.Boost ? 1f : 0f)
                // 負の Vertical を捨てるのは、下げる向き (しゃがみ・降下) に当たる動きが馬に無く、ジャンプの軸に負の意味を持たせないため。
                .With(HorseStride.JumpAxis, Mathf.Max(0f, input.Vertical));
        }
    }
}
