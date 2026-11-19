namespace Shooter.Runtime
{
    public static class BotAircraftPilotLog
    {
        public static string InvalidSettings(string assetName, string reason)
        {
            var source = string.IsNullOrEmpty(assetName) ? "(unnamed settings)" : assetName;
            return $"{BotAircraftPilot.LogTag} bot aircraft pilot settings '{source}' are invalid: {reason}";
        }

        public static string NoWaypoints(string aircraftName)
        {
            return $"{BotAircraftPilot.LogTag} '{aircraftName}' has no waypoints; the bot flies straight ahead at its altitude";
        }

        public static string NoMotor(string aircraftName)
        {
            return $"{BotAircraftPilot.LogTag} '{aircraftName}' has no SimpleAircraftMotor; the bot cannot read how it is flying and leaves the controls idle";
        }

        public static string NoMotorLink(string aircraftName)
        {
            return $"{BotAircraftPilot.LogTag} '{aircraftName}' has no VehicleMotorLink; the bot's controls cannot reach the motor";
        }

        public static string NoDriverSeat(string aircraftName)
        {
            return $"{BotAircraftPilot.LogTag} '{aircraftName}' has no driver seat; the bot cannot fly it";
        }
    }
}
