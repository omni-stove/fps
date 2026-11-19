namespace Shooter.Runtime
{
    public static class AircraftStickLog
    {
        public static string MissingThrottleAction(string referenceName)
        {
            return $"{AircraftStickControlScheme.LogTag} the throttle action reference '{referenceName}' has no action; the throttle opens while boost is held";
        }

        public static string ThrottleActionNotOneAxis(string actionName, string controlType)
        {
            return $"{AircraftStickControlScheme.LogTag} the throttle action '{actionName}' gives '{controlType}', not one axis; the throttle opens while boost is held";
        }
    }
}
