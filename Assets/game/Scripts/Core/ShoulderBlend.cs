using System;

namespace Fps.Core
{
    /// <summary>
    /// 三人称のカメラを右肩と左肩の間で入れ替えるときの補間。目標の肩 (<see cref="Target"/>) と、そこへの進み具合
    /// (<see cref="Progress"/>、0 が反対の肩・1 が目標の肩) を持ち、三人称のオフセットの x に掛ける符号 (<see cref="OffsetSign"/>) を出す。
    /// </summary>
    public sealed class ShoulderBlend
    {
        /// <summary>片方の肩からもう片方へ移り切るまでの既定の秒数。</summary>
        public const float DefaultSeconds = 0.15f;

        /// <param name="initial">初めの肩。補間せず、その肩にいる状態から始める。</param>
        /// <param name="seconds">片方の肩からもう片方へ移り切るまでの秒数。0 以上の有限値。0 なら即座に入れ替わる。</param>
        public ShoulderBlend(ShoulderSide initial = ShoulderSide.Right, float seconds = DefaultSeconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be a non-negative finite number");
            }

            Target = ValidateSide(initial, nameof(initial));
            Seconds = seconds;
            Progress = 1f;
        }

        /// <summary>片方の肩からもう片方へ移り切るまでの秒数。</summary>
        public float Seconds { get; }

        /// <summary>向かっている (補間が済んでいればいる) 肩。</summary>
        public ShoulderSide Target { get; private set; }

        /// <summary>目標の肩への進み具合。0 が反対の肩、1 が目標の肩。</summary>
        public float Progress { get; private set; }

        /// <summary>補間の途中か。</summary>
        public bool IsBlending => Progress < 1f;

        /// <summary>
        /// 三人称のオフセットの x に掛ける符号。右肩で +1、左肩で -1、補間中はその間。
        /// 動き始めと止まり際が急にならないよう、進み具合を smoothstep で緩める。
        /// </summary>
        public float OffsetSign
        {
            get
            {
                var to = Sign(Target);
                var t = Progress;
                var eased = t * t * (3f - (2f * t));
                return -to + (2f * to * eased);
            }
        }

        /// <summary>
        /// 目標の肩を反対にする。補間の途中なら今の位置から折り返す。
        /// smoothstep は s(1 - p) = 1 - s(p) なので、進み具合を 1 - p にすれば <see cref="OffsetSign"/> は飛ばない。
        /// </summary>
        public void Toggle()
        {
            Target = Target == ShoulderSide.Right ? ShoulderSide.Left : ShoulderSide.Right;
            Progress = 1f - Progress;
        }

        /// <summary>目標の肩を side にする。今の目標と同じなら何もしない。違えば <see cref="Toggle"/> と同じ。</summary>
        /// <returns>目標が変わったら true。</returns>
        public bool SetTarget(ShoulderSide side)
        {
            if (ValidateSide(side, nameof(side)) == Target)
            {
                return false;
            }

            Toggle();
            return true;
        }

        /// <summary>deltaSeconds だけ補間を進め、進めた後の <see cref="OffsetSign"/> を返す。</summary>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
        public float Tick(float deltaSeconds)
        {
            ValidateDeltaSeconds(deltaSeconds);

            Progress = Seconds <= 0f ? 1f : Math.Min(1f, Progress + (deltaSeconds / Seconds));
            return OffsetSign;
        }

        /// <summary><see cref="Tick"/> が受け付ける秒数か調べ、違えば例外を投げる。状態を変える前に検査したい呼び出し元のため。</summary>
        public static void ValidateDeltaSeconds(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }
        }

        private static float Sign(ShoulderSide side)
        {
            return side == ShoulderSide.Left ? -1f : 1f;
        }

        private static ShoulderSide ValidateSide(ShoulderSide side, string paramName)
        {
            if (side != ShoulderSide.Right && side != ShoulderSide.Left)
            {
                throw new ArgumentOutOfRangeException(paramName, side, "unknown shoulder side");
            }

            return side;
        }
    }
}
