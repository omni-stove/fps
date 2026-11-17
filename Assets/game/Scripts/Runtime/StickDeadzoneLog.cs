using System.Globalization;

namespace Shooter.Runtime
{
    public static class StickDeadzoneLog
    {
        public static string Applied(string assetName, float deadzone, int bindings)
        {
            return $"{PlayerBindings.LogTag} stick deadzone {deadzone.ToString("0.###", CultureInfo.InvariantCulture)} on {bindings} bindings of '{assetName}'";
        }

        public static string Removed(string assetName, int bindings)
        {
            return $"{PlayerBindings.LogTag} removed the stick deadzone from {bindings} bindings of '{assetName}'";
        }

        public static string NoActions()
        {
            return $"{PlayerBindings.LogTag} project-wide Input Actions are not set; the stick deadzone setting is not applied";
        }
    }
}
