using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    public sealed class PlayerProfile : IEquatable<PlayerProfile>
    {
        public const int CurrentVersion = 3;

        public const int OldestReadableVersion = 1;

        private const string _versionKey = "version";
        private const string _xpKey = "xp";
        private const string _killsKey = "kills";
        private const string _assistsKey = "assists";
        private const string _deathsKey = "deaths";
        private const string _matchesKey = "matches";
        private const string _winsKey = "wins";
        private const string _ratingKey = "rating";

        private static readonly string[] _version1Keys = { _versionKey, _xpKey, _killsKey, _deathsKey, _matchesKey, _winsKey };

        private static readonly string[] _version2Keys = { _versionKey, _xpKey, _killsKey, _assistsKey, _deathsKey, _matchesKey, _winsKey };

        private static readonly string[] _keys = { _versionKey, _xpKey, _killsKey, _assistsKey, _deathsKey, _matchesKey, _winsKey, _ratingKey };

        public static readonly PlayerProfile Empty = new PlayerProfile(0, 0, 0, 0, 0, 0);

        public PlayerProfile(int xp, int kills, int deaths, int matches, int wins)
            : this(xp, kills, deaths, matches, wins, 0)
        {
        }

        public PlayerProfile(int xp, int kills, int deaths, int matches, int wins, int assists)
            : this(xp, kills, deaths, matches, wins, assists, SkillSpec.DefaultInitialRating)
        {
        }

        public PlayerProfile(int xp, int kills, int deaths, int matches, int wins, int assists, int rating)
        {
            RequireNonNegative(xp, nameof(xp));
            RequireNonNegative(kills, nameof(kills));
            RequireNonNegative(deaths, nameof(deaths));
            RequireNonNegative(matches, nameof(matches));
            RequireNonNegative(wins, nameof(wins));
            RequireNonNegative(assists, nameof(assists));
            RequireNonNegative(rating, nameof(rating));
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
            Assists = assists;
            Rating = rating;
        }

        public int Version { get; }

        public int Xp { get; }

        public int Kills { get; }

        public int Assists { get; }

        public int Deaths { get; }

        public int Matches { get; }

        public int Wins { get; }

        public int Rating { get; }

        public static PlayerProfile Started(int rating)
        {
            return new PlayerProfile(0, 0, 0, 0, 0, 0, rating);
        }

        public PlayerProfile WithRating(int rating)
        {
            return new PlayerProfile(Xp, Kills, Deaths, Matches, Wins, Assists, rating);
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendInt(builder, _versionKey, Version).Append(',');
            AppendInt(builder, _xpKey, Xp).Append(',');
            AppendInt(builder, _killsKey, Kills).Append(',');
            AppendInt(builder, _assistsKey, Assists).Append(',');
            AppendInt(builder, _deathsKey, Deaths).Append(',');
            AppendInt(builder, _matchesKey, Matches).Append(',');
            AppendInt(builder, _winsKey, Wins).Append(',');
            AppendInt(builder, _ratingKey, Rating);
            builder.Append('}');
            return builder.ToString();
        }

        public static PlayerProfile FromJson(string json)
        {
            return FromJson(json, SkillSpec.DefaultInitialRating);
        }

        // 初めの値を PlayerProfile の中で決めないのは、実力の値を持たない版 2 までの保存をゲームが調整した値から始めるため。
        public static PlayerProfile FromJson(string json, int initialRating)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            RequireNonNegative(initialRating, nameof(initialRating));
            if (!(SaveJson.Parse(json) is Dictionary<string, object> root))
            {
                throw new FormatException("invalid profile data: the root must be an object");
            }

            // 版ごとに項目が違うので、バージョンは他の項目より先に確かめる。
            var version = ReadInt(root, _versionKey);
            if (version < OldestReadableVersion || version > CurrentVersion)
            {
                throw new FormatException(
                    $"unsupported profile data version {version.ToString(CultureInfo.InvariantCulture)} "
                    + $"(expected {OldestReadableVersion.ToString(CultureInfo.InvariantCulture)} to {CurrentVersion.ToString(CultureInfo.InvariantCulture)})");
            }

            var keys = KeysOf(version);
            foreach (var key in root.Keys)
            {
                if (Array.IndexOf(keys, key) < 0)
                {
                    throw new FormatException($"invalid profile data: unknown key '{key}'");
                }
            }

            var xp = ReadInt(root, _xpKey);
            var kills = ReadInt(root, _killsKey);
            var assists = version < 2 ? 0 : ReadInt(root, _assistsKey);
            var deaths = ReadInt(root, _deathsKey);
            var matches = ReadInt(root, _matchesKey);
            var wins = ReadInt(root, _winsKey);
            var rating = version < 3 ? initialRating : ReadInt(root, _ratingKey);
            try
            {
                return new PlayerProfile(xp, kills, deaths, matches, wins, assists, rating);
            }
            catch (ArgumentException e)
            {
                // 値が範囲外などで組み立てられないのも、壊れたデータとして扱う。
                throw new FormatException("invalid profile data: " + e.Message, e);
            }
        }

        public bool Equals(PlayerProfile other)
        {
            return other != null
                && Xp == other.Xp
                && Kills == other.Kills
                && Assists == other.Assists
                && Deaths == other.Deaths
                && Matches == other.Matches
                && Wins == other.Wins
                && Rating == other.Rating;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Xp, Kills, Assists, Deaths, Matches, Wins, Rating);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "xp={0} kills={1} assists={2} deaths={3} matches={4} wins={5} rating={6}",
                Xp, Kills, Assists, Deaths, Matches, Wins, Rating);
        }

        private static string[] KeysOf(int version)
        {
            switch (version)
            {
                case 1:
                    return _version1Keys;
                case 2:
                    return _version2Keys;
                default:
                    return _keys;
            }
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
