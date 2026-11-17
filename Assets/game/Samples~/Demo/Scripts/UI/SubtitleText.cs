using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class SubtitleText
    {
        public static string FarText => GameText.Get("subtitle.far");

        public static string Format(SoundCue cue)
        {
            var text = "[" + KindText(cue.Kind);
            if (cue.Direction != SoundCueDirection.None)
            {
                text += " " + DirectionText(cue.Direction);
            }

            if (cue.IsFar)
            {
                text += " " + FarText;
            }

            return text + "]";
        }

        public static string KindText(SoundCueKind kind)
        {
            switch (kind)
            {
                case SoundCueKind.Gunshot:
                case SoundCueKind.Footstep:
                case SoundCueKind.Explosion:
                case SoundCueKind.Reload:
                case SoundCueKind.Clatter:
                    return GameText.Get("subtitle.kind." + kind);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown sound cue kind");
            }
        }

        public static string DirectionText(SoundCueDirection direction)
        {
            switch (direction)
            {
                case SoundCueDirection.None:
                    return string.Empty;
                case SoundCueDirection.Front:
                case SoundCueDirection.FrontRight:
                case SoundCueDirection.Right:
                case SoundCueDirection.BackRight:
                case SoundCueDirection.Back:
                case SoundCueDirection.BackLeft:
                case SoundCueDirection.Left:
                case SoundCueDirection.FrontLeft:
                    return GameText.Get("subtitle.direction." + direction);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "unknown sound cue direction");
            }
        }
    }
}
