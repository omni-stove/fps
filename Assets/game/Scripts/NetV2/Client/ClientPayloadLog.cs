using System.Globalization;

namespace Shooter.NetV2
{
    public static class ClientPayloadLog
    {
        public static string CartMissing()
        {
            return MatchClient.LogTag + " the server plays Payload but the scene has no PayloadCart; the HUD does not show the cart";
        }

        public static string CheckpointMismatch(int checkpointCount)
        {
            return MatchClient.LogTag + " the server's payload passed more checkpoints than the local route has ("
                + checkpointCount.ToString(CultureInfo.InvariantCulture) + "); the HUD shows the last local checkpoint";
        }
    }
}
