namespace Shooter.Runtime
{
    public static class BlockListLog
    {
        public const string Tag = "[Block]";

        public static string Problem(string key, string problem)
        {
            return Tag + " block list '" + key + "': " + problem + "; keeping the entries that could be read";
        }
    }
}
