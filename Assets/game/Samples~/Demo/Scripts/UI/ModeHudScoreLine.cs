using Shooter.Core;

namespace Shooter.Runtime
{
    public readonly struct ModeHudScoreLine
    {
        public Side Side { get; }

        public LocalizedText Text { get; }

        public bool IsOwn { get; }

        public bool IsLeading { get; }

        public ModeHudScoreLine(Side side, LocalizedText text, bool isOwn, bool isLeading)
        {
            Side = side;
            Text = text;
            IsOwn = isOwn;
            IsLeading = isLeading;
        }
    }
}
