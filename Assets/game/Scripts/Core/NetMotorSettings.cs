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
            : this(motor, stance, mover, ProneSpec.Default)
        {
        }

        public NetMotorSettings(MotorSpec motor, StanceGeometry stance, CapsuleMoverSpec mover, ProneSpec prone)
        {
            Motor = motor ?? throw new ArgumentNullException(nameof(motor));
            Stance = stance ?? throw new ArgumentNullException(nameof(stance));
            Mover = mover ?? throw new ArgumentNullException(nameof(mover));
            Prone = prone ?? throw new ArgumentNullException(nameof(prone));
        }

        public static NetMotorSettings Default { get; } = new NetMotorSettings(
            MotorSpec.Default,
            new StanceGeometry(
                DefaultStandingHeight,
                DefaultStandingCenterY,
                DefaultEyeY,
                DefaultCrouchHeight,
                FitProneHeight(ProneSpec.DefaultHeight, DefaultCrouchHeight, DefaultRadius),
                DefaultRadius),
            CapsuleMoverSpec.Default,
            ProneSpec.Default);

        public MotorSpec Motor { get; }

        public StanceGeometry Stance { get; }

        public CapsuleMoverSpec Mover { get; }

        /// <summary>伏せの速さと飛び込み。伏せの高さは <see cref="Stance"/> の <see cref="StanceGeometry.Prone"/> を使う。</summary>
        public ProneSpec Prone { get; }

        /// <summary>
        /// PlayerController と同じく、カプセルが取れる高さ (半径の 2 倍以上、しゃがみ以下) に収める。
        /// 伏せの高さをゲームが変えても、カプセルを作れずに動けなくなるより近い高さで伏せる方がよいため。
        /// </summary>
        public static float FitProneHeight(float height, float crouchHeight, float radius)
        {
            var low = Math.Min(radius * 2f, crouchHeight);
            return height < low ? low : (height > crouchHeight ? crouchHeight : height);
        }
    }
}
