using System;

namespace Fps.Runtime
{
    /// <summary>再生しているキルカム。<see cref="GameSignals.KillCam"/> で UI に渡す。再生していない間は信号の値が null。</summary>
    public readonly struct KillCamStatus
    {
        /// <param name="shot">再生の情報。</param>
        /// <param name="progress">再生の進み (0〜1)。範囲の外は丸める。</param>
        /// <exception cref="ArgumentOutOfRangeException">progress が NaN。</exception>
        public KillCamStatus(KillCamShot shot, float progress)
        {
            if (float.IsNaN(progress))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "progress must be a number");
            }

            Shot = shot;
            Progress = Math.Clamp(progress, 0f, 1f);
        }

        public KillCamShot Shot { get; }

        /// <summary>再生の進み (0〜1)。</summary>
        public float Progress { get; }
    }
}
