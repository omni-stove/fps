using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// HUD のストリークの欄 (<see cref="StreakView"/>) に出す文。今の数・次の報酬までの数・使える報酬ごとの使い方を、上から 1 行ずつにする。
    /// 語は表 (Resources/Localization/&lt;言語&gt;/streaks.json) のキーで持ち、報酬の名前は枠の <see cref="StreakSlot.DisplayNameKey"/> から引く (<see cref="DisplayNames.Of(StreakSlot)"/>)。
    /// </summary>
    public static class StreakText
    {
        /// <summary>今の数の行。{0} が倒れずに倒した数。</summary>
        public const string KillsKey = "streak.kills";

        /// <summary>次の報酬までの行。{0} があと何体か、{1} が報酬の名前。</summary>
        public const string NextKey = "streak.next";

        /// <summary>使える報酬の行。{0} が使うキーの表記、{1} が報酬の名前。</summary>
        public const string ReadyKey = "streak.ready";

        /// <summary>今の入力機器に使うキーが無いときの、使える報酬の行。{0} が報酬の名前。</summary>
        public const string ReadyWithoutKeyKey = "streak.readyNoKey";

        public static LocalizedText Kills(StreakStatus status)
        {
            return new LocalizedText(KillsKey, status.Kills);
        }

        /// <summary>
        /// まだ届いていない枠のうち、いちばん少ない数で届く枠。同じ数の枠が並ぶなら先の枠。
        /// 使えない枠でも、もう数に届いているもの (使い終えた枠) は数えない。
        /// </summary>
        /// <returns>届いていない枠が無ければ false。</returns>
        public static bool TryFindNext(StreakStatus status, out StreakSlot next)
        {
            next = default;
            var found = false;
            foreach (var slot in status.Slots)
            {
                if (slot.Ready || slot.KillsRequired <= status.Kills)
                {
                    continue;
                }

                if (!found || slot.KillsRequired < next.KillsRequired)
                {
                    next = slot;
                    found = true;
                }
            }

            return found;
        }

        public static LocalizedText Next(StreakStatus status, StreakSlot next)
        {
            return new LocalizedText(NextKey, next.KillsRequired - status.Kills, DisplayNames.Of(next));
        }

        /// <summary>slot を使う行。keyLabel が空なら、キーを書かない行 (<see cref="ReadyWithoutKeyKey"/>) にする。</summary>
        public static LocalizedText Ready(StreakSlot slot, string keyLabel)
        {
            var name = DisplayNames.Of(slot);
            return string.IsNullOrWhiteSpace(keyLabel)
                ? new LocalizedText(ReadyWithoutKeyKey, name)
                : new LocalizedText(ReadyKey, keyLabel.Trim(), name);
        }

        /// <summary>欄の行。今の数、(届いていない枠があれば) 次の報酬まで、使える枠ごとの行を枠の並びで続ける。</summary>
        public static List<LocalizedText> Lines(StreakStatus status, string keyLabel)
        {
            var lines = new List<LocalizedText> { Kills(status) };
            if (TryFindNext(status, out var next))
            {
                lines.Add(Next(status, next));
            }

            foreach (var slot in status.Slots)
            {
                if (slot.Ready)
                {
                    lines.Add(Ready(slot, keyLabel));
                }
            }

            return lines;
        }
    }
}
