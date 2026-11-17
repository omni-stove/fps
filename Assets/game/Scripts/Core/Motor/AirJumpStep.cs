namespace Shooter.Core
{
    /// <summary>
    /// 跳んだかを返すだけで速度は書かない。オフラインの <see cref="PlayerMotor"/> もネットの <see cref="CharacterMotorSimulation"/> も
    /// <see cref="PlayerMotor.StepAirJump"/> から呼び、跳んだ後の速度と Jumped の知らせを PlayerMotor の 1 か所で決めるため。
    /// </summary>
    public static class AirJumpStep
    {
        public static int Step(int airJumpsUsed, bool grounded, bool jumpPressed, MotorSpec spec, out bool jumped)
        {
            jumped = false;
            return airJumpsUsed;
        }
    }
}
