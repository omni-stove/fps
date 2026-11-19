using System.Globalization;

namespace Shooter.NetV2
{
    public static class ClientStashLog
    {
        public static string SkippedStacks(int count)
        {
            return MatchClient.LogTag + " the server's stash has " + count.ToString(CultureInfo.InvariantCulture)
                + " stack(s) whose item id is not a lowercase UUID; the stash panel leaves them out";
        }
    }
}
