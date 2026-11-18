using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 保存先のファイルの更新時刻を使わず中身に時刻を持たせるのは、保存先を差し替えても同じ比べ方になり、
    // 消したことも (中身の無い印として) 他の端末へ伝えられるようにするため。
    public sealed class CloudSaveEnvelope
    {
        public const int CurrentVersion = 1;

        private const string _versionKey = "version";
        private const string _slotKey = "slot";
        private const string _savedAtKey = "savedAt";
        private const string _deletedKey = "deleted";
        private const string _textKey = "text";

        private static readonly string[] _keys = { _versionKey, _slotKey, _savedAtKey, _deletedKey, _textKey };

        private CloudSaveEnvelope(string slot, CloudSaveStamp stamp, string text)
        {
            if (string.IsNullOrEmpty(slot))
            {
                throw new ArgumentException("slot must not be empty", nameof(slot));
            }

            if (stamp.Deleted ? text != null : text == null)
            {
                throw new ArgumentException("a deleted slot carries no text and a saved slot carries text", nameof(text));
            }

            Slot = slot;
            Stamp = stamp;
            Text = text;
        }

        public string Slot { get; }

        public CloudSaveStamp Stamp { get; }

        public long SavedAt => Stamp.SavedAt;

        public bool Deleted => Stamp.Deleted;

        public string Text { get; }

        public static CloudSaveEnvelope Saved(string slot, long savedAt, string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new CloudSaveEnvelope(slot, new CloudSaveStamp(savedAt, false), text);
        }

        public static CloudSaveEnvelope Removed(string slot, long savedAt)
        {
            return new CloudSaveEnvelope(slot, new CloudSaveStamp(savedAt, true), null);
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"").Append(_versionKey).Append("\":").Append(CurrentVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"").Append(_slotKey).Append("\":");
            SaveJson.AppendString(builder, Slot);
            builder.Append(",\"").Append(_savedAtKey).Append("\":").Append(SavedAt.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"").Append(_deletedKey).Append("\":").Append(Deleted ? "true" : "false");
            builder.Append(",\"").Append(_textKey).Append("\":");
            if (Text == null)
            {
                builder.Append("null");
            }
            else
            {
                SaveJson.AppendString(builder, Text);
            }

            builder.Append('}');
            return builder.ToString();
        }

        public static CloudSaveEnvelope FromJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (!(SaveJson.Parse(json) is Dictionary<string, object> root))
            {
                throw new FormatException("invalid cloud save: the root must be an object");
            }

            // 新しい版のゲームが書いたものを読み違えて手元を上書きしないよう、版は他の項目より先に確かめる。
            if (!(Read(root, _versionKey) is long version) || version != CurrentVersion)
            {
                throw new FormatException(
                    "unsupported cloud save version (expected " + CurrentVersion.ToString(CultureInfo.InvariantCulture) + ")");
            }

            foreach (var key in root.Keys)
            {
                if (Array.IndexOf(_keys, key) < 0)
                {
                    throw new FormatException("invalid cloud save: unknown key '" + key + "'");
                }
            }

            if (!(Read(root, _slotKey) is string slot) || slot.Length == 0)
            {
                throw new FormatException("invalid cloud save: 'slot' must be a non-empty string");
            }

            if (!(Read(root, _savedAtKey) is long savedAt) || savedAt < 0)
            {
                throw new FormatException("invalid cloud save: 'savedAt' must be a non-negative integer");
            }

            if (!(Read(root, _deletedKey) is bool deleted))
            {
                throw new FormatException("invalid cloud save: 'deleted' must be true or false");
            }

            var text = Read(root, _textKey);
            if (deleted)
            {
                if (text != null)
                {
                    throw new FormatException("invalid cloud save: a deleted slot must have 'text' null");
                }

                return Removed(slot, savedAt);
            }

            if (!(text is string body))
            {
                throw new FormatException("invalid cloud save: 'text' must be a string");
            }

            return Saved(slot, savedAt, body);
        }

        private static object Read(Dictionary<string, object> root, string key)
        {
            if (!root.TryGetValue(key, out var value))
            {
                throw new FormatException("invalid cloud save: '" + key + "' is missing");
            }

            return value;
        }
    }
}
