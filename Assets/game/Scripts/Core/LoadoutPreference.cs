using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 武器や能力の値を持たず Id だけを持つのは、値を試合の一覧から引き直すため。
    public static class LoadoutPreference
    {
        public const int Version = 3;

        // 能力を持たない前の版の保存も、武器の選択は戻せるよう読む
        public const int OldestReadableVersion = 1;

        // 1 組だけを保存していた最後の版。ここまでの保存は、名前の無い最初の組として読む
        public const int LastSingleLoadoutVersion = 2;

        private const int _firstAbilityVersion = 2;

        public static string ToJson(LoadoutPresetBook book)
        {
            if (book == null)
            {
                throw new ArgumentNullException(nameof(book));
            }

            var builder = new StringBuilder();
            builder.Append("{\"version\":").Append(Version.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"active\":").Append(Math.Max(book.ActiveIndex, 0).ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"presets\":[");
            for (var i = 0; i < book.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var preset = book[i];
                builder.Append("{\"name\":");
                SaveJson.AppendString(builder, preset.Name);
                builder.Append(',');
                AppendChoice(builder, preset.Choice);
                builder.Append('}');
            }

            builder.Append("]}");
            return builder.ToString();
        }

        public static LoadoutPresetBook FromJson(string text, LoadoutPresetSpec spec, out string problem)
        {
            return FromJson(text, spec, out problem, out _);
        }

        // 上限を下げたデータで前の保存を読むと組が上限を超えるので、先頭から上限までを残し、落とした数を dropped で返す
        public static LoadoutPresetBook FromJson(string text, LoadoutPresetSpec spec, out string problem, out int dropped)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            dropped = 0;
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

            var book = new LoadoutPresetBook(spec);
            if (number <= LastSingleLoadoutVersion)
            {
                var single = ReadChoice(root, number, string.Empty, out problem);
                if (single == null)
                {
                    return null;
                }

                book.Add(string.Empty, single);
                return book;
            }

            return ReadPresets(root, book, out problem, out dropped);
        }

        private static LoadoutPresetBook ReadPresets(Dictionary<string, object> root, LoadoutPresetBook book, out string problem, out int dropped)
        {
            dropped = 0;
            if (!root.TryGetValue("presets", out var presets) || !(presets is List<object> list))
            {
                problem = "the saved loadout has no list of presets";
                return null;
            }

            var active = 0L;
            if (root.TryGetValue("active", out var activeValue))
            {
                if (!(activeValue is long activeNumber) || (list.Count > 0 && (activeNumber < 0 || activeNumber >= list.Count)))
                {
                    problem = "the saved loadout's active preset is not one of its presets";
                    return null;
                }

                active = activeNumber;
            }

            var kept = Math.Min(list.Count, book.MaxPresets);
            for (var i = 0; i < kept; i++)
            {
                var label = "preset " + i.ToString(CultureInfo.InvariantCulture) + ": ";
                if (!(list[i] is Dictionary<string, object> entry))
                {
                    problem = label + "it is not a JSON object";
                    return null;
                }

                var name = string.Empty;
                if (entry.TryGetValue("name", out var nameValue))
                {
                    if (!(nameValue is string text))
                    {
                        problem = label + "its name is not text";
                        return null;
                    }

                    // 名前の上限を下げたデータで前の保存を読んでも、組ごと捨てずに名前を上限で切って残す
                    name = text.Trim();
                    if (name.Length > book.Spec.MaxNameLength)
                    {
                        name = name.Substring(0, book.Spec.MaxNameLength).TrimEnd();
                    }
                }

                var choice = ReadChoice(entry, Version, label, out problem);
                if (choice == null)
                {
                    return null;
                }

                try
                {
                    book.Add(name, choice);
                }
                catch (ArgumentException e)
                {
                    problem = label + e.Message;
                    return null;
                }
            }

            if (kept > 0)
            {
                book.Select(active < kept ? (int)active : 0);
            }

            dropped = list.Count - kept;
            problem = null;
            return book;
        }

        private static LoadoutChoice ReadChoice(Dictionary<string, object> root, long version, string label, out string problem)
        {
            if (!root.TryGetValue("weapons", out var weapons) || !(weapons is List<object> list) || list.Count == 0)
            {
                problem = label + "the saved loadout has no weapons";
                return null;
            }

            var ids = new string[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                if (!(list[i] is string id) || id.Length == 0)
                {
                    problem = label + "slot " + i + " of the saved loadout has no weapon id";
                    return null;
                }

                ids[i] = id;
            }

            string[] abilityIds = null;
            if (version >= _firstAbilityVersion && root.TryGetValue("abilities", out var abilities))
            {
                if (!(abilities is List<object> abilityList))
                {
                    problem = label + "the saved loadout's abilities are not a list";
                    return null;
                }

                abilityIds = new string[abilityList.Count];
                for (var i = 0; i < abilityList.Count; i++)
                {
                    if (!(abilityList[i] is string abilityId))
                    {
                        problem = label + "ability slot " + i + " of the saved loadout is not an id";
                        return null;
                    }

                    abilityIds[i] = abilityId;
                }
            }

            problem = null;
            return new LoadoutChoice(ids, abilityIds);
        }

        private static void AppendChoice(StringBuilder builder, LoadoutChoice choice)
        {
            var weaponIds = LoadoutPresetBook.RequireSavable(choice).WeaponIds;
            builder.Append("\"weapons\":[");
            for (var i = 0; i < weaponIds.Count; i++)
            {
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
        }
    }
}
