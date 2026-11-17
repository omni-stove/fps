using System.Globalization;

namespace Shooter.Core
{
    /// <summary>
    /// ホード (<see cref="Horde"/>) の <c>[Mode]</c> のログ。
    /// </summary>
    public static class HordeText
    {
        /// <example><c>[Mode] wave start number=2/5 enemies=6</c></example>
        public static string WaveStartedLog(int number, int? waveCount, int enemyCount)
        {
            return ModeLog.Tag + " wave start number=" + Number(number) + "/" + Total(waveCount) + " enemies=" + Number(enemyCount);
        }

        /// <summary>returning はこの波の間に戻る (倒れていた) 人の数。</summary>
        /// <example><c>[Mode] wave cleared number=2 currency=120 returning=1</c></example>
        public static string WaveClearedLog(int number, int currency, int returning)
        {
            return ModeLog.Tag + " wave cleared number=" + Number(number) + " currency=" + Number(currency) + " returning=" + Number(returning);
        }

        /// <example><c>[Mode] currency +12 wave=2 total=120</c></example>
        public static string EarnedLog(int waveNumber, int reward, int currency)
        {
            return ModeLog.Tag + " currency +" + Number(reward) + " wave=" + Number(waveNumber) + " total=" + Number(currency);
        }

        /// <example><c>[Mode] repair structure=Wall-North pieces=3 cost=15 currency=105</c></example>
        public static string PurchasedLog(HordeFortificationAction action, string structureName, int pieces, int cost, int currency)
        {
            return ModeLog.Tag + " " + ActionKey(action) + " structure=" + Word(structureName) + " pieces=" + Number(pieces)
                + " cost=" + Number(cost) + " currency=" + Number(currency);
        }

        /// <example><c>[Mode] purchase-denied action=build structure=Barricade-NE reason=NotEnoughCurrency cost=40 currency=10</c></example>
        public static string DeniedLog(HordeFortificationAction action, string structureName, HordePurchaseVerdict verdict, int cost, int currency)
        {
            return ModeLog.Tag + " purchase-denied action=" + ActionKey(action) + " structure=" + Word(structureName) + " reason=" + verdict
                + " cost=" + Number(cost) + " currency=" + Number(currency);
        }

        /// <summary>試合の結果。勝ちなら越えた波の数、負けなら倒された波の番号を書く。</summary>
        /// <example><c>[Mode] horde victory waves=5/5</c>、負けなら <c>[Mode] horde defeat wave=3/5</c></example>
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
