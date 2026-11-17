using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    /// <summary>
    /// プレイヤーの成長と戦績。累計の XP・倒した数・アシストの数・倒れた数・試合数・勝った数を持つ。不変。
    /// 値を変えるのは <see cref="ProgressionLedger"/> で、新しいプロフィールを返す。<see cref="ToJson"/> と <see cref="FromJson"/> で文字列にして保存する。
    /// </summary>
    public sealed class PlayerProfile : IEquatable<PlayerProfile>
    {
        /// <summary>このコードが書き出すプロフィールの形式のバージョン。</summary>
        public const int CurrentVersion = 2;

        /// <summary><see cref="FromJson"/> が読み替えられる最も古い版。アシストの数を持たない。</summary>
        public const int OldestReadableVersion = 1;

        private const string _versionKey = "version";
        private const string _xpKey = "xp";
        private const string _killsKey = "kills";
        private const string _assistsKey = "assists";
        private const string _deathsKey = "deaths";
        private const string _matchesKey = "matches";
        private const string _winsKey = "wins";

        private static readonly string[] _version1Keys = { _versionKey, _xpKey, _killsKey, _deathsKey, _matchesKey, _winsKey };

        private static readonly string[] _keys = { _versionKey, _xpKey, _killsKey, _assistsKey, _deathsKey, _matchesKey, _winsKey };

        /// <summary>まだ 1 試合も遊んでいないプロフィール。</summary>
        public static readonly PlayerProfile Empty = new PlayerProfile(0, 0, 0, 0, 0, 0);

        /// <summary>アシストの数が 0 のプロフィール。</summary>
        /// <exception cref="ArgumentOutOfRangeException">どれかが負か、wins が matches より多い。</exception>
        public PlayerProfile(int xp, int kills, int deaths, int matches, int wins)
            : this(xp, kills, deaths, matches, wins, 0)
        {
        }

        /// <param name="xp">累計の XP。0 以上。</param>
        /// <param name="kills">倒した数。0 以上。</param>
        /// <param name="deaths">倒れた数。0 以上。</param>
        /// <param name="matches">終えた試合の数。0 以上。</param>
        /// <param name="wins">勝った試合の数。0 以上で matches 以下。</param>
        /// <param name="assists">アシストした数。0 以上。</param>
        /// <exception cref="ArgumentOutOfRangeException">どれかが負か、wins が matches より多い。</exception>
        public PlayerProfile(int xp, int kills, int deaths, int matches, int wins, int assists)
        {
            RequireNonNegative(xp, nameof(xp));
            RequireNonNegative(kills, nameof(kills));
            RequireNonNegative(deaths, nameof(deaths));
            RequireNonNegative(matches, nameof(matches));
            RequireNonNegative(wins, nameof(wins));
            RequireNonNegative(assists, nameof(assists));
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
        }

        /// <summary>プロフィールの形式のバージョン。前の版から読み替えたものも含め、常に <see cref="CurrentVersion"/>。</summary>
        public int Version { get; }

        public int Xp { get; }

        public int Kills { get; }

        public int Assists { get; }

        public int Deaths { get; }

        public int Matches { get; }

        public int Wins { get; }

        /// <summary>JSON の文字列に <see cref="CurrentVersion"/> の形で書き出す。<see cref="FromJson"/> で同じ内容に戻る。</summary>
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
            AppendInt(builder, _winsKey, Wins);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>
        /// <see cref="ToJson"/> で書き出した文字列から読む。<see cref="OldestReadableVersion"/> の版 (アシストの数が無い) は、アシスト 0 として読み替える。
        /// JSON として壊れている、その版の項目が欠けている・余分・型違い・範囲外、読めない版のときは <see cref="FormatException"/> を投げる。
        /// </summary>
        /// <exception cref="ArgumentNullException">json が null。</exception>
        /// <exception cref="FormatException">json がプロフィールとして読めない。</exception>
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

            // 版ごとに項目が違うので、バージョンは他の項目より先に確かめる。
            var version = ReadInt(root, _versionKey);
            if (version < OldestReadableVersion || version > CurrentVersion)
            {
                throw new FormatException(
                    $"unsupported profile data version {version.ToString(CultureInfo.InvariantCulture)} "
                    + $"(expected {OldestReadableVersion.ToString(CultureInfo.InvariantCulture)} to {CurrentVersion.ToString(CultureInfo.InvariantCulture)})");
            }

            var keys = version == OldestReadableVersion ? _version1Keys : _keys;
            foreach (var key in root.Keys)
            {
                if (Array.IndexOf(keys, key) < 0)
                {
                    throw new FormatException($"invalid profile data: unknown key '{key}'");
                }
            }

            var xp = ReadInt(root, _xpKey);
            var kills = ReadInt(root, _killsKey);
            var assists = version == OldestReadableVersion ? 0 : ReadInt(root, _assistsKey);
            var deaths = ReadInt(root, _deathsKey);
            var matches = ReadInt(root, _matchesKey);
            var wins = ReadInt(root, _winsKey);
            try
            {
                return new PlayerProfile(xp, kills, deaths, matches, wins, assists);
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
                && Wins == other.Wins;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Xp, Kills, Assists, Deaths, Matches, Wins);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "xp={0} kills={1} assists={2} deaths={3} matches={4} wins={5}",
                Xp, Kills, Assists, Deaths, Matches, Wins);
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
