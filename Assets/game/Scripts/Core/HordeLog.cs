using System.Globalization;

namespace Shooter.Core
{
    public static class HordeLog
    {
        public static string WaveStartedLog(int number, int? waveCount, int enemyCount)
        {
            return ModeLog.Tag + " wave start number=" + Number(number) + "/" + Total(waveCount) + " enemies=" + Number(enemyCount);
        }

        public static string WaveClearedLog(int number, int currency, int returning)
        {
            return ModeLog.Tag + " wave cleared number=" + Number(number) + " currency=" + Number(currency) + " returning=" + Number(returning);
        }

        public static string EarnedLog(int waveNumber, int reward, int currency)
        {
            return ModeLog.Tag + " currency +" + Number(reward) + " wave=" + Number(waveNumber) + " total=" + Number(currency);
        }

        public static string PurchasedLog(HordeFortificationAction action, string structureName, int pieces, int cost, int currency)
        {
            return ModeLog.Tag + " " + ActionKey(action) + " structure=" + Word(structureName) + " pieces=" + Number(pieces)
                + " cost=" + Number(cost) + " currency=" + Number(currency);
        }

        public static string DeniedLog(HordeFortificationAction action, string structureName, HordePurchaseVerdict verdict, int cost, int currency)
        {
            return ModeLog.Tag + " purchase-denied action=" + ActionKey(action) + " structure=" + Word(structureName) + " reason=" + verdict
                + " cost=" + Number(cost) + " currency=" + Number(currency);
        }

        public static string ResultLog(bool victory, int waveNumber, int? waveCount)
        {
            return ModeLog.Tag + " horde " + (victory ? "victory waves=" : "defeat wave=") + Number(waveNumber) + "/" + Total(waveCount);
        }

        private static string ActionKey(HordeFortificationAction action)
        {
            switch (action)
            {
                case HordeFortificationAction.Build:
                    return "build";
                case HordeFortificationAction.Repair:
                    return "repair";
                default:
                    return "none";
            }
        }

        private static string Total(int? waveCount) => waveCount.HasValue ? Number(waveCount.Value) : WaveLog.Endless;

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        // 空白を含む値はログを key=value で読む側を壊すので詰める。
        private static string Word(string value) => string.IsNullOrEmpty(value) ? "-" : value.Replace(' ', '_');
    }
}
