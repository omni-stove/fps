using System;

namespace Fps.Core
{
    /// <summary>
    /// キルカム (<see cref="DeathCamera.KillCam"/>) の 1 回の再生の進み。<see cref="Advance"/> に経過時間と再生の速さを渡して進め、
    /// クリップの最後に着いたら終わる。クリップが空 (記録が無い) なら最初から終わっている。
    /// 再生はカメラだけのもので、リスポーンまでの時間には関わらない (呼び出し側は再生中もリスポーンの待ちを進める)。
    /// </summary>
    public sealed class KillCamPlayback
    {
        /// <param name="clip">再生するクリップ。null は空とみなす。</param>
        public KillCamPlayback(KillCamClip clip)
        {
            Clip = clip ?? KillCamClip.Empty;
            IsFinished = Clip.IsEmpty;
        }

        /// <summary>再生するクリップ。null にならない。</summary>
        public KillCamClip Clip { get; }

        /// <summary>クリップの先頭からの再生位置 (秒)。</summary>
        public float Elapsed { get; private set; }

        /// <summary>最後まで再生したか、再生できるクリップが無かったか。</summary>
        public bool IsFinished { get; private set; }

        /// <summary>再生の進み (0 から 1)。空のクリップは 1。</summary>
        public float Progress => Clip.IsEmpty ? 1f : Math.Min(1f, Elapsed / Clip.Duration);

        /// <summary>今の再生位置の姿勢。</summary>
        /// <exception cref="InvalidOperationException">クリップに姿勢が 1 つも無い。</exception>
        public KillCamPose Pose => Clip.Sample(Elapsed);

        /// <summary>
        /// deltaSeconds 秒を speed 倍で進める。この呼び出しで最後に着いたら true。もう終わっていれば何もせず false。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds が 0 以上の有限値でないか、speed が <see cref="IsValidSpeed"/> でない。</exception>
        public bool Advance(float deltaSeconds, float speed)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (!IsValidSpeed(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be positive and finite");
            }

            if (IsFinished)
            {
                return false;
            }

            Elapsed = Math.Min(Clip.Duration, Elapsed + (deltaSeconds * speed));
            if (Elapsed >= Clip.Duration)
            {
                IsFinished = true;
                return true;
            }

            return false;
        }

        /// <summary>再生の速さとして使えるか (0 より大きい有限値)。</summary>
        public static bool IsValidSpeed(float speed)
        {
            return speed > 0f && !float.IsInfinity(speed);
        }
    }
}
