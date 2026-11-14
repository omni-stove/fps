using System;

namespace Fps.Core
{
    /// <summary>ロードアウトを選ぶ画面に出す文字列。<see cref="GameText"/> から今の言語で引く。</summary>
    public static class LoadoutFormat
    {
        /// <summary>画面の見出し。</summary>
        public static string Title => GameText.Get("loadout.title");

        /// <summary>決定のボタン。</summary>
        public static string Confirm => GameText.Get("loadout.confirm");

        /// <summary>サーバーへ送って答えを待っている間の案内。</summary>
        public static string Sending => GameText.Get("loadout.sending");

        /// <summary>サーバーが受け入れたときの案内。</summary>
        public static string Accepted => GameText.Get("loadout.accepted");

        /// <summary>メニューで選んで保存したときの案内。</summary>
        public static string Saved => GameText.Get("loadout.saved");

        /// <summary>選べる武器が無いスロットに出す文字列。</summary>
        public static string NoOption => GameText.Get("loadout.noOption");

        /// <summary>slot 番 (0 から) のスロットの見出し。</summary>
        /// <example><c>SlotTitle(0)</c> は <c>スロット 1</c></example>
        public static string SlotTitle(int slot)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must not be negative");
            }

            return GameText.Format("loadout.slot", slot + 1);
        }

        /// <summary>選べない理由の案内。reason が空なら理由を付けない。</summary>
        /// <example><c>Rejected("slot 1 ...")</c> は <c>選べません: slot 1 ...</c></example>
        public static string Rejected(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? GameText.Get("loadout.rejected") : GameText.Format("loadout.rejectedWithReason", reason);
        }

        /// <summary>武器の種別の表示名。</summary>
        /// <exception cref="ArgumentOutOfRangeException">category が定義済みの値でない。</exception>
        public static string Category(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.AssaultRifle:
                case WeaponCategory.SubmachineGun:
                case WeaponCategory.Shotgun:
                case WeaponCategory.MarksmanRifle:
                case WeaponCategory.SniperRifle:
                case WeaponCategory.LightMachineGun:
                case WeaponCategory.Handgun:
                case WeaponCategory.Launcher:
                    return GameText.Get("weapon.category." + category);
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "category is not a valid WeaponCategory");
            }
        }
    }
}
