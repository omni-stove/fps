using System.Globalization;
using Shooter.Core;

namespace Shooter.NetV2
{
    public static class ClientHordeLog
    {
        public static string FortifyRequested(HordeFortificationAction action, string nameKey, int cost, bool sent)
        {
            return MatchClient.LogTag + " horde fortify request action=" + action + " structure=" + Word(nameKey)
                + " cost=" + cost.ToString(CultureInfo.InvariantCulture) + " sent=" + (sent ? "True" : "False");
        }

        private static string Word(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }
    }
}
