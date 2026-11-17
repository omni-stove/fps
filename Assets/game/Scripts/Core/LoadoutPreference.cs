using System;
using System.Collections.Generic;
using System.Text;

namespace Shooter.Core
{
    // 武器の値を持たず Id だけを持つのは、値を試合の一覧から引き直すため。
    public static class LoadoutPreference
    {
        public const int Version = 1;

        public static string ToJson(IReadOnlyList<string> weaponIds)
        {
            if (weaponIds == null)
            {
                throw new ArgumentNullException(nameof(weaponIds));
            }

            if (weaponIds.Count == 0)
            {
                throw new ArgumentException("at least one weapon id is required", nameof(weaponIds));
            }

            var builder = new StringBuilder();
            builder.Append("{\"version\":").Append(Version).Append(",\"weapons\":[");
            for (var i = 0; i < weaponIds.Count; i++)
            {
                if (string.IsNullOrEmpty(weaponIds[i]))
                {
                    throw new ArgumentException("slot " + i + " has no weapon id", nameof(weaponIds));
                }

                if (i > 0)
                {
                    builder.Append(',');
                }

                SaveJson.AppendString(builder, weaponIds[i]);
            }

            builder.Append("]}");
            return builder.ToString();
        }

        public static IReadOnlyList<string> FromJson(string text, out string problem)
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

            if (!root.TryGetValue("version", out var version) || !(version is long number) || number != Version)
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

            problem = null;
            return ids;
        }
    }
}
