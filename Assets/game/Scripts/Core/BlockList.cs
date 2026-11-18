using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 試合の参加者 Id は試合ごとに振り直されるので、試合をまたいで覚えるのはログインの Id (EOS の ProductUserId) にする。
    // ブロックはチャットを、ミュートは声を止めるもので、どちらか片方だけを選べるよう別々に持つ。
    public sealed class BlockList
    {
        public const int CurrentVersion = 1;

        private const string _versionKey = "version";
        private const string _blockedKey = "blocked";
        private const string _mutedKey = "muted";

        private static readonly string[] _keys = { _versionKey, _blockedKey, _mutedKey };

        // ボイスの名簿 (VoiceRoster) が大文字・小文字を区別せずに引くので、同じ人を 2 人と数えないよう揃える。
        private readonly HashSet<string> _blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _muted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event Action Changed;

        public int BlockedCount => _blocked.Count;

        public int MutedCount => _muted.Count;

        public IReadOnlyList<string> Blocked => Sorted(_blocked);

        public IReadOnlyList<string> Muted => Sorted(_muted);

        public bool IsBlocked(string userId)
        {
            return IsValidUserId(userId) && _blocked.Contains(userId);
        }

        public bool IsMuted(string userId)
        {
            return IsValidUserId(userId) && _muted.Contains(userId);
        }

        public bool SetBlocked(string userId, bool blocked)
        {
            return Set(_blocked, userId, blocked);
        }

        public bool SetMuted(string userId, bool muted)
        {
            return Set(_muted, userId, muted);
        }

        public static bool IsValidUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            foreach (var c in userId)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    return false;
                }
            }

            return true;
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            SaveJson.AppendString(builder, _versionKey);
            builder.Append(':').Append(CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append(',');
            AppendArray(builder, _blockedKey, Blocked);
            builder.Append(',');
            AppendArray(builder, _mutedKey, Muted);
            builder.Append('}');
            return builder.ToString();
        }

        public static BlockList FromJson(string json, out IReadOnlyList<string> problems)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var found = new List<string>();
            problems = new ReadOnlyCollection<string>(found);
            var list = new BlockList();
            object parsed;
            try
            {
                parsed = SaveJson.Parse(json);
            }
            catch (FormatException e)
            {
                found.Add("not valid JSON: " + e.Message);
                return list;
            }

            if (!(parsed is Dictionary<string, object> root))
            {
                found.Add("the root must be an object");
                return list;
            }

            // 形式が変わったときに古い解釈で読み違えないよう、読めるバージョンでなければ中身は見ない
            if (!root.TryGetValue(_versionKey, out var versionValue) || !(versionValue is long version)
                || version < 1 || version > CurrentVersion)
            {
                found.Add("unsupported version (expected 1 to " + CurrentVersion.ToString(CultureInfo.InvariantCulture) + ")");
                return list;
            }

            foreach (var key in root.Keys)
            {
                if (Array.IndexOf(_keys, key) < 0)
                {
                    found.Add("ignored unknown key '" + key + "'");
                }
            }

            ReadArray(root, _blockedKey, list._blocked, found);
            ReadArray(root, _mutedKey, list._muted, found);
            return list;
        }

        private bool Set(HashSet<string> set, string userId, bool on)
        {
            if (!IsValidUserId(userId))
            {
                throw new ArgumentException("a user id must be non-empty and have no spaces or control characters", nameof(userId));
            }

            var changed = on ? set.Add(userId) : set.Remove(userId);
            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }

        private static void ReadArray(Dictionary<string, object> root, string key, HashSet<string> into, List<string> found)
        {
            if (!root.TryGetValue(key, out var value))
            {
                return;
            }

            if (!(value is List<object> items))
            {
                found.Add("'" + key + "' must be an array of user ids");
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var index = i.ToString(CultureInfo.InvariantCulture);
                if (!(items[i] is string userId) || !IsValidUserId(userId))
                {
                    found.Add("'" + key + "'[" + index + "] is not a user id; skipped");
                    continue;
                }

                if (!into.Add(userId))
                {
                    found.Add("'" + key + "'[" + index + "] repeats '" + userId + "'; skipped");
                }
            }
        }

        private static void AppendArray(StringBuilder builder, string key, IReadOnlyList<string> values)
        {
            SaveJson.AppendString(builder, key);
            builder.Append(":[");
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                SaveJson.AppendString(builder, values[i]);
            }

            builder.Append(']');
        }

        // 保存した文が並びの順で変わらないよう、決まった順にする。
        private static ReadOnlyCollection<string> Sorted(HashSet<string> set)
        {
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(list);
        }
    }
}
