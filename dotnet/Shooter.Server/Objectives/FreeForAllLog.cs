using System;
using System.Globalization;
using System.Text;
using Shooter.Core;

namespace Shooter.Server
{
    // 専用サーバーの FFA の陣営 (Side) は、プレイヤー Id と同じ番号で作るので、行にはプレイヤー Id として書く
    internal static class FreeForAllLog
    {
        public const string Tag = "[FreeForAll]";

        public const string Draw = "draw";

        public static string Started(FreeForAllSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return Tag + " net scoreLimit=" + Number(spec.ScoreLimit)
                + " timeLimit=" + Seconds(spec.TimeLimitSeconds)
                + "s respawn=" + Seconds(spec.RespawnSeconds)
                + "s pointsPerKill=" + Number(spec.PointsPerKill);
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

        public static string Ended(ModeSession mode)
        {
            ArgumentNullException.ThrowIfNull(mode);
            var winner = mode.Winner;
            var text = new StringBuilder(Tag).Append(" ended winner=")
                .Append(winner.HasValue ? Number(winner.Value.Id) : Draw)
                .Append(" scores=");
            var sides = mode.Sides;
            for (var i = 0; i < sides.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }

                text.Append(Number(sides[i].Id)).Append(':').Append(Number(HudFormat.DisplayedScore(mode.GetScore(sides[i]))));
            }

            return text.ToString();
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Seconds(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
