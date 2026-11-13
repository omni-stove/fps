using System;
using System.Collections.Generic;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// 最後に選んだロードアウト (スロット順の武器の Id) を保存する文字列にする・戻す。
    /// 形は <c>{"version":1,"weapons":["id", ...]}</c>。武器の値は持たず、Id だけを持つ (値は試合の一覧から引き直す)。
    /// </summary>
    public static class LoadoutPreference
    {
        /// <summary>今の形の版。</summary>
        public const int Version = 1;

        /// <summary>
        /// weaponIds を保存する文字列にする。
        /// </summary>
        /// <exception cref="ArgumentNullException">weaponIds が null。</exception>
        /// <exception cref="ArgumentException">空・空の Id がある。</exception>
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

        /// <summary>
        /// <see cref="ToJson"/> の文字列を戻す。読めたらスロット順の Id と null の problem、
        /// 読めなければ null と理由を返す (壊れている・知らない版・Id の列が無いか空・文字列でない Id や空の Id がある)。
        /// </summary>
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
