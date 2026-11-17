using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    // key=value を空白で区切って読めるよう、値に空白を入れない。
    public static class ZoneLog
    {
        public const string Tag = "[Zone]";

        public static string Started(ZoneSchedule schedule)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            return Tag + " start center=" + Point(schedule.InitialCenter)
                + " radius=" + Length(schedule.InitialRadius)
                + " phases=" + schedule.PhaseCount.ToString(CultureInfo.InvariantCulture)
                + " seed=" + schedule.Seed.ToString(CultureInfo.InvariantCulture)
                + " total=" + Tenths(schedule.TotalSeconds);
        }

        public static string Stage(ZoneState state, int phaseCount)
        {
            if (phaseCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(phaseCount), phaseCount, "phaseCount must be positive");
            }

            if (state.PhaseIndex < 0 || state.PhaseIndex >= phaseCount)
            {
                throw new ArgumentOutOfRangeException(nameof(state), state.PhaseIndex, "state.PhaseIndex must be below phaseCount");
            }

            var line = Tag + " phase=" + (state.PhaseIndex + 1).ToString(CultureInfo.InvariantCulture)
                + "/" + phaseCount.ToString(CultureInfo.InvariantCulture)
                + " stage=" + state.Stage
                + " radius=" + Length(state.Radius)
                + " center=" + Point(state.Center);
            if (state.Stage != ZoneStage.Closed)
            {
                line += " next_radius=" + Length(state.TargetRadius)
                    + " next_center=" + Point(state.TargetCenter);
            }

            line += " dps=" + Tenths(state.DamagePerSecond);
            if (state.Stage != ZoneStage.Closed)
            {
                line += " in=" + Tenths(state.SecondsUntilStageEnds);
            }

            return line;
        }

        public static string Outside(string id)
        {
            return Tag + " outside target=" + Token(id);
        }

        public static string Inside(string id)
        {
            return Tag + " inside target=" + Token(id);
        }

        public static string Stopped()
        {
            return Tag + " stop";
        }

        private static string Point(Vector2 point)
        {
            return "(" + Length(point.X) + ", " + Length(point.Y) + ")";
        }

        private static string Length(float value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string Tenths(float value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture);
        }

        private static string Token(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "-";
            }

            var chars = text.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
