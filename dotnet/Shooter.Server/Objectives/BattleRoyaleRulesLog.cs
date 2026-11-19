using System;
using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // エリア・物資投下・分隊の決着の行は、オフラインと同じ Core の ZoneLog・SupplyDropLog・BattleRoyaleLog で書き、
    // サーバーにしか無い出来事 (分隊の振り分け・降下・地図の箱の受け渡し) をここで書く
    internal static class BattleRoyaleRulesLog
    {
        public const string Tag = "[BattleRoyale]";

        public static string Started(BattleRoyaleSpec spec, NetMapZoneCircle circle, float? dropHeight)
        {
            ArgumentNullException.ThrowIfNull(spec);
            ArgumentNullException.ThrowIfNull(circle);
            return Tag + " net squads=" + Text(spec.SquadCount) + "x" + Text(spec.SquadSize)
                + " timeLimit=" + (spec.HasTimeLimit ? Seconds(spec.TimeLimitSeconds) : "none")
                + " zone=" + Point(circle.Center) + " radius=" + Meters(circle.Radius)
                + " phases=" + Text(spec.Zone.Phases.Count)
                + " drop=" + (dropHeight.HasValue ? Meters(dropHeight.Value) : "ground")
                + " redeploys=" + Text(spec.Redeploy.Charges)
                + " supplyStages=" + Text(spec.SupplyDrops.Stages.Count);
        }

        public static string Squad(int playerId, Side squad)
        {
            return Tag + " squad player=" + Text(playerId) + " squad=" + Text(squad.Id);
        }

        public static string NoSquad(int playerId, bool live)
        {
            return Tag + " squad player=" + Text(playerId) + " squad=none ("
                + (live ? "joined after the drop and no standing squad has room" : "every squad is full") + "; the player sits out this match)";
        }

        public static string Begin(int entrants, int players)
        {
            return Tag + " begin squads=" + Text(entrants) + " players=" + Text(players);
        }

        public static string FewSquads(int entrants)
        {
            return Tag + " only " + Text(entrants) + " squad(s) have players; the last squad is decided at once";
        }

        public static string Drop(int playerId, float height, Vector3 from, bool placed)
        {
            return Tag + " drop player=" + Text(playerId) + " height=" + Meters(height) + " from=" + Point(from)
                + (placed ? string.Empty : " skipped=no-mover");
        }

        public static string Ground(int playerId)
        {
            return Tag + " drop player=" + Text(playerId) + " height=none (the map points have no dropHeight; starting on the ground)";
        }

        public static string Redeployed(int playerId, int left)
        {
            return Tag + " redeployed player=" + Text(playerId) + " left=" + Text(left);
        }

        public static string CrateLanded(int crate, int itemId)
        {
            return Tag + " crate=" + Text(crate) + " item=" + Text(itemId);
        }

        public static string ZoneDamage(int playerId, int damage, DamageOutcome outcome)
        {
            return Tag + " zone-damage player=" + Text(playerId) + " damage=" + Text(damage) + " outcome=" + outcome;
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

        public static string Ended(BattleRoyaleSession session)
        {
            ArgumentNullException.ThrowIfNull(session);
            var winner = session.Mode.Winner;
            return Tag + " state=" + session.Mode.State
                + " winner=" + (winner.HasValue ? "squad" + Text(winner.Value.Id) : MatchLog.Draw)
                + " squads=" + Text(session.EntrantCount);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Meters(float value) => value.ToString("F1", CultureInfo.InvariantCulture) + "m";

        private static string Seconds(float value) => value.ToString("F1", CultureInfo.InvariantCulture) + "s";

        private static string Point(Vector3 value)
        {
            return "(" + value.X.ToString("F2", CultureInfo.InvariantCulture)
                + "," + value.Y.ToString("F2", CultureInfo.InvariantCulture)
                + "," + value.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }
    }
}
