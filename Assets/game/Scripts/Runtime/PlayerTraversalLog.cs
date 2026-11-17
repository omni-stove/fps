using System.Globalization;

namespace Shooter.Runtime
{
    public static class PlayerTraversalLog
    {
        public static string Swimming(bool swimming, float surfaceY)
        {
            return swimming
                ? $"{PlayerController.LogTag} swimming=true surface={surfaceY.ToString("F2", CultureInfo.InvariantCulture)}"
                : $"{PlayerController.LogTag} swimming=false";
        }
    }
}
