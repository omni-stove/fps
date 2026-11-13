namespace Fps.Core
{
    /// <summary><see cref="SubtitleLines"/> の 1 行。</summary>
    public readonly struct SubtitleLine
    {
        public SubtitleLine(SoundCueKind kind, string text, float expiresAt)
        {
            Kind = kind;
            Text = text;
            ExpiresAt = expiresAt;
        }

        public SoundCueKind Kind { get; }

        /// <summary>出す文字 (例: 「[銃声 左後ろ]」)。</summary>
        public string Text { get; }

        /// <summary>この時刻になったら消す。</summary>
        public float ExpiresAt { get; }
    }
}
