using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class PayloadRulesLog
    {
        public const string Tag = "[Payload]";

        public static string Started(Payload payload, NetMapPayload cart)
        {
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(cart);
            var spec = payload.Spec;
            return Tag + " net attackers=" + payload.Attackers.ToTeam()
                + " length=" + Meters(payload.Route.Length)
                + " checkpoints=" + Text(payload.Route.CheckpointCount)
                + " radius=" + Text(cart.Radius)
                + " cartSpeed=" + Text(spec.CartSpeed)
                + " minPushers=" + Text(spec.MinPushers)
                + " rollbackDelay=" + Text(spec.RollbackDelaySeconds)
                + " rollbackSpeed=" + Text(spec.RollbackSpeed)
                + " checkpointBonus=" + Text(spec.CheckpointBonusSeconds)
                + " timeLimit=" + Text(payload.Session.TimeLimitSeconds);
        }

        // 1 回の Tick で台車が中継点を越えて進むこともあるので、進みは台車の今の位置でなく中継点の位置で書く (PayloadLog と同じ)
        public static string Checkpoint(Payload payload, int checkpoint)
        {
            ArgumentNullException.ThrowIfNull(payload);
            var route = payload.Route;
            return Tag + " checkpoint=" + Text(checkpoint + 1) + "/" + Text(route.CheckpointCount)
                + " progress=" + Fraction(route.FractionAt(route.CheckpointDistance(checkpoint)))
                + " bonus=" + Seconds(payload.Spec.CheckpointBonusSeconds)
                + " remaining=" + Seconds(payload.Session.RemainingSeconds);
        }

        public static string Motion(Payload payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            return Tag + " cart=" + PayloadLog.MotionName(payload)
                + " pushers=" + Text(payload.Pushers)
                + " defenders=" + Text(payload.CountNearCart(payload.Defenders))
                + " progress=" + Fraction(payload.Progress);
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

        public static string State(Payload payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            var session = payload.Session;
            var text = Tag + " state=" + session.State
                + " delivered=" + (payload.IsDelivered ? "true" : "false")
                + " progress=" + Fraction(payload.Progress)
                + " checkpoints=" + Text(payload.CheckpointsReached) + "/" + Text(payload.Route.CheckpointCount)
                + " remaining=" + Seconds(Math.Max(0f, session.RemainingSeconds));
            return session.State == GameModeState.Ended ? text + " winner=" + WinnerName(Side.ToTeam(session.Winner)) : text;
        }

        private static string WinnerName(Team? team) => team.HasValue ? team.Value.ToString() : MatchLog.Draw;

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Fraction(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

        private static string Meters(double value) => value.ToString("F1", CultureInfo.InvariantCulture) + "m";

        private static string Seconds(float value) => value.ToString("F1", CultureInfo.InvariantCulture) + "s";
    }
}
