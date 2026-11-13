using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>ロードアウトを選ぶ画面に出す文字列。</summary>
    public static class LoadoutFormat
    {
        /// <summary>画面の見出し。</summary>
        public const string Title = "ロードアウト";

        /// <summary>決定のボタン。</summary>
        public const string Confirm = "決定";

        /// <summary>サーバーへ送って答えを待っている間の案内。</summary>
        public const string Sending = "送信中…";

        /// <summary>サーバーが受け入れたときの案内。</summary>
        public const string Accepted = "次の出現からこの武器で出ます";

        /// <summary>メニューで選んで保存したときの案内。</summary>
        public const string Saved = "ロードアウトを保存しました。オンラインに入ると送ります";

        /// <summary>選べる武器が無いスロットに出す文字列。</summary>
        public const string NoOption = "選べる武器がありません";

        /// <summary>slot 番 (0 から) のスロットの見出し。</summary>
        /// <example><c>SlotTitle(0)</c> は <c>スロット 1</c></example>
        public static string SlotTitle(int slot)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must not be negative");
            }

            return "スロット " + (slot + 1).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>選べない理由の案内。reason が空なら理由を付けない。</summary>
        /// <example><c>Rejected("slot 1 ...")</c> は <c>選べません: slot 1 ...</c></example>
        public static string Rejected(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? "選べません" : "選べません: " + reason;
        }

        /// <summary>武器の種別の表示名。</summary>
        /// <exception cref="ArgumentOutOfRangeException">category が定義済みの値でない。</exception>
        public static string Category(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.AssaultRifle:
                    return "アサルトライフル";
                case WeaponCategory.SubmachineGun:
                    return "サブマシンガン";
                case WeaponCategory.Shotgun:
                    return "ショットガン";
                case WeaponCategory.MarksmanRifle:
                    return "マークスマンライフル";
                case WeaponCategory.SniperRifle:
                    return "スナイパーライフル";
                case WeaponCategory.LightMachineGun:
                    return "ライトマシンガン";
                case WeaponCategory.Handgun:
                    return "ハンドガン";
                case WeaponCategory.Launcher:
                    return "ランチャー";
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "category is not a valid WeaponCategory");
            }
        }
    }
}
