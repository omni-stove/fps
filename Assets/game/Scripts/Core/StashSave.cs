using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 持ち物の値を持たず Id と数だけを持つのは、重ねられる数などを持ち物の表から引き直すため。
    public static class StashSave
    {
        public const int Version = 1;

        public const string VersionItem = "version";
        public const string StacksItem = "stacks";
        public const string ItemIdItem = "itemId";
        public const string CountItem = "count";

        public static string ToJson(IReadOnlyList<StashEntry> stacks)
        {
            if (stacks == null)
            {
                throw new ArgumentNullException(nameof(stacks));
            }

            var builder = new StringBuilder();
            builder.Append("{\"").Append(VersionItem).Append("\":").Append(Version.ToString(CultureInfo.InvariantCulture))
                .Append(",\"").Append(StacksItem).Append("\":[");
            for (var i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i] ?? throw new ArgumentException("stacks must not contain null", nameof(stacks));
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append("{\"").Append(ItemIdItem).Append("\":");
                SaveJson.AppendString(builder, stack.ItemId);
                builder.Append(",\"").Append(CountItem).Append("\":").Append(stack.Count.ToString(CultureInfo.InvariantCulture)).Append('}');
            }

            builder.Append("]}");
            return builder.ToString();
        }

        public static IReadOnlyList<StashEntry> FromJson(string text, out string problem)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                problem = "the saved stash is empty";
                return null;
            }

            object parsed;
            try
            {
                parsed = SaveJson.Parse(text);
            }
            catch (FormatException e)
            {
                problem = "the saved stash is not valid JSON: " + e.Message;
                return null;
            }

            if (!(parsed is Dictionary<string, object> root))
            {
                problem = "the saved stash is not a JSON object";
                return null;
            }

            if (!root.TryGetValue(VersionItem, out var version) || !(version is long number) || number != Version)
            {
                problem = "the saved stash has an unknown version";
                return null;
            }

            if (!root.TryGetValue(StacksItem, out var stacks) || !(stacks is List<object> list))
            {
                problem = "the saved stash has no stacks list";
                return null;
            }

            var entries = new StashEntry[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                entries[i] = ReadStack(list[i], i, out problem);
                if (entries[i] == null)
                {
                    return null;
                }
            }

            problem = null;
            return entries;
        }

        private static StashEntry ReadStack(object value, int index, out string problem)
        {
            var at = "stack " + index.ToString(CultureInfo.InvariantCulture) + " of the saved stash";
            if (!(value is Dictionary<string, object> stack))
            {
                problem = at + " is not a JSON object";
                return null;
            }

            if (!stack.TryGetValue(ItemIdItem, out var itemId) || !(itemId is string id))
            {
                problem = at + " has no item id";
                return null;
            }

            if (!stack.TryGetValue(CountItem, out var count) || !(count is long amount) || amount < 1 || amount > int.MaxValue)
            {
                problem = at + " has no positive count";
                return null;
            }

            try
            {
                problem = null;
                return new StashEntry(id, (int)amount);
            }
            catch (ArgumentException e)
            {
                problem = at + ": " + e.Message;
                return null;
            }
        }
    }
}
