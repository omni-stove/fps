using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    public sealed class PlayerProfile : IEquatable<PlayerProfile>
    {
        public const int CurrentVersion = 1;

        private const string _versionKey = "version";
        private const string _xpKey = "xp";
        private const string _killsKey = "kills";
        private const string _deathsKey = "deaths";
        private const string _matchesKey = "matches";
        private const string _winsKey = "wins";

        private static readonly string[] _keys = { _versionKey, _xpKey, _killsKey, _deathsKey, _matchesKey, _winsKey };

        public static readonly PlayerProfile Empty = new PlayerProfile(0, 0, 0, 0, 0);

        public PlayerProfile(int xp, int kills, int deaths, int matches, int wins)
        {
            RequireNonNegative(xp, nameof(xp));
            RequireNonNegative(kills, nameof(kills));
            RequireNonNegative(deaths, nameof(deaths));
            RequireNonNegative(matches, nameof(matches));
            RequireNonNegative(wins, nameof(wins));
            if (wins > matches)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wins), wins, $"wins must not be more than matches ({matches.ToString(CultureInfo.InvariantCulture)})");
            }

            Version = CurrentVersion;
            Xp = xp;
            Kills = kills;
            Deaths = deaths;
            Matches = matches;
            Wins = wins;
        }

        public int Version { get; }

        public int Xp { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public int Matches { get; }

        public int Wins { get; }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendInt(builder, _versionKey, Version).Append(',');
            AppendInt(builder, _xpKey, Xp).Append(',');
            AppendInt(builder, _killsKey, Kills).Append(',');
            AppendInt(builder, _deathsKey, Deaths).Append(',');
            AppendInt(builder, _matchesKey, Matches).Append(',');
            AppendInt(builder, _winsKey, Wins);
            builder.Append('}');
            return builder.ToString();
        }

        public static PlayerProfile FromJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (!(SaveJson.Parse(json) is Dictionary<string, object> root))
            {
                throw new FormatException("invalid profile data: the root must be an object");
            }

            // 形式が変わっても判別できるよう、バージョンは他の項目より先に確かめる。
            var version = ReadInt(root, _versionKey);
            if (version != CurrentVersion)
            {
                throw new FormatException(
                    $"unsupported profile data version {version.ToString(CultureInfo.InvariantCulture)} (expected {CurrentVersion.ToString(CultureInfo.InvariantCulture)})");
            }

            foreach (var key in root.Keys)
            {
                if (Array.IndexOf(_keys, key) < 0)
                {
                    throw new FormatException($"invalid profile data: unknown key '{key}'");
                }
            }

            var xp = ReadInt(root, _xpKey);
            var kills = ReadInt(root, _killsKey);
            var deaths = ReadInt(root, _deathsKey);
            var matches = ReadInt(root, _matchesKey);
            var wins = ReadInt(root, _winsKey);
            try
            {
                return new PlayerProfile(xp, kills, deaths, matches, wins);
            }
            catch (ArgumentException e)
            {
                // 値が範囲外などで組み立てられないのも、壊れたデータとして扱う。
                throw new FormatException("invalid profile data: " + e.Message, e);
            }
        }

        public bool Equals(PlayerProfile other)
        {
            return other != null && Xp == other.Xp && Kills == other.Kills && Deaths == other.Deaths && Matches == other.Matches && Wins == other.Wins;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Xp, Kills, Deaths, Matches, Wins);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture, "xp={0} kills={1} deaths={2} matches={3} wins={4}", Xp, Kills, Deaths, Matches, Wins);
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
                throw new FormatException($"invalid profile data: missing '{key}'");
            }

            if (!(value is long number) || number < int.MinValue || number > int.MaxValue)
            {
                throw new FormatException($"invalid profile data: '{key}' must be a 32-bit integer");
            }

            return (int)number;
        }

        private static void RequireNonNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must not be negative");
            }
        }
    }
}
