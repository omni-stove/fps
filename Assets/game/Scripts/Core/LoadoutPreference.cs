using System;
using System.Collections.Generic;
using System.Text;

namespace Shooter.Core
{
    // 武器や能力の値を持たず Id だけを持つのは、値を試合の一覧から引き直すため。
    public static class LoadoutPreference
    {
        public const int Version = 2;

        // 能力を持たない前の版の保存も、武器の選択は戻せるよう読む
        public const int OldestReadableVersion = 1;

        public static string ToJson(LoadoutChoice choice)
        {
            if (choice == null)
            {
                throw new ArgumentNullException(nameof(choice));
            }

            var weaponIds = choice.WeaponIds;
            if (weaponIds.Count == 0)
            {
                throw new ArgumentException("at least one weapon id is required", nameof(choice));
            }

            var builder = new StringBuilder();
            builder.Append("{\"version\":").Append(Version).Append(",\"weapons\":[");
            for (var i = 0; i < weaponIds.Count; i++)
            {
                if (string.IsNullOrEmpty(weaponIds[i]))
                {
                    throw new ArgumentException("slot " + i + " has no weapon id", nameof(choice));
                }

                if (i > 0)
                {
                    builder.Append(',');
                }

                SaveJson.AppendString(builder, weaponIds[i]);
            }

            builder.Append(']');
            var abilityIds = choice.AbilityIds;
            if (abilityIds != null)
            {
                builder.Append(",\"abilities\":[");
                for (var i = 0; i < abilityIds.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    SaveJson.AppendString(builder, abilityIds[i]);
                }

                builder.Append(']');
            }

            builder.Append('}');
            return builder.ToString();
        }

        public static LoadoutChoice FromJson(string text, out string problem)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                problem = "the saved loadout is empty";
                return null;
            }

            object parsed;
            try
            {
                parsed = SaveJson.Parse(text);
            }
            catch (FormatException e)
            {
                problem = "the saved loadout is not valid JSON: " + e.Message;
                return null;
            }

            if (!(parsed is Dictionary<string, object> root))
            {
                problem = "the saved loadout is not a JSON object";
                return null;
            }

            if (!root.TryGetValue("version", out var version) || !(version is long number)
                || number < OldestReadableVersion || number > Version)
            {
                problem = "the saved loadout has an unknown version";
                return null;
            }

            if (!root.TryGetValue("weapons", out var weapons) || !(weapons is List<object> list) || list.Count == 0)
            {
                problem = "the saved loadout has no weapons";
                return null;
            }

            var ids = new string[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                if (!(list[i] is string id) || id.Length == 0)
                {
                    problem = "slot " + i + " of the saved loadout has no weapon id";
                    return null;
                }

                ids[i] = id;
            }

            string[] abilityIds = null;
            if (number >= 2 && root.TryGetValue("abilities", out var abilities))
            {
                if (!(abilities is List<object> abilityList))
                {
                    problem = "the saved loadout's abilities are not a list";
                    return null;
                }

                abilityIds = new string[abilityList.Count];
                for (var i = 0; i < abilityList.Count; i++)
                {
                    if (!(abilityList[i] is string abilityId))
                    {
                        problem = "ability slot " + i + " of the saved loadout is not an id";
                        return null;
                    }

                    abilityIds[i] = abilityId;
                }
            }

            problem = null;
            return new LoadoutChoice(ids, abilityIds);
        }
    }
}
