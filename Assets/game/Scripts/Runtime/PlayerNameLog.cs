namespace Shooter.Runtime
{
    public static class PlayerNameLog
    {
        public const string Tag = "[Name]";

        public static string Rejected(string key, int maxLength)
        {
            return Tag + " saved name '" + key + "' is not a name of 1 to " + maxLength
                + " characters without control or line-break characters; playing without a name";
        }
    }
}
