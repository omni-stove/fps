using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 各スロットのセーブ本体の形式を変えずに済むよう、手元で書いた時刻はスロットごとに別の 1 つのスロットへまとめて持つ。
    public sealed class CloudSaveStamps
    {
        public const int CurrentVersion = 1;

        private const string _versionKey = "version";
        private const string _slotsKey = "slots";
        private const string _savedAtKey = "savedAt";
        private const string _deletedKey = "deleted";

        private readonly SortedDictionary<string, CloudSaveStamp> _slots =
            new SortedDictionary<string, CloudSaveStamp>(StringComparer.Ordinal);

        public IEnumerable<string> Slots => _slots.Keys;

        public int Count => _slots.Count;

        public bool TryGet(string slot, out CloudSaveStamp stamp)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            return _slots.TryGetValue(slot, out stamp);
        }

        public void Set(string slot, CloudSaveStamp stamp)
        {
            if (string.IsNullOrEmpty(slot))
            {
                throw new ArgumentException("slot must not be empty", nameof(slot));
            }

            _slots[slot] = stamp;
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"").Append(_versionKey).Append("\":").Append(CurrentVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"").Append(_slotsKey).Append("\":{");
            var first = true;
            foreach (var pair in _slots)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                SaveJson.AppendString(builder, pair.Key);
                builder.Append(":{\"").Append(_savedAtKey).Append("\":").Append(pair.Value.SavedAt.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"").Append(_deletedKey).Append("\":").Append(pair.Value.Deleted ? "true" : "false");
                builder.Append('}');
            }

            builder.Append("}}");
            return builder.ToString();
        }

        public static CloudSaveStamps FromJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (!(SaveJson.Parse(json) is Dictionary<string, object> root))
            {
                throw new FormatException("invalid cloud save stamps: the root must be an object");
            }

            if (!root.TryGetValue(_versionKey, out var versionValue) || !(versionValue is long version) || version != CurrentVersion)
            {
                throw new FormatException(
                    "unsupported cloud save stamps version (expected " + CurrentVersion.ToString(CultureInfo.InvariantCulture) + ")");
            }

            if (!root.TryGetValue(_slotsKey, out var slotsValue) || !(slotsValue is Dictionary<string, object> slots))
            {
                throw new FormatException("invalid cloud save stamps: 'slots' must be an object");
            }

            var stamps = new CloudSaveStamps();
            foreach (var pair in slots)
            {
                if (pair.Key.Length == 0
                    || !(pair.Value is Dictionary<string, object> entry)
                    || !entry.TryGetValue(_savedAtKey, out var savedAtValue) || !(savedAtValue is long savedAt) || savedAt < 0
                    || !entry.TryGetValue(_deletedKey, out var deletedValue) || !(deletedValue is bool deleted)
                    || entry.Count != 2)
                {
                    throw new FormatException("invalid cloud save stamps: slot '" + pair.Key + "' needs a non-negative 'savedAt' and 'deleted'");
                }

                stamps.Set(pair.Key, new CloudSaveStamp(savedAt, deleted));
            }

            return stamps;
        }
    }
}
