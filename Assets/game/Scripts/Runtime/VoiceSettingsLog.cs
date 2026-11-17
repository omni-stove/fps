namespace Shooter.Runtime
{
    /// <summary><see cref="VoiceSettingsStore"/> の <c>[Voice]</c> 付きの警告の文言。</summary>
    public static class VoiceSettingsLog
    {
        public const string Tag = "[Voice]";

        /// <example><c>[Voice] voice settings 'Shooter.VoiceSettings': 'volumePercent' 500 is out of range (0 to 200); using the default or the nearest valid value</c></example>
        public static string Problem(string key, string problem)
        {
            return Tag + " voice settings '" + key + "': " + problem + "; using the default or the nearest valid value";
        }
    }
}
