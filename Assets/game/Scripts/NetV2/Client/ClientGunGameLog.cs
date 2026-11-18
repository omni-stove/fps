namespace Shooter.NetV2
{
    public static class ClientGunGameLog
    {
        public static string LadderNotShown(string ladderName, string reason)
        {
            return MatchClient.LogTag + " gun game ladder '" + ladderName + "' cannot be shown: " + reason;
        }

        public static string LadderMissing()
        {
            return MatchClient.LogTag + " the server plays Gun Game but GameContent has no gun game ladder; the HUD does not show the tiers";
        }

        public static string LadderMismatch()
        {
            return MatchClient.LogTag + " the server's gun game has more tiers than the local ladder; the HUD shows the last local tier";
        }
    }
}
