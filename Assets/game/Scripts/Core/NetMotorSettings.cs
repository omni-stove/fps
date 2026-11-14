using System;

namespace Fps.Core
{
    /// <summary>
    /// ネット対戦のプレイヤーの移動の値 (Unity では MotorSettings と NetPlayer.prefab の CharacterController・NetworkPlayerMotor に焼いてある値)。
    /// Unity 抜きのサーバーはクライアントの予測と同じ値で動かさないと毎 tick 位置が食い違うので、<see cref="NetMatchFile"/> で同じ値を受け取る。
    /// </summary>
    public sealed class NetMotorSettings
    {
        // Unity の NetPlayer.prefab の CharacterController と NetworkPlayerMotor の既定と同じ寸法
        public const float DefaultStandingHeight = 2f;
        public const float DefaultStandingCenterY = 0f;
        public const float DefaultEyeY = 0.6f;
        public const float DefaultCrouchHeight = 1.2f;
        public const float DefaultRadius = 0.5f;

        public NetMotorSettings(MotorSpec motor, StanceGeometry stance, CapsuleMoverSpec mover)
        {
            Motor = motor ?? throw new ArgumentNullException(nameof(motor));
            Stance = stance ?? throw new ArgumentNullException(nameof(stance));
            Mover = mover ?? throw new ArgumentNullException(nameof(mover));
        }

        public static NetMotorSettings Default { get; } = new NetMotorSettings(
            MotorSpec.Default,
            new StanceGeometry(DefaultStandingHeight, DefaultStandingCenterY, DefaultEyeY, DefaultCrouchHeight, DefaultRadius),
            CapsuleMoverSpec.Default);

        public MotorSpec Motor { get; }

        public StanceGeometry Stance { get; }

        public CapsuleMoverSpec Mover { get; }
    }
}
