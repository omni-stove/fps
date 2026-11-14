namespace Fps.Core
{
    /// <summary><see cref="TimeScaleArbitration"/> が受けている時間の速さの要求 1 つ。</summary>
    public readonly struct TimeScaleRequest
    {
        public TimeScaleRequest(string reason, float scale, float remainingSeconds)
        {
            Reason = reason;
            Scale = scale;
            RemainingSeconds = remainingSeconds;
        }

        /// <summary>要求の理由。同じ理由の要求は 1 つにまとまる。</summary>
        public string Reason { get; }

        /// <summary>求める時間の速さ (0 より大きく 1 以下)。</summary>
        public float Scale { get; }

        /// <summary>期限までの残りの実時間 (秒)。</summary>
        public float RemainingSeconds { get; }

        public override string ToString()
        {
            return TimeScaleArbitration.FormatRequest(this);
        }
    }
}
