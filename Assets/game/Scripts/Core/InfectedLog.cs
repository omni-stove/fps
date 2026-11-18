using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    public static class InfectedLog
    {
        public const string LogPrefix = ModeLog.Tag + " infected";

        public const string Nobody = "-";

        public static string Outbreak(IReadOnlyList<int> firstInfectedIds)
        {
            if (firstInfectedIds == null)
            {
                throw new ArgumentNullException(nameof(firstInfectedIds));
            }

            var text = new StringBuilder(LogPrefix).Append(" outbreak first=");
            if (firstInfectedIds.Count == 0)
            {
                return LogPrefix + " outbreak first=" + Nobody;
            }

            for (var i = 0; i < firstInfectedIds.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }

                text.Append(Number(firstInfectedIds[i]));
            }

            return text.ToString();
        }

        public static string Turn(InfectedTurn turn)
        {
            var by = turn.IsFirst ? "first" : turn.InfectorId.HasValue ? Number(turn.InfectorId.Value) : Nobody;
            return LogPrefix + " turn player=" + Number(turn.PlayerId) + " by=" + by;
        }

        public static string Equip(int playerId, string weaponName)
        {
            return LogPrefix + " equip player=" + Number(playerId) + " weapon=" + (string.IsNullOrWhiteSpace(weaponName) ? Nobody : weaponName);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
