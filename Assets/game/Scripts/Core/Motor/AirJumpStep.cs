using System;

namespace Shooter.Core
{
    // 跳んだかを返すだけで速度を書かないのは、オフラインの PlayerMotor もネットの CharacterMotorSimulation も PlayerMotor.StepAirJump から呼び、
    // 跳んだ後の速度と Jumped の知らせを PlayerMotor の 1 か所で決めるため。
    public static class AirJumpStep
    {
        public static int Step(int airJumpsUsed, bool grounded, bool jumpPressed, MotorSpec spec, out bool jumped)
        {
            return Step(airJumpsUsed, grounded, false, jumpPressed, spec, out jumped);
        }

        // 猶予の間の押下は PlayerMotor.Step が地上のジャンプとして跳ぶので、ここでは跳ばずに数えもしない。
        public static int Step(int airJumpsUsed, bool grounded, bool inJumpGrace, bool jumpPressed, MotorSpec spec, out bool jumped)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (airJumpsUsed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(airJumpsUsed), airJumpsUsed, "airJumpsUsed must not be negative");
            }

            jumped = false;
            if (grounded)
            {
                return 0;
            }

            if (inJumpGrace || !jumpPressed || airJumpsUsed >= spec.AirJumps)
            {
                return airJumpsUsed;
            }

            jumped = true;
            return airJumpsUsed + 1;
        }
    }
}
