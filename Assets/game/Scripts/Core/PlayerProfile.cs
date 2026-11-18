using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    public sealed class PlayerProfile : IEquatable<PlayerProfile>
    {
        public const int CurrentVersion = 4;

        public const int OldestReadableVersion = 1;

        private const string _versionKey = "version";
        private const string _xpKey = "xp";
        private const string _killsKey = "kills";
        private const string _assistsKey = "assists";
        private const string _deathsKey = "deaths";
        private const string _matchesKey = "matches";
        private const string _winsKey = "wins";
        private const string _ratingKey = "rating";
        private const string _headshotsKey = "headshots";
        private const string _meleeKillsKey = "meleeKills";
        private const string _playSecondsKey = "playSeconds";
        private const string _achievementsKey = "achievements";

        private static readonly string[] _version1Keys = { _versionKey, _xpKey, _killsKey, _deathsKey, _matchesKey, _winsKey };

        private static readonly string[] _version2Keys = { _versionKey, _xpKey, _killsKey, _assistsKey, _deathsKey, _matchesKey, _winsKey };

        private static readonly string[] _version3Keys = { _versionKey, _xpKey, _killsKey, _assistsKey, _deathsKey, _matchesKey, _winsKey, _ratingKey };

        private static readonly string[] _keys =
        {
            _versionKey, _xpKey, _killsKey, _assistsKey, _deathsKey, _matchesKey, _winsKey, _ratingKey,
            _headshotsKey, _meleeKillsKey, _playSecondsKey, _achievementsKey,
        };

        private static readonly string[] _noAchievements = Array.Empty<string>();

        public static readonly PlayerProfile Empty = new PlayerProfile(0, 0, 0, 0, 0, 0);

        private readonly string[] _achievements;

        public PlayerProfile(int xp, int kills, int deaths, int matches, int wins)
            : this(xp, kills, deaths, matches, wins, 0)
        {
        }

        public PlayerProfile(int xp, int kills, int deaths, int matches, int wins, int assists)
            : this(xp, kills, deaths, matches, wins, assists, SkillSpec.DefaultInitialRating)
        {
        }

        public PlayerProfile(int xp, int kills, int deaths, int matches, int wins, int assists, int rating)
            : this(xp, kills, deaths, matches, wins, assists, rating, 0, 0, 0, null)
        {
        }

        public PlayerProfile(
            int xp,
            int kills,
            int deaths,
            int matches,
            int wins,
            int assists,
            int rating,
            int headshots,
            int meleeKills,
            int playSeconds,
            IReadOnlyList<string> achievements)
        {
            RequireNonNegative(xp, nameof(xp));
            RequireNonNegative(kills, nameof(kills));
            RequireNonNegative(deaths, nameof(deaths));
            RequireNonNegative(matches, nameof(matches));
            RequireNonNegative(wins, nameof(wins));
            RequireNonNegative(assists, nameof(assists));
            RequireNonNegative(rating, nameof(rating));
            RequireNonNegative(headshots, nameof(headshots));
            RequireNonNegative(meleeKills, nameof(meleeKills));
            RequireNonNegative(playSeconds, nameof(playSeconds));
            if (wins > matches)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wins), wins, $"wins must not be more than matches ({matches.ToString(CultureInfo.InvariantCulture)})");
            }

            // 頭・近接の数をキル以下に縛らないのは、キルと内訳を別の記録係が同じ知らせから数え、どちらが先に数えるかが購読の順で決まるため。
            Version = CurrentVersion;
            Xp = xp;
            Kills = kills;
            Deaths = deaths;
            Matches = matches;
            Wins = wins;
            Assists = assists;
            Rating = rating;
            Headshots = headshots;
            MeleeKills = meleeKills;
            PlaySeconds = playSeconds;
            _achievements = CopyAchievements(achievements);
        }

        public int Version { get; }

        public int Xp { get; }

        public int Kills { get; }

        public int Assists { get; }

        public int Deaths { get; }

        public int Matches { get; }

        public int Wins { get; }

        public int Rating { get; }

        public int Headshots { get; }

        public int MeleeKills { get; }

        public int PlaySeconds { get; }

        // 解いた順に持つのは、統計の画面で新しく解いたものを後ろに並べるため。
        public IReadOnlyList<string> Achievements => _achievements;

        public static PlayerProfile Started(int rating)
        {
            return new PlayerProfile(0, 0, 0, 0, 0, 0, rating);
        }

        public bool HasAchievement(string id)
        {
            return id != null && Array.IndexOf(_achievements, id) >= 0;
        }

        public PlayerProfile WithRating(int rating)
        {
            return new PlayerProfile(Xp, Kills, Deaths, Matches, Wins, Assists, rating, Headshots, MeleeKills, PlaySeconds, _achievements);
        }

        // ProgressionLedger が数える項目だけを差し替え、実績の記録係が数える項目は持ち越す。
        public PlayerProfile WithRecord(int xp, int kills, int deaths, int matches, int wins, int assists)
        {
            return new PlayerProfile(xp, kills, deaths, matches, wins, assists, Rating, Headshots, MeleeKills, PlaySeconds, _achievements);
        }

        public PlayerProfile WithStats(int headshots, int meleeKills, int playSeconds)
        {
            return new PlayerProfile(Xp, Kills, Deaths, Matches, Wins, Assists, Rating, headshots, meleeKills, playSeconds, _achievements);
        }

        public PlayerProfile WithAchievement(string id)
        {
            var trimmed = RequireId(id, nameof(id));
            if (HasAchievement(trimmed))
            {
                return this;
            }

            var achievements = new string[_achievements.Length + 1];
            Array.Copy(_achievements, achievements, _achievements.Length);
            achievements[_achievements.Length] = trimmed;
            return new PlayerProfile(Xp, Kills, Deaths, Matches, Wins, Assists, Rating, Headshots, MeleeKills, PlaySeconds, achievements);
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
            AppendInt(builder, _ratingKey, Rating).Append(',');
            AppendInt(builder, _headshotsKey, Headshots).Append(',');
            AppendInt(builder, _meleeKillsKey, MeleeKills).Append(',');
            AppendInt(builder, _playSecondsKey, PlaySeconds).Append(',');
            SaveJson.AppendString(builder, _achievementsKey);
            builder.Append(":[");
            for (var i = 0; i < _achievements.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                SaveJson.AppendString(builder, _achievements[i]);
            }

            builder.Append("]}");
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

            // 版 3 までは内訳と遊んだ時間を数えていないので 0 から始め、実績は次に条件を確かめたときに今の数から解き直す。
            var headshots = version < 4 ? 0 : ReadInt(root, _headshotsKey);
            var meleeKills = version < 4 ? 0 : ReadInt(root, _meleeKillsKey);
            var playSeconds = version < 4 ? 0 : ReadInt(root, _playSecondsKey);
            var achievements = version < 4 ? null : ReadIds(root, _achievementsKey);
            try
            {
                return new PlayerProfile(xp, kills, deaths, matches, wins, assists, rating, headshots, meleeKills, playSeconds, achievements);
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
                && Rating == other.Rating
                && Headshots == other.Headshots
                && MeleeKills == other.MeleeKills
                && PlaySeconds == other.PlaySeconds
                && SameAchievements(_achievements, other._achievements);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            var achievements = new HashCode();
            foreach (var id in _achievements)
            {
                achievements.Add(id, StringComparer.Ordinal);
            }

            return HashCode.Combine(
                HashCode.Combine(Xp, Kills, Assists, Deaths, Matches, Wins, Rating),
                HashCode.Combine(Headshots, MeleeKills, PlaySeconds, achievements.ToHashCode()));
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "xp={0} kills={1} assists={2} deaths={3} matches={4} wins={5} rating={6} headshots={7} meleeKills={8} playSeconds={9} achievements={10}",
                Xp, Kills, Assists, Deaths, Matches, Wins, Rating, Headshots, MeleeKills, PlaySeconds, string.Join(",", _achievements));
        }

        private static string[] KeysOf(int version)
        {
            switch (version)
            {
                case 1:
                    return _version1Keys;
                case 2:
                    return _version2Keys;
                case 3:
                    return _version3Keys;
                default:
                    return _keys;
            }
        }

        private static string[] CopyAchievements(IReadOnlyList<string> achievements)
        {
            if (achievements == null || achievements.Count == 0)
            {
                return _noAchievements;
            }

            var copy = new string[achievements.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                var id = RequireId(achievements[i], nameof(achievements));
                if (Array.IndexOf(copy, id, 0, i) >= 0)
                {
                    throw new ArgumentException("achievements contains the id '" + id + "' twice", nameof(achievements));
                }

                copy[i] = id;
            }

            return copy;
        }

        private static string RequireId(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(name + " must not contain an empty id", name);
            }

            return id.Trim();
        }

        private static bool SameAchievements(string[] a, string[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (var i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
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

        private static List<string> ReadIds(Dictionary<string, object> fields, string key)
        {
            if (!fields.TryGetValue(key, out var value))
            {
                throw new FormatException($"invalid profile data: missing '{key}'");
            }

            if (!(value is List<object> items))
            {
                throw new FormatException($"invalid profile data: '{key}' must be an array");
            }

            var ids = new List<string>(items.Count);
            foreach (var item in items)
            {
                if (!(item is string id))
                {
                    throw new FormatException($"invalid profile data: '{key}' must hold only strings");
                }

                ids.Add(id);
            }

            return ids;
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
