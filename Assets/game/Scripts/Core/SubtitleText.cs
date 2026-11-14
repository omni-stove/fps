using System;

namespace Fps.Core
{
    /// <summary>音の合図の字幕の書き方。「[種類 向き]」で、向きが分からなければ「[種類]」、遠ければ末尾に「遠く」を足す。</summary>
    public static class SubtitleText
    {
        /// <summary>遠い合図 (<see cref="SoundCue.IsFar"/>) の末尾に足す語。</summary>
        public const string FarText = "遠く";

        /// <summary>cue の字幕 (例: 「[銃声 左後ろ]」「[爆発]」「[足音 右 遠く]」)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">種類か向きが定義済みの値でない。</exception>
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

        /// <exception cref="ArgumentOutOfRangeException">kind が定義済みの値でない。</exception>
        public static string KindText(SoundCueKind kind)
        {
            switch (kind)
            {
                case SoundCueKind.Gunshot:
                    return "銃声";
                case SoundCueKind.Footstep:
                    return "足音";
                case SoundCueKind.Explosion:
                    return "爆発";
                case SoundCueKind.Reload:
                    return "リロード";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown sound cue kind");
            }
        }

        /// <summary>向きの語。<see cref="SoundCueDirection.None"/> は空文字。</summary>
        /// <exception cref="ArgumentOutOfRangeException">direction が定義済みの値でない。</exception>
        public static string DirectionText(SoundCueDirection direction)
        {
            switch (direction)
            {
                case SoundCueDirection.None:
                    return string.Empty;
                case SoundCueDirection.Front:
                    return "正面";
                case SoundCueDirection.FrontRight:
                    return "右前";
                case SoundCueDirection.Right:
                    return "右";
                case SoundCueDirection.BackRight:
                    return "右後ろ";
                case SoundCueDirection.Back:
                    return "後ろ";
                case SoundCueDirection.BackLeft:
                    return "左後ろ";
                case SoundCueDirection.Left:
                    return "左";
                case SoundCueDirection.FrontLeft:
                    return "左前";
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "unknown sound cue direction");
            }
        }
    }
}
