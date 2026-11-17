using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // 表示名などの人の名前は出さない。
    public static class ServerEventLog
    {
        public const string ServerStartedType = "server_started";
        public const string ServerStoppingType = "server_stopping";
        public const string MatchStartedType = "match_started";
        public const string PlayerJoinedType = "player_joined";
        public const string PlayerLeftType = "player_left";
        public const string KillType = "kill";
        public const string MatchEndedType = "match_ended";
        public const string PingPlacedType = "ping_placed";
        public const string PingRefusedType = "ping_refused";
        public const string VehicleEnteredType = "vehicle_entered";
        public const string VehicleLeftType = "vehicle_left";
        public const string VehicleExplodedType = "vehicle_exploded";
        public const string ChatType = "chat";
        public const string ChatRefusedType = "chat_refused";
        public const string MapVoteType = "map_vote";
        public const string MapChangedType = "map_changed";
        public const string TeamBalancedType = "team_balanced";

        public static string NewMatchId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string ServerStarted(DateTime utcNow, string version, NetTransportKind transport, string endpoint)
        {
            var values = Begin(ServerStartedType, utcNow);
            values.Add(Pair("version", RequireText(version, nameof(version))));
            values.Add(Pair("transport", NetLaunchOptions.TransportName(transport)));
            values.Add(Pair("endpoint", RequireText(endpoint, nameof(endpoint))));
            return FlatJsonObject.Format(values);
        }

        public static string ServerStopping(DateTime utcNow)
        {
            return FlatJsonObject.Format(Begin(ServerStoppingType, utcNow));
        }

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

        public static string PlayerJoined(DateTime utcNow, string matchId, int playerId, string productUserId, Team team, bool rejoined)
        {
            var values = BeginMatch(PlayerJoinedType, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            values.Add(Pair("team", MatchLog.TeamName(team)));
            values.Add(Pair("rejoined", Bool(rejoined)));
            return FlatJsonObject.Format(values);
        }

        public static string PlayerLeft(DateTime utcNow, string matchId, int playerId, string productUserId)
        {
            var values = BeginMatch(PlayerLeftType, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            return FlatJsonObject.Format(values);
        }

        public static string Kill(
            DateTime utcNow,
            string matchId,
            int killerId,
            string killerProductUserId,
            int victimId,
            string victimProductUserId,
            bool teamKill,
            IReadOnlyList<int> assisterIds = null)
        {
            var values = BeginMatch(KillType, utcNow, matchId);
            AddPlayer(values, "killerId", "killerProductUserId", killerId, killerProductUserId);
            AddPlayer(values, "victimId", "victimProductUserId", victimId, victimProductUserId);
            values.Add(Pair("teamKill", Bool(teamKill)));
            if (assisterIds != null && assisterIds.Count > 0)
            {
                // 値はすべて文字列という決まりを守るため、配列にせずカンマ区切りの 1 つの文字列にする
                var ids = new string[assisterIds.Count];
                for (var i = 0; i < ids.Length; i++)
                {
                    if (assisterIds[i] < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(assisterIds), assisterIds[i], "assister ids must not be negative");
                    }

                    ids[i] = Number(assisterIds[i]);
                }

                values.Add(Pair("assisterIds", string.Join(",", ids)));
            }

            return FlatJsonObject.Format(values);
        }

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

        // 位置は出さない。運用で要るのは誰がどれだけ付けたかで、毎回の座標は行を太らせるだけのため。
        public static string PingPlaced(
            DateTime utcNow, string matchId, int playerId, string productUserId, Team team, int pingId, PingKind kind, int? targetId, bool refreshed)
        {
            if (!Enum.IsDefined(typeof(PingKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be a PingKind");
            }

            if ((kind == PingKind.Enemy) != targetId.HasValue)
            {
                throw new ArgumentException("an enemy ping needs a target and a location ping must not have one", nameof(targetId));
            }

            var values = BeginMatch(PingPlacedType, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            values.Add(Pair("team", MatchLog.TeamName(team)));
            values.Add(Pair("pingId", Number(pingId)));
            values.Add(Pair("kind", kind.ToString()));
            if (targetId.HasValue)
            {
                values.Add(Pair("targetId", Number(targetId.Value)));
            }

            values.Add(Pair("refreshed", Bool(refreshed)));
            return FlatJsonObject.Format(values);
        }

        public static string PingRefused(DateTime utcNow, string matchId, int playerId, string productUserId, PingRefusal reason)
        {
            if (reason == PingRefusal.None || !Enum.IsDefined(typeof(PingRefusal), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a PingRefusal other than None");
            }

            var values = BeginMatch(PingRefusedType, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            values.Add(Pair("reason", reason.ToString()));
            return FlatJsonObject.Format(values);
        }

        public static string VehicleEntered(
            DateTime utcNow, string matchId, int playerId, string productUserId, int vehicle, string vehicleId, int seat, VehicleSeatRole role)
        {
            var values = BeginVehicle(VehicleEnteredType, utcNow, matchId, playerId, productUserId, vehicle, vehicleId);
            values.Add(Pair("seat", Number(seat)));
            values.Add(Pair("role", role.ToString()));
            return FlatJsonObject.Format(values);
        }

        public static string VehicleLeft(
            DateTime utcNow, string matchId, int playerId, string productUserId, int vehicle, string vehicleId, int seat, VehicleLeaveReason reason)
        {
            if (!Enum.IsDefined(typeof(VehicleLeaveReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a VehicleLeaveReason");
            }

            var values = BeginVehicle(VehicleLeftType, utcNow, matchId, playerId, productUserId, vehicle, vehicleId);
            values.Add(Pair("seat", Number(seat)));
            values.Add(Pair("reason", reason.ToString()));
            return FlatJsonObject.Format(values);
        }

        // 壊した人は、最後に傷めた人が抜けていれば出さない。乗り物の Id と並びの番号は、どのマップのどの車かを運用で数えるため両方出す。
        public static string VehicleExploded(
            DateTime utcNow, string matchId, int vehicle, string vehicleId, int? attackerId, string attackerProductUserId, int riders, int damaged,
            int killed)
        {
            if (riders < 0 || damaged < 0 || killed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(riders), "riders, damaged and killed must not be negative");
            }

            var values = BeginMatch(VehicleExplodedType, utcNow, matchId);
            AddVehicle(values, vehicle, vehicleId);
            if (attackerId.HasValue)
            {
                AddPlayer(values, "attackerId", "attackerProductUserId", attackerId.Value, attackerProductUserId);
            }

            values.Add(Pair("riders", Number(riders)));
            values.Add(Pair("damaged", Number(damaged)));
            values.Add(Pair("killed", Number(killed)));
            return FlatJsonObject.Format(values);
        }

        // 本文はプレイヤーが書いたものなので出さず、長さだけを出す (ChatLine.ToString と同じ)。
        public static string Chat(DateTime utcNow, string matchId, int playerId, string productUserId, ChatChannel channel, int length)
        {
            if (!Enum.IsDefined(typeof(ChatChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "channel must be a ChatChannel");
            }

            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "length must be positive");
            }

            var values = BeginMatch(ChatType, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            values.Add(Pair("channel", channel.ToString()));
            values.Add(Pair("length", Number(length)));
            return FlatJsonObject.Format(values);
        }

        public static string ChatRefused(DateTime utcNow, string matchId, int playerId, string productUserId, ChatRefusal reason)
        {
            if (reason == ChatRefusal.None || !Enum.IsDefined(typeof(ChatRefusal), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a ChatRefusal other than None");
            }

            var values = BeginMatch(ChatRefusedType, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            values.Add(Pair("reason", reason.ToString()));
            return FlatJsonObject.Format(values);
        }

        public static string MapVote(DateTime utcNow, string matchId, int playerId, string productUserId, string mapId)
        {
            var values = BeginMatch(MapVoteType, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            values.Add(Pair("mapId", RequireText(mapId, nameof(mapId))));
            return FlatJsonObject.Format(values);
        }

        public static string MapChanged(DateTime utcNow, string matchId, string fromMapId, string toMapId)
        {
            var values = BeginMatch(MapChangedType, utcNow, matchId);
            values.Add(Pair("fromMapId", RequireText(fromMapId, nameof(fromMapId))));
            values.Add(Pair("toMapId", RequireText(toMapId, nameof(toMapId))));
            return FlatJsonObject.Format(values);
        }

        public static string TeamBalanced(DateTime utcNow, string matchId, int playerId, string productUserId, Team from, Team to)
        {
            if (from == to)
            {
                throw new ArgumentException("a balanced player must change teams (from and to are both " + from + ")", nameof(to));
            }

            var values = BeginMatch(TeamBalancedType, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            values.Add(Pair("from", MatchLog.TeamName(from)));
            values.Add(Pair("to", MatchLog.TeamName(to)));
            return FlatJsonObject.Format(values);
        }

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

        private static List<KeyValuePair<string, string>> BeginVehicle(
            string type, DateTime utcNow, string matchId, int playerId, string productUserId, int vehicle, string vehicleId)
        {
            var values = BeginMatch(type, utcNow, matchId);
            AddPlayer(values, "playerId", "productUserId", playerId, productUserId);
            AddVehicle(values, vehicle, vehicleId);
            return values;
        }

        private static void AddVehicle(List<KeyValuePair<string, string>> values, int vehicle, string vehicleId)
        {
            if (vehicle < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vehicle), vehicle, "vehicle must not be negative");
            }

            values.Add(Pair("vehicle", Number(vehicle)));
            values.Add(Pair("vehicleId", RequireText(vehicleId, nameof(vehicleId))));
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
