namespace Shooter.Runtime
{
    public static class GyroLookLog
    {
        public const string LogTag = "[Gyro]";

        public static string NoDevice()
        {
            return $"{LogTag} gyro look is on but no gyroscope is connected; the view is turned only by the mouse and the stick";
        }

        public static string Enabled(string deviceName)
        {
            return $"{LogTag} enabled the gyroscope '{deviceName}' for gyro look";
        }

        public static string Released(string deviceName)
        {
            return $"{LogTag} disabled the gyroscope '{deviceName}'";
        }
    }
}
