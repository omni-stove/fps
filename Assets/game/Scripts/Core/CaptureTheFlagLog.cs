using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Shooter.Core
{
    public static class CaptureTheFlagLog
    {
        public const string Tag = ModeLog.Tag;

        public static string Taken(Side flag, int playerId)
        {
            return Tag + " flag-taken flag=" + Number(flag.Id) + " player=" + Number(playerId);
        }

        public static string Dropped(Side flag, int playerId)
        {
            return Tag + " flag-dropped flag=" + Number(flag.Id) + " player=" + Number(playerId);
        }

        public static string Returned(Side flag, int? playerId)
        {
            return Tag + " flag-returned flag=" + Number(flag.Id) + " by=" + Optional(playerId);
        }

        public static string Captured(Side flag, int playerId, Side side, double score)
        {
            return Tag + " flag-captured flag=" + Number(flag.Id) + " player=" + Number(playerId)
                + " side=" + Number(side.Id) + " score=" + ModeLog.FormatScore(score);
        }

        public static string NetRules(CaptureTheFlagSpec spec, float respawnSeconds)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Tag + " ctf net scoreLimit=" + Number(spec.ScoreLimit)
                + " timeLimit=" + Seconds(spec.TimeLimitSeconds)
                + " respawn=" + Seconds(respawnSeconds)
                + " returnTime=" + Seconds(spec.ReturnSeconds)
                + " pickupRadius=" + Decimal(spec.PickupRadius)
                + " captureRadius=" + Decimal(spec.CaptureRadius)
                + " pointsPerCapture=" + Number(spec.PointsPerCapture);
        }

        public static string Flags(CaptureTheFlag game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            var text = new StringBuilder(Tag).Append(" flags");
            foreach (var side in CaptureTheFlag.FlagSides)
            {
                var state = game.GetFlagState(side);
                text.Append(' ').Append(Number(side.Id)).Append('=').Append(state);
                switch (state)
                {
                    case FlagState.Carried:
                        text.Append('(').Append(Optional(game.GetCarrierId(side))).Append(')');
                        break;
                    case FlagState.Dropped:
                        text.Append('(').Append((game.GetReturnRemaining(side) ?? 0f).ToString("F1", CultureInfo.InvariantCulture)).Append(')');
                        break;
                }
            }

            return text.ToString();
        }

        public static string Errand(int playerId, BotErrandKind kind, Vector3 destination)
        {
            var text = Tag + " errand player=" + Number(playerId) + " kind=" + kind;
            if (kind == BotErrandKind.None)
            {
                return text;
            }

            return text + " at=(" + destination.X.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + destination.Y.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + destination.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        private static string Optional(int? value)
        {
            return value.HasValue ? Number(value.Value) : "-";
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Seconds(float value)
        {
            return Decimal(value) + "s";
        }

        private static string Decimal(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
