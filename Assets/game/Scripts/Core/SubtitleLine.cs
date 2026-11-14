namespace Fps.Core
{
    /// <summary><see cref="SubtitleLines"/> の 1 行。音の合図の行か、誰かが話した行 (<see cref="IsSpeech"/>)。</summary>
    public readonly struct SubtitleLine
    {
        public SubtitleLine(SoundCueKind kind, string text, float expiresAt)
            : this(kind, null, text, expiresAt)
        {
        }

        private SubtitleLine(SoundCueKind kind, string speaker, string text, float expiresAt)
        {
            Kind = kind;
            Speaker = speaker;
            Text = text;
            ExpiresAt = expiresAt;
        }

        /// <summary>speaker が話した行。</summary>
        public static SubtitleLine Speech(string speaker, string text, float expiresAt)
        {
            return new SubtitleLine(default, speaker ?? string.Empty, text, expiresAt);
        }

        /// <summary>音の合図の種類。話した行 (<see cref="IsSpeech"/>) では使わない。</summary>
        public SoundCueKind Kind { get; }

        /// <summary>話した人。音の合図の行なら null。</summary>
        public string Speaker { get; }

        public bool IsSpeech => Speaker != null;

        /// <summary>出す文字 (例: 「[銃声 左後ろ]」「Bot_1: 了解、そこを守る」)。</summary>
        public string Text { get; }

        /// <summary>この時刻になったら消す。</summary>
        public float ExpiresAt { get; }
    }
}
