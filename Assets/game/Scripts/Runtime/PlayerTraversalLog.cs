using System.Globalization;
using Shooter.Core;

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

        public static string WallRunning(WallRunSide side)
        {
            switch (side)
            {
                case WallRunSide.Left:
                    return $"{PlayerController.LogTag} wallRun=left";
                case WallRunSide.Right:
                    return $"{PlayerController.LogTag} wallRun=right";
                default:
                    return $"{PlayerController.LogTag} wallRun=none";
            }
        }
    }
}
