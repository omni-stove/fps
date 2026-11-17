using System;

namespace Shooter.Core
{
    /// <summary>
    /// 跳んだかを返すだけで速度は書かない。オフラインの <see cref="PlayerMotor"/> もネットの <see cref="CharacterMotorSimulation"/> も
    /// <see cref="PlayerMotor.StepAirJump"/> から呼び、跳んだ後の速度と Jumped の知らせを PlayerMotor の 1 か所で決めるため。
    /// </summary>
    public static class AirJumpStep
    {
        /// <summary>
        /// 接地中は回数を 0 に戻して跳ばない (そのジャンプは <see cref="PlayerMotor.Step"/> が地上のジャンプとして跳ぶ)。
        /// 空中で jumpPressed かつ回数が <see cref="MotorSpec.AirJumps"/> より少なければ、跳んで回数を 1 増やす。
        /// </summary>
        /// <param name="airJumpsUsed">着地してから空中で跳んだ回数。0 以上。</param>
        /// <param name="grounded">接地しているか。</param>
        /// <param name="jumpPressed">今 tick にジャンプが押されたか。</param>
        /// <param name="spec">移動の設定。null 不可。</param>
        /// <param name="jumped">空中で跳んだら true。</param>
        /// <returns>この tick の後の回数。</returns>
        public static int Step(int airJumpsUsed, bool grounded, bool jumpPressed, MotorSpec spec, out bool jumped)
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

            if (!jumpPressed || airJumpsUsed >= spec.AirJumps)
            {
                return airJumpsUsed;
            }

            jumped = true;
            return airJumpsUsed + 1;
        }
    }
}
