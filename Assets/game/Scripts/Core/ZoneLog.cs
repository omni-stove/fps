using System;
using System.Globalization;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 縮むエリア (<see cref="ZoneSchedule"/>) の <c>[Zone]</c> 付きログの文言。key=value を空白で区切り、値に空白を入れない。
    /// 段階は 1 始まりで <c>phase=1/3</c> のように全体の数と並べて書く。位置は水平面 (x, z)、長さは小数 2 桁、秒とダメージは小数 1 桁。
    /// </summary>
    public static class ZoneLog
    {
        public const string Tag = "[Zone]";

        /// <summary>エリアを動かし始めた。</summary>
        /// <example><c>[Zone] start center=(0.00, 0.00) radius=60.00 phases=3 seed=7 total=150.0</c></example>
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

        /// <summary>段階か動き (待ち・縮み・最後) が変わった。</summary>
        /// <example>
        /// <c>[Zone] phase=1/3 stage=Waiting radius=60.00 center=(0.00, 0.00) next_radius=35.00 next_center=(4.12, -9.80) dps=1.0 in=30.0</c>。
        /// <c>in</c> は今の動きが終わるまでの秒数。最後の円 (<c>stage=Closed</c>) では next_ と in を書かない。
        /// </example>
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

        /// <summary>id の者がエリアの外に出た。</summary>
        /// <example><c>[Zone] outside target=Player</c></example>
        public static string Outside(string id)
        {
            return Tag + " outside target=" + Token(id);
        }

        /// <summary>id の者がエリアの中に戻った。</summary>
        /// <example><c>[Zone] inside target=Player</c></example>
        public static string Inside(string id)
        {
            return Tag + " inside target=" + Token(id);
        }

        /// <example><c>[Zone] stop</c></example>
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
