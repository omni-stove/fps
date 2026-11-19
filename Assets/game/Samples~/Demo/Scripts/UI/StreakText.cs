using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public enum StreakCardState
    {
        Charging,

        Selected,

        Held,

        Spent,
    }

    public static class StreakText
    {
        public const string KillsKey = "streak.kills";

        public const string ProgressKey = "streak.progress";

        public const string UseKey = "streak.use";

        public const string ReadyKey = "streak.ready";

        public const string SpentKey = "streak.spent";

        public const string SwitchKey = "streak.switch";

        public const string SwitchWithoutKeyKey = "streak.switchNoKey";

        public const string TargetChooseKey = "streak.target.choose";

        public const string TargetMoveKey = "streak.target.move";

        public const string TargetConfirmKey = "streak.target.confirm";

        public const string TargetConfirmWithoutKeyKey = "streak.target.confirmNoKey";

        public const string TargetCancelKey = "streak.target.cancel";

        public const string TargetCancelWithoutKeyKey = "streak.target.cancelNoKey";

        public static LocalizedText Kills(StreakStatus status)
        {
            return new LocalizedText(KillsKey, status.Kills);
        }

        public static LocalizedText Name(StreakSlot slot)
        {
            return DisplayNames.Of(slot);
        }

        public static StreakCardState StateOf(StreakStatus status, StreakSlot slot)
        {
            if (slot.Ready)
            {
                return status.IsSelected(slot) ? StreakCardState.Selected : StreakCardState.Held;
            }

            return status.Kills >= slot.KillsRequired ? StreakCardState.Spent : StreakCardState.Charging;
        }

        // 使ったものを満たしたままにするのは、この命ではもう貯まらない枠を、貯め始めの空の枠と見分けるため。
        public static float Fill(StreakStatus status, StreakSlot slot)
        {
            if (StateOf(status, slot) != StreakCardState.Charging)
            {
                return 1f;
            }

            return Math.Min(1f, (float)status.Kills / slot.KillsRequired);
        }

        // 使うキーで使われるのは選んでいる報酬だけなので、キーはその枠にだけ添える。
        public static LocalizedText Caption(StreakStatus status, StreakSlot slot, string useKeyLabel)
        {
            switch (StateOf(status, slot))
            {
                case StreakCardState.Selected:
                    return WithKey(UseKey, ReadyKey, useKeyLabel);
                case StreakCardState.Held:
                    return new LocalizedText(ReadyKey);
                case StreakCardState.Spent:
                    return new LocalizedText(SpentKey);
                default:
                    return new LocalizedText(ProgressKey, status.Kills, slot.KillsRequired);
            }
        }

        public static bool ShowsSwitch(StreakStatus status)
        {
            return status.ReadyCount > 1;
        }

        public static LocalizedText Switch(string switchKeyLabel)
        {
            return WithKey(SwitchKey, SwitchWithoutKeyKey, switchKeyLabel);
        }

        public static List<LocalizedText> TargetingLines(StreakTargetingStatus status, string confirmKeyLabel, string cancelKeyLabel)
        {
            return new List<LocalizedText>
            {
                new LocalizedText(TargetChooseKey, DisplayNames.Of(status.DisplayNameKey, status.RewardId)),
                new LocalizedText(TargetMoveKey),
                WithKey(TargetConfirmKey, TargetConfirmWithoutKeyKey, confirmKeyLabel),
                WithKey(TargetCancelKey, TargetCancelWithoutKeyKey, cancelKeyLabel),
            };
        }

        private static LocalizedText WithKey(string key, string withoutKey, string keyLabel)
        {
            return string.IsNullOrWhiteSpace(keyLabel) ? new LocalizedText(withoutKey) : new LocalizedText(key, keyLabel.Trim());
        }
    }
}
