using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class DroneHudText
    {
        public const string BatteryKey = "hud.drone.battery";

        public const string OutOfRangeKey = "hud.drone.outOfRange";

        public const string RecallKey = "hud.drone.recall";

        // ドローンの届く距離の残りがこの割合を切ったら、落ちる前に戻れるよう警告する。
        public const float DefaultRangeWarningFraction = 0.2f;

        // 能力の 1 つ目の枠に置く能力を入れる前提の既定。別の枠に入れたゲームは DroneHudView.RecallActionName を替える。
        public static readonly string DefaultRecallActionName = ActionNameOf(InputActionPaths.Ability1);

        public static int BatteryPercent(DroneStatus status)
        {
            return (int)Math.Round(Math.Clamp(status.BatteryFraction, 0f, 1f) * 100f, MidpointRounding.AwayFromZero);
        }

        public static LocalizedText Battery(DroneStatus status)
        {
            return new LocalizedText(BatteryKey, BatteryPercent(status));
        }

        public static bool IsRangeWarning(DroneStatus status, float warningFraction)
        {
            return status.RangeFraction <= warningFraction;
        }

        public static LocalizedText OutOfRange()
        {
            return new LocalizedText(OutOfRangeKey);
        }

        public static LocalizedText Recall(string keyLabel)
        {
            return new LocalizedText(RecallKey, keyLabel ?? string.Empty);
        }

        // 操縦している間は体を手放していて能力を使えず、戻す押し方が効かないので、降りている間だけ案内する。
        public static bool ShowsRecall(DroneStatus status)
        {
            return !status.Piloting;
        }

        private static string ActionNameOf(string path)
        {
            var slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }
    }
}
