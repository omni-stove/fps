namespace Shooter.Runtime
{
    public static class VoiceSettingsLog
    {
        public const string Tag = "[Voice]";

        public static string Problem(string key, string problem)
        {
            return Tag + " voice settings '" + key + "': " + problem + "; using the default or the nearest valid value";
        }
    }
}
