using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // タグの出来事 (落ちた・拾われた・消えた) はオフラインと同じ KillConfirmedText の行で残し、ここは専用サーバーだけの行を組み立てる
    internal static class ServerKillConfirmedLog
    {
        public static string Settings(KillConfirmedSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return KillConfirmedText.LogPrefix + " net scoreLimit=" + Number(spec.ScoreLimit)
                + " timeLimit=" + Number(spec.TimeLimitSeconds)
                + "s respawn=" + Number(spec.RespawnSeconds)
                + "s tagLifetime=" + Number(spec.TagLifetimeSeconds)
                + "s pointsPerConfirm=" + Number(spec.PointsPerConfirm)
                + " tagPickupRadius=" + Number(spec.TagPickupRadius);
        }

        public static string Restart()
        {
            return KillConfirmedText.LogPrefix + " restart";
        }

        public static string NoBody(int playerId)
        {
            return KillConfirmedText.LogPrefix + " no tag player=" + Number(playerId) + " reason=no body";
        }

        public static string State(KillConfirmed killConfirmed)
        {
            ArgumentNullException.ThrowIfNull(killConfirmed);
            var session = killConfirmed.Session;
            var winner = Side.ToTeam(session.Winner);
            var winnerText = session.State != GameModeState.Ended ? "-" : winner.HasValue ? winner.Value.ToString() : MatchLog.Draw;
            return KillConfirmedText.LogPrefix + " state=" + session.State
                + " winner=" + winnerText
                + " score=" + Number(KillConfirmedRules.ScoreOf(session.GetScore(Side.Alpha)))
                + "-" + Number(KillConfirmedRules.ScoreOf(session.GetScore(Side.Bravo)))
                + " confirmed=" + Number(killConfirmed.GetConfirmed(Side.Alpha)) + "-" + Number(killConfirmed.GetConfirmed(Side.Bravo))
                + " denied=" + Number(killConfirmed.GetDenied(Side.Alpha)) + "-" + Number(killConfirmed.GetDenied(Side.Bravo))
                + " remaining=" + session.RemainingSeconds.ToString("F1", CultureInfo.InvariantCulture);
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Number(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
