using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ホード (<see cref="Horde"/>) の HUD の行 (表のキー <c>mode.horde.*</c> の <see cref="LocalizedText"/>) と <c>[Mode]</c> のログ。
    /// </summary>
    public static class HordeText
    {
        /// <summary>自分が倒れていて、戦っている波を味方が越えれば戻れるときに出す行。</summary>
        public static LocalizedText ReturnsAfterWave => new LocalizedText("mode.horde.returnsAfterWave");

        public static LocalizedText Victory => new LocalizedText("mode.horde.victory");
        public static LocalizedText Defeat => new LocalizedText("mode.horde.defeat");

        /// <example><c>Wave(2, 5)</c> は <c>WAVE 2/5</c>、終わりが無ければ <c>WAVE 2</c>、始まる前は <c>WAVE -/5</c></example>
        public static LocalizedText Wave(int number, int? waveCount)
        {
            var current = number > 0 ? Number(number) : "-";
            return waveCount.HasValue
                ? new LocalizedText("mode.horde.wave", current, waveCount.Value)
                : new LocalizedText("mode.horde.waveEndless", current);
        }

        /// <example><c>RemainingEnemies(4)</c> は <c>残りの敵 4</c></example>
        public static LocalizedText RemainingEnemies(int remaining)
        {
            return new LocalizedText("mode.horde.remainingEnemies", Math.Max(0, remaining));
        }

        /// <summary>秒は <see cref="HudFormat.DisplayedSeconds"/> と同じく切り上げる。</summary>
        /// <example><c>NextWave(4.2f)</c> は <c>次の波まで 5 秒</c></example>
        public static LocalizedText NextWave(float restSeconds)
        {
            return new LocalizedText("mode.horde.nextWave", HudFormat.DisplayedSeconds(restSeconds));
        }

        /// <example><c>Currency(120)</c> は <c>通貨 120</c></example>
        public static LocalizedText Currency(int currency)
        {
            return new LocalizedText("mode.horde.currency", currency);
        }

        /// <summary>防衛物にできることの名前。</summary>
        public static LocalizedText ActionName(HordeFortificationAction action)
        {
            switch (action)
            {
                case HordeFortificationAction.Build:
                    return new LocalizedText("mode.horde.build");
                case HordeFortificationAction.Repair:
                    return new LocalizedText("mode.horde.repair");
                default:
                    return LocalizedText.Literal("-");
            }
        }

        /// <summary>
        /// 近くの防衛物の案内。払えるときはキー (空なら省く) と値段を、払えないときは理由を添える。
        /// </summary>
        /// <example>
        /// <c>Prompt("E", Repair, "Wall-North", 30, Ok)</c> は <c>[E] Wall-North を直す (30)</c>、
        /// 波の途中なら <c>Wall-North を直す (30): 波の間だけ</c>
        /// </example>
        public static LocalizedText Prompt(string keyLabel, HordeFortificationAction action, string structureName, int cost, HordePurchaseVerdict verdict)
        {
            var body = new LocalizedText("mode.horde.prompt", Word(structureName), ActionName(action), cost);
            switch (verdict)
            {
                case HordePurchaseVerdict.Ok:
                    return string.IsNullOrWhiteSpace(keyLabel) ? body : new LocalizedText("mode.horde.promptKey", keyLabel.Trim(), body);
                case HordePurchaseVerdict.WaveInProgress:
                    return new LocalizedText("mode.horde.promptBetweenWaves", body);
                case HordePurchaseVerdict.NotEnoughCurrency:
                    return new LocalizedText("mode.horde.promptNotEnoughCurrency", body);
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
