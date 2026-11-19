using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // レートを進行のプロフィールと別に保存するのは、進行を外したゲームでもマッチングにレートを使えるようにするため。
    public sealed class SkillSave : IEquatable<SkillSave>
    {
        public const int CurrentVersion = 1;

        public const int OldestReadableVersion = 1;

        private const string _versionKey = "version";
        private const string _ratingKey = "rating";

        private static readonly string[] _keys = { _versionKey, _ratingKey };

        public SkillSave(int rating)
        {
            if (rating < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rating), rating, "rating must not be negative");
            }

            Version = CurrentVersion;
            Rating = rating;
        }

        public int Version { get; }

        public int Rating { get; }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendInt(builder, _versionKey, Version).Append(',');
            AppendInt(builder, _ratingKey, Rating);
            builder.Append('}');
            return builder.ToString();
        }

        public static SkillSave FromJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (!(SaveJson.Parse(json) is Dictionary<string, object> root))
            {
                throw new FormatException("invalid skill data: the root must be an object");
            }

            // 版ごとに項目が変わり得るので、バージョンは他の項目より先に確かめる。
            var version = ReadInt(root, _versionKey);
            if (version < OldestReadableVersion || version > CurrentVersion)
            {
                throw new FormatException(
                    $"unsupported skill data version {version.ToString(CultureInfo.InvariantCulture)} "
                    + $"(expected {OldestReadableVersion.ToString(CultureInfo.InvariantCulture)} to {CurrentVersion.ToString(CultureInfo.InvariantCulture)})");
            }

            foreach (var key in root.Keys)
            {
                if (Array.IndexOf(_keys, key) < 0)
                {
                    throw new FormatException($"invalid skill data: unknown key '{key}'");
                }
            }

            var rating = ReadInt(root, _ratingKey);
            try
            {
                return new SkillSave(rating);
            }
            catch (ArgumentException e)
            {
                // 値が範囲外で組み立てられないのも、壊れたデータとして扱う。
                throw new FormatException("invalid skill data: " + e.Message, e);
            }
        }

        public bool Equals(SkillSave other)
        {
            return other != null && Rating == other.Rating;
        }

        public override bool Equals(object obj)
        {
            return obj is SkillSave other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Rating;
        }

        public override string ToString()
        {
            return "rating=" + Rating.ToString(CultureInfo.InvariantCulture);
        }

        private static StringBuilder AppendInt(StringBuilder builder, string key, int value)
        {
            SaveJson.AppendString(builder, key);
            return builder.Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static int ReadInt(Dictionary<string, object> fields, string key)
        {
            if (!fields.TryGetValue(key, out var value))
            {
                throw new FormatException($"invalid skill data: missing '{key}'");
            }

            if (!(value is long number) || number < int.MinValue || number > int.MaxValue)
            {
                throw new FormatException($"invalid skill data: '{key}' must be a 32-bit integer");
            }

            return (int)number;
        }
    }
}
