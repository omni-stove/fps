using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// ネット対戦でプレイヤーが自分のロードアウトを選んだときの <c>[Net] loadout</c> 付きログの文言。
    /// 持ち主の送信・サーバーの判定・持ち主に届いた結果を同じ頭で出し、クライアントとサーバーのログを突き合わせられるようにする。
    /// </summary>
    public static class LoadoutChoiceLog
    {
        public const string Prefix = "[Net] loadout";

        /// <summary>持ち主が送った武器の Id の並び。</summary>
        /// <example><c>[Net] loadout request weapons=6c1f...,b4e2...</c></example>
        public static string Requested(IReadOnlyList<string> weaponIds)
        {
            if (weaponIds == null)
            {
                throw new ArgumentNullException(nameof(weaponIds));
            }

            return Prefix + " request weapons=" + Join(weaponIds);
        }

        /// <summary>サーバーが受け入れ、clientId の次の出現から配る武器 (スロット順)。</summary>
        /// <example><c>[Net] loadout accepted client=1 weapons=6c1f...,b4e2... applies=next-spawn</c></example>
        public static string Accepted(ulong clientId, IReadOnlyList<WeaponSpec> weapons)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            var ids = new List<string>(weapons.Count);
            foreach (var weapon in weapons)
            {
                ids.Add(weapon != null ? weapon.Id : "-");
            }

            return Prefix + " accepted client=" + clientId.ToString(CultureInfo.InvariantCulture)
                + " weapons=" + Join(ids) + " applies=next-spawn";
        }

        /// <summary>サーバーが断った理由。武器は前のまま変えない。</summary>
        /// <example><c>[Net] loadout rejected client=1 reason=slot 0 does not accept Handgun (weapon 'Handgun')</c></example>
        public static string Rejected(ulong clientId, string reason)
        {
            return Prefix + " rejected client=" + clientId.ToString(CultureInfo.InvariantCulture)
                + " reason=" + (string.IsNullOrEmpty(reason) ? "-" : reason);
        }

        /// <summary>持ち主に届いた結果。受け入れなら <c>accepted</c>、断られたら <c>rejected reason=..</c>。</summary>
        /// <example><c>[Net] loadout answer rejected reason=...</c></example>
        public static string Answer(bool accepted, string reason)
        {
            return accepted
                ? Prefix + " answer accepted applies=next-spawn"
                : Prefix + " answer rejected reason=" + (string.IsNullOrEmpty(reason) ? "-" : reason);
        }

        private static string Join(IReadOnlyList<string> values)
        {
            if (values.Count == 0)
            {
                return "-";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(string.IsNullOrEmpty(values[i]) ? "-" : values[i]);
            }

            return builder.ToString();
        }
    }
}
