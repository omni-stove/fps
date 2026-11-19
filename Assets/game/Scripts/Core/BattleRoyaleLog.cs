using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Shooter.Core
{
    public static class BattleRoyaleLog
    {
        public const string Tag = ModeLog.Tag;

        public static string Downed(int playerId, int? downerId)
        {
            return Tag + " downed player=" + Number(playerId) + " by=" + Optional(downerId);
        }

        public static string Revived(int reviverId, int playerId)
        {
            return Tag + " revived player=" + Number(playerId) + " by=" + Number(reviverId);
        }

        public static string BledOut(int playerId, int? creditId)
        {
            return Tag + " bledout player=" + Number(playerId) + " credit=" + Optional(creditId);
        }

        public static string SquadWiped(int playerId, int? creditId)
        {
            return Tag + " squad-wiped player=" + Number(playerId) + " credit=" + Optional(creditId);
        }

        public static string Eliminated(Side squad, int place, int remaining)
        {
            return Tag + " eliminated squad=" + Number(squad.Id) + " place=" + Number(place) + " remaining=" + Number(remaining);
        }

        public static string Placements(BattleRoyaleSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var winner = session.Mode.Winner;
            var text = new StringBuilder(Tag).Append(" placements winner=")
                .Append(winner.HasValue ? Number(winner.Value.Id) : ModeLog.Draw);
            foreach (var placement in session.GetPlacements())
            {
                text.Append(' ').Append(Number(placement.Place)).Append(":squad=").Append(Number(placement.Squad.Id));
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

        public static string Drop(int playerId, float height, ParachuteOpenVerdict verdict)
        {
            return Tag + " drop player=" + Number(playerId)
                + " height=" + height.ToString("F2", CultureInfo.InvariantCulture)
                + " parachute=" + verdict;
        }

        public static string Redeploy(int playerId, RedeployVerdict verdict, int phaseIndex, int chargesLeft, float? seconds)
        {
            var text = Tag + " redeploy player=" + Number(playerId) + " verdict=" + verdict
                + " phase=" + Number(phaseIndex + 1) + " left=" + Number(chargesLeft);
            return seconds.HasValue ? text + " in=" + seconds.Value.ToString("F2", CultureInfo.InvariantCulture) : text;
        }

        public static string CrateOpened(int playerId, int crate, int pickups)
        {
            return Tag + " crate-opened player=" + Number(playerId) + " crate=" + Number(crate) + " pickups=" + Number(pickups);
        }

        private static string Optional(int? value)
        {
            return value.HasValue ? Number(value.Value) : "-";
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
