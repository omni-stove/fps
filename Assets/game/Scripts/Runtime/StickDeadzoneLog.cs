using System.Globalization;

namespace Shooter.Runtime
{
    public static class StickDeadzoneLog
    {
        /// <summary>例: <c>[Input] stick deadzone 0.2 on 2 bindings of 'InputSystem_Actions'</c>。</summary>
        public static string Applied(string assetName, float deadzone, int bindings)
        {
            return $"{PlayerBindings.LogTag} stick deadzone {deadzone.ToString("0.###", CultureInfo.InvariantCulture)} on {bindings} bindings of '{assetName}'";
        }

        /// <summary>例: <c>[Input] removed the stick deadzone from 2 bindings of 'InputSystem_Actions'</c>。</summary>
        public static string Removed(string assetName, int bindings)
        {
            return $"{PlayerBindings.LogTag} removed the stick deadzone from {bindings} bindings of '{assetName}'";
        }

        /// <summary>例: <c>[Input] project-wide Input Actions are not set; the stick deadzone setting is not applied</c>。</summary>
        public static string NoActions()
        {
            return $"{PlayerBindings.LogTag} project-wide Input Actions are not set; the stick deadzone setting is not applied";
        }
    }
}
