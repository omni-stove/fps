using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>ホード (<see cref="Horde"/>) の HUD の行と <c>[Mode]</c> のログ。</summary>
    public static class HordeText
    {
        /// <summary>自分が倒れていて、戦っている波を味方が越えれば戻れるときに出す行。</summary>
        public const string ReturnsAfterWave = "波を越えると復活";

        public const string Victory = "防衛成功";
        public const string Defeat = "防衛失敗";

        /// <example><c>Wave(2, 5)</c> は <c>WAVE 2/5</c>、終わりが無ければ <c>WAVE 2</c>、始まる前は <c>WAVE -/5</c></example>
        public static string Wave(int number, int? waveCount)
        {
            var current = number > 0 ? Number(number) : "-";
            return "WAVE " + current + (waveCount.HasValue ? "/" + Number(waveCount.Value) : string.Empty);
        }

        /// <example><c>RemainingEnemies(4)</c> は <c>残りの敵 4</c></example>
        public static string RemainingEnemies(int remaining)
        {
            return "残りの敵 " + Number(Math.Max(0, remaining));
        }

        /// <summary>秒は <see cref="HudFormat.DisplayedSeconds"/> と同じく切り上げる。</summary>
        /// <example><c>NextWave(4.2f)</c> は <c>次の波まで 5 秒</c></example>
        public static string NextWave(float restSeconds)
        {
            return "次の波まで " + Number(HudFormat.DisplayedSeconds(restSeconds)) + " 秒";
        }

        /// <example><c>Currency(120)</c> は <c>通貨 120</c></example>
        public static string Currency(int currency)
        {
            return "通貨 " + Number(currency);
        }

        /// <summary>防衛物にできることの名前。</summary>
        public static string ActionName(HordeFortificationAction action)
        {
            switch (action)
            {
                case HordeFortificationAction.Build:
                    return "置く";
                case HordeFortificationAction.Repair:
                    return "直す";
                default:
                    return "-";
            }
        }

        /// <summary>
        /// 近くの防衛物の案内。払えるときはキー (空なら省く) と値段を、払えないときは理由を添える。
        /// </summary>
        /// <example>
        /// <c>Prompt("E", Repair, "Wall-North", 30, Ok)</c> は <c>[E] Wall-North を直す (30)</c>、
        /// 波の途中なら <c>Wall-North を直す (30): 波の間だけ</c>
        /// </example>
        public static string Prompt(string keyLabel, HordeFortificationAction action, string structureName, int cost, HordePurchaseVerdict verdict)
        {
            var body = Word(structureName) + " を" + ActionName(action) + " (" + Number(cost) + ")";
            switch (verdict)
            {
                case HordePurchaseVerdict.Ok:
                    return string.IsNullOrWhiteSpace(keyLabel) ? body : "[" + keyLabel.Trim() + "] " + body;
                case HordePurchaseVerdict.WaveInProgress:
                    return body + ": 波の間だけ";
                case HordePurchaseVerdict.NotEnoughCurrency:
                    return body + ": 通貨が足りない";
                default:
                    return body;
            }
        }

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
