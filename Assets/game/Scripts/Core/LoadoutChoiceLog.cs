using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 持ち主の送信・サーバーの判定・持ち主に届いた結果を同じ頭で出し、クライアントとサーバーのログを突き合わせられるようにする。
    public static class LoadoutChoiceLog
    {
        public const string Prefix = "[Net] loadout";

        public static string Requested(IReadOnlyList<string> weaponIds)
        {
            if (weaponIds == null)
            {
                throw new ArgumentNullException(nameof(weaponIds));
            }

            return Prefix + " request weapons=" + Join(weaponIds);
        }

        // 能力を載せない要求 (abilityIds が null) は、サーバーがロードアウトの能力を使うので、能力の欄を書かずに武器だけの行と同じにする
        public static string Requested(IReadOnlyList<string> weaponIds, IReadOnlyList<string> abilityIds)
        {
            var line = Requested(weaponIds);
            return abilityIds == null ? line : line + " abilities=" + Join(abilityIds);
        }

        public static string SavedAbilitiesNotSent(string reason)
        {
            return Prefix + " the saved abilities are not sent; the server uses the loadout's: " + (string.IsNullOrEmpty(reason) ? "-" : reason);
        }

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

        public static string Rejected(ulong clientId, string reason)
        {
            return Prefix + " rejected client=" + clientId.ToString(CultureInfo.InvariantCulture)
                + " reason=" + (string.IsNullOrEmpty(reason) ? "-" : reason);
        }

        // 武器の行と分けるのは、能力だけを断っても武器の選択は受け入れることがあるため。
        public static string AcceptedAbilities(ulong clientId, IReadOnlyList<string> abilityIds)
        {
            if (abilityIds == null)
            {
                throw new ArgumentNullException(nameof(abilityIds));
            }

            return Prefix + " abilities accepted client=" + clientId.ToString(CultureInfo.InvariantCulture)
                + " abilities=" + Join(abilityIds) + " applies=next-spawn";
        }

        public static string RejectedAbilities(ulong clientId, string reason)
        {
            return Prefix + " abilities rejected client=" + clientId.ToString(CultureInfo.InvariantCulture)
                + " reason=" + (string.IsNullOrEmpty(reason) ? "-" : reason);
        }

        public static string AbilitiesNotOffered(string reason)
        {
            return Prefix + " the loadout screen does not offer abilities: " + (string.IsNullOrEmpty(reason) ? "-" : reason);
        }

        public static string RequestedCosmetic(string cosmeticId)
        {
            return Prefix + " request cosmetic=" + (string.IsNullOrEmpty(cosmeticId) ? "-" : cosmeticId);
        }

        public static string CosmeticTooLong(string cosmeticId, int maxBytes)
        {
            return Prefix + " the cosmetic '" + cosmeticId + "' is longer than " + maxBytes.ToString(CultureInfo.InvariantCulture)
                + " bytes and is not sent";
        }

        public static string Cosmetic(ulong clientId, string cosmeticId)
        {
            return Prefix + " cosmetic client=" + clientId.ToString(CultureInfo.InvariantCulture)
                + " id=" + (string.IsNullOrEmpty(cosmeticId) ? "-" : cosmeticId);
        }

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
