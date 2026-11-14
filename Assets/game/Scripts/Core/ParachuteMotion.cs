using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// パラシュート (<see cref="Parachute.On"/>) で降りる動き。開けるか (<see cref="Evaluate"/>) と、開いている間の速度 (<see cref="Step"/>) を決める。
    /// 下へは一定の <see cref="DescentSpeed"/> で降り、横は移動の入力の向きへ <see cref="HorizontalSpeed"/> まで
    /// <see cref="SteerAcceleration"/> で寄せる (入力が無ければ止まっていく)。衝突判定と着地の検出は呼ぶ側が受け持つ。
    /// 座標は <see cref="PlayerMotor"/> と同じく、水平ベクトルの X がワールドの X、Y がワールドの Z。
    /// </summary>
    public sealed class ParachuteMotion
    {
        /// <summary>生成時に指定しなかったときの <see cref="MinOpenHeight"/>。</summary>
        public const float DefaultMinOpenHeight = 6f;

        /// <summary>
        /// 生成時に指定しなかったときの <see cref="DescentSpeed"/>。<see cref="MotorSpec.DefaultSafeFallSpeed"/> より遅く、
        /// 開いたまま着地しても落下ダメージの閾値に届かない。
        /// </summary>
        public const float DefaultDescentSpeed = 4f;

        /// <summary>生成時に指定しなかったときの <see cref="HorizontalSpeed"/>。</summary>
        public const float DefaultHorizontalSpeed = 6f;

        /// <summary>生成時に指定しなかったときの <see cref="SteerAcceleration"/>。</summary>
        public const float DefaultSteerAcceleration = 8f;

        private Vector2 _horizontalVelocity;

        /// <param name="minOpenHeight">開ける、足元から地面までの最低の高さ (m)。0 より大きい有限値。</param>
        /// <param name="descentSpeed">開いている間に降りる速さ (m/s)。0 より大きい有限値。</param>
        /// <param name="horizontalSpeed">開いている間の横の速さの上限 (m/s)。0 以上の有限値。</param>
        /// <param name="steerAcceleration">横の速度を入力の向きへ寄せる加速度 (m/s²)。0 より大きい有限値。</param>
        public ParachuteMotion(
            float minOpenHeight = DefaultMinOpenHeight,
            float descentSpeed = DefaultDescentSpeed,
            float horizontalSpeed = DefaultHorizontalSpeed,
            float steerAcceleration = DefaultSteerAcceleration)
        {
            if (!(minOpenHeight > 0f) || float.IsInfinity(minOpenHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(minOpenHeight), minOpenHeight, "minOpenHeight must be positive and finite");
            }

            if (!(descentSpeed > 0f) || float.IsInfinity(descentSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(descentSpeed), descentSpeed, "descentSpeed must be positive and finite");
            }

            if (!(horizontalSpeed >= 0f) || float.IsInfinity(horizontalSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalSpeed), horizontalSpeed, "horizontalSpeed must be finite and not negative");
            }

            if (!(steerAcceleration > 0f) || float.IsInfinity(steerAcceleration))
            {
                throw new ArgumentOutOfRangeException(nameof(steerAcceleration), steerAcceleration, "steerAcceleration must be positive and finite");
            }

            MinOpenHeight = minOpenHeight;
            DescentSpeed = descentSpeed;
            HorizontalSpeed = horizontalSpeed;
            SteerAcceleration = steerAcceleration;
        }

        /// <summary>開ける、足元から地面までの最低の高さ (m)。</summary>
        public float MinOpenHeight { get; }

        /// <summary>開いている間に降りる速さ (m/s、正の値で下向き)。</summary>
        public float DescentSpeed { get; }

        /// <summary>開いている間の横の速さの上限 (m/s)。</summary>
        public float HorizontalSpeed { get; }

        /// <summary>横の速度を入力の向きへ寄せる加速度 (m/s²)。</summary>
        public float SteerAcceleration { get; }

        /// <summary>今の横の速度 (m/s、ワールドの水平)。<see cref="Open"/> で始め、<see cref="Step"/> で変わる。</summary>
        public Vector2 HorizontalVelocity => _horizontalVelocity;

        /// <summary>今の速度 (m/s、ワールド)。Y は <see cref="DescentSpeed"/> の下向き。</summary>
        public Vector3 Velocity => new Vector3(_horizontalVelocity.X, -DescentSpeed, _horizontalVelocity.Y);

        /// <summary>
        /// 開けるかを決める。理由が重なるときは、ルール・もう開いている・ほかの動き・高さの順に最初のものを返す。
        /// </summary>
        /// <param name="rule">ルールの <see cref="Parachute"/>。定義済みの値。</param>
        /// <param name="isOpen">もう開いているか。</param>
        /// <param name="isBusy">ほかの動きが移動を受け持っているか。</param>
        /// <param name="heightAboveGround">足元から地面までの高さ (m)。0 以上。地面が見つからなければ正の無限大。</param>
        public ParachuteOpenVerdict Evaluate(Parachute rule, bool isOpen, bool isBusy, float heightAboveGround)
        {
            if (!Enum.IsDefined(typeof(Parachute), rule))
            {
                throw new ArgumentOutOfRangeException(nameof(rule), rule, "rule is not a defined Parachute");
            }

            if (!(heightAboveGround >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(heightAboveGround), heightAboveGround, "heightAboveGround must not be negative or NaN");
            }

            if (rule != Parachute.On)
            {
                return ParachuteOpenVerdict.RuleOff;
            }

            if (isOpen)
            {
                return ParachuteOpenVerdict.AlreadyOpen;
            }

            if (isBusy)
            {
                return ParachuteOpenVerdict.Busy;
            }

            return heightAboveGround < MinOpenHeight ? ParachuteOpenVerdict.TooLow : ParachuteOpenVerdict.Open;
        }

        /// <summary>
        /// 開いたときの横の速度を、それまでの横の速度 (m/s、ワールドの水平) から決める。<see cref="HorizontalSpeed"/> を超える分は切り詰める。
        /// 縦の速度は捨て、次の <see cref="Step"/> から <see cref="DescentSpeed"/> で降りる。
        /// </summary>
        public void Open(Vector2 horizontalVelocity)
        {
            if (!IsFinite(horizontalVelocity.X) || !IsFinite(horizontalVelocity.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalVelocity), horizontalVelocity, "horizontalVelocity must be finite");
            }

            _horizontalVelocity = ClampLength(horizontalVelocity, HorizontalSpeed);
        }

        /// <summary>
        /// deltaSeconds 進め、横の速度を移動の入力の向きへ寄せて、この間の終わりの速度 (<see cref="Velocity"/>) を返す。
        /// 入力の大きさは 1 で切り詰め、少し倒しただけなら遅く進む。
        /// </summary>
        /// <param name="moveInput">移動入力 (X 右・Y 前)。各成分が有限値。</param>
        /// <param name="yawRadians">向き (<see cref="PlayerMotor.Step"/> と同じ)。有限値。</param>
        /// <param name="deltaSeconds">経過秒数。0 以上の有限値。</param>
        public Vector3 Step(Vector2 moveInput, float yawRadians, float deltaSeconds)
        {
            if (!IsFinite(moveInput.X) || !IsFinite(moveInput.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(moveInput), moveInput, "moveInput must be finite");
            }

            if (!IsFinite(yawRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(yawRadians), yawRadians, "yawRadians must be finite");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            var target = PlayerMotor.ToWorld(ClampLength(moveInput, 1f), yawRadians) * HorizontalSpeed;
            var toTarget = target - _horizontalVelocity;
            var maxChange = SteerAcceleration * deltaSeconds;
            var distance = toTarget.Length();

            // 残りが 1 Tick で寄せられる量以下なら、行き過ぎて揺れないよう目標ちょうどに合わせる。
            _horizontalVelocity = distance <= maxChange ? target : _horizontalVelocity + (toTarget * (maxChange / distance));
            return Velocity;
        }

        /// <summary>横の速度を 0 にする (閉じたときなど)。</summary>
        public void Reset()
        {
            _horizontalVelocity = Vector2.Zero;
        }

        private static Vector2 ClampLength(Vector2 value, float max)
        {
            var length = value.Length();
            return length > max && length > 0f ? value * (max / length) : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
