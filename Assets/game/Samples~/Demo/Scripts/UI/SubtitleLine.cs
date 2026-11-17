using Shooter.Core;

namespace Shooter.Runtime
{
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

        public static SubtitleLine Speech(string speaker, string text, float expiresAt)
        {
            return new SubtitleLine(default, speaker ?? string.Empty, text, expiresAt);
        }

        public SoundCueKind Kind { get; }

        public string Speaker { get; }

        public bool IsSpeech => Speaker != null;

        public string Text { get; }

        public float ExpiresAt { get; }
    }
}
