using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// サーバーの起動・終了と試合のイベントを、運用のために機械で読める 1 行の JSON (<see cref="FlatJsonObject.Format"/>) にする。
    /// サーバーを <c>-log-format json</c> (<see cref="ServerLogFormat.Json"/>) で起動したときだけ、標準出力に 1 イベント 1 行で出す。
    /// </summary>
    /// <remarks>
    /// どの行も <c>type</c> と <c>time</c> (UTC の ISO 8601、ミリ秒まで) で始まり、試合のイベントには <c>matchId</c> が続く。
    /// 値はすべて文字列 (数も <c>"3"</c>)。プレイヤーは <c>playerId</c> (サーバーのクライアント Id) で表し、
    /// EOS でつないだ人には <c>productUserId</c> も付ける。表示名などの人の名前は出さない。
    /// 項目の一覧は docs/dedicated-server.md にある。
    /// </remarks>
    public static class ServerEventLog
    {
        public const string ServerStartedType = "server_started";
        public const string ServerStoppingType = "server_stopping";
        public const string MatchStartedType = "match_started";
        public const string PlayerJoinedType = "player_joined";
        public const string PlayerLeftType = "player_left";
        public const string KillType = "kill";
        public const string MatchEndedType = "match_ended";

        /// <summary>試合を見分ける新しい Id (32 桁の小文字の 16 進数)。次の試合を始めるたびに作り直す。</summary>
        public static string NewMatchId()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>待ち受けを始めた。</summary>
        /// <example><c>{"type":"server_started","time":"2026-11-13T09:00:00.000Z","version":"0.2.0","transport":"udp","endpoint":"0.0.0.0:7777"}</c></example>
        public static string ServerStarted(DateTime utcNow, string version, NetTransportKind transport, string endpoint)
        {
            var values = Begin(ServerStartedType, utcNow);
            values.Add(Pair("version", RequireText(version, nameof(version))));
            values.Add(Pair("transport", NetLaunchOptions.TransportName(transport)));
            values.Add(Pair("endpoint", RequireText(endpoint, nameof(endpoint))));
            return FlatJsonObject.Format(values);
        }

        /// <summary>終了を求められた (SIGTERM など)。</summary>
        /// <example><c>{"type":"server_stopping","time":"2026-11-13T09:30:00.000Z"}</c></example>
        public static string ServerStopping(DateTime utcNow)
        {
            return FlatJsonObject.Format(Begin(ServerStoppingType, utcNow));
        }

        /// <summary>両チームがそろって試合が始まった。players は試合に入っている人数。</summary>
        /// <example><c>{"type":"match_started","time":"...","matchId":"6c1f...","players":"2"}</c></example>
        public static string MatchStarted(DateTime utcNow, string matchId, int playerCount)
        {
            if (playerCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, "playerCount must not be negative");
            }

            var values = BeginMatch(MatchStartedType, utcNow, matchId);
            values.Add(Pair("players", Number(playerCount)));
            return FlatJsonObject.Format(values);
        }

        /// <summary>
        /// プレイヤーが試合に入った。rejoined は切断してから戻り、前の成績を引き継いだか。
        /// </summary>
        /// <param name="productUserId">EOS の ProductUserId。EOS でつないでいないなら null で、項目ごと出さない。</param>
        /// <example><c>{"type":"player_joined","time":"...","matchId":"6c1f...","playerId":"1","productUserId":"0002...","team":"Bravo","rejoined":"false"}</c></example>
        public static string PlayerJoined(DateTime utcNow, string matchId, int playerId, string productUserId, Team team, bool rejoined)
        {
            var values = BeginMatch(PlayerJoinedType, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            values.Add(Pair("team", MatchLog.TeamName(team)));
            values.Add(Pair("rejoined", Bool(rejoined)));
            return FlatJsonObject.Format(values);
        }

        /// <summary>プレイヤーが試合から抜けた (切断を含む)。</summary>
        /// <param name="productUserId">EOS の ProductUserId。EOS でつないでいないなら null で、項目ごと出さない。</param>
        /// <example><c>{"type":"player_left","time":"...","matchId":"6c1f...","playerId":"1"}</c></example>
        public static string PlayerLeft(DateTime utcNow, string matchId, int playerId, string productUserId)
        {
            var values = BeginMatch(PlayerLeftType, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            return FlatJsonObject.Format(values);
        }

        /// <summary>killer が victim を倒した。teamKill は味方に倒された (点数の入らない) キルか。自滅では killer と victim が同じ。</summary>
        /// <example><c>{"type":"kill","time":"...","matchId":"6c1f...","killerId":"0","victimId":"1","teamKill":"false"}</c></example>
        public static string Kill(
            DateTime utcNow,
            string matchId,
            int killerId,
            string killerProductUserId,
            int victimId,
            string victimProductUserId,
            bool teamKill)
        {
            var values = BeginMatch(KillType, utcNow, matchId);
            AddPlayer(values, "killerId", "killerProductUserId", killerId, killerProductUserId);
            AddPlayer(values, "victimId", "victimProductUserId", victimId, victimProductUserId);
            values.Add(Pair("teamKill", Bool(teamKill)));
            return FlatJsonObject.Format(values);
        }

        /// <summary>試合が終わった。winner は勝ったチーム、引き分けなら <see cref="MatchLog.Draw"/>。</summary>
        /// <example><c>{"type":"match_ended","time":"...","matchId":"6c1f...","winner":"Alpha","scoreAlpha":"30","scoreBravo":"12"}</c></example>
        public static string MatchEnded(DateTime utcNow, string matchId, Team? winner, int alphaScore, int bravoScore)
        {
            if (alphaScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaScore), alphaScore, "alphaScore must not be negative");
            }

            if (bravoScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bravoScore), bravoScore, "bravoScore must not be negative");
            }

            var values = BeginMatch(MatchEndedType, utcNow, matchId);
            values.Add(Pair("winner", winner.HasValue ? MatchLog.TeamName(winner.Value) : MatchLog.Draw));
            values.Add(Pair("scoreAlpha", Number(alphaScore)));
            values.Add(Pair("scoreBravo", Number(bravoScore)));
            return FlatJsonObject.Format(values);
        }

        /// <summary>ログの <c>time</c> の書式。UTC でなければ例外。</summary>
        /// <example><c>2026-11-13T09:00:00.123Z</c></example>
        public static string FormatTime(DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc)
            {
                // ローカル時刻のまま渡すとサーバーの置き場所でずれるので、呼び出し側に UTC を求める
                throw new ArgumentException("time must be UTC (got " + utcNow.Kind + ")", nameof(utcNow));
            }

            return utcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        private static List<KeyValuePair<string, string>> Begin(string type, DateTime utcNow)
        {
            return new List<KeyValuePair<string, string>>
            {
                Pair("type", type),
                Pair("time", FormatTime(utcNow)),
            };
        }

        private static List<KeyValuePair<string, string>> BeginMatch(string type, DateTime utcNow, string matchId)
        {
            var values = Begin(type, utcNow);
            values.Add(Pair("matchId", RequireText(matchId, nameof(matchId))));
            return values;
        }

        private static void AddPlayer(
            List<KeyValuePair<string, string>> values, string idKey, string productUserIdKey, int playerId, string productUserId)
        {
            if (productUserId != null && !NetLaunchOptions.IsProductUserId(productUserId))
            {
                throw new ArgumentException(
                    productUserIdKey + " must be an EOS ProductUserId or null (got '" + productUserId + "')", nameof(productUserId));
            }

            values.Add(Pair(idKey, Number(playerId)));
            if (productUserId != null)
            {
                values.Add(Pair(productUserIdKey, productUserId));
            }
        }

        private static string RequireText(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(paramName + " must not be null or empty", paramName);
            }

            return value;
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
