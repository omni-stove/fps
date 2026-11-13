using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // System.Text.Json は Unity に無いので、JSON として読めるかは dotnet のテストでだけ確かめる
    public sealed class ServerEventLogTests
    {
        private const string MatchId = "0123456789abcdef0123456789abcdef";
        private const string KillerPuid = "0002aaaabbbbccccddddeeeeffff0000";
        private const string VictimPuid = "0002111122223333444455556666aaaa";

        private static readonly DateTime _time = new DateTime(2026, 11, 13, 9, 30, 5, 123, DateTimeKind.Utc);

        /// <summary>1 行の JSON オブジェクトとして読み、キーと値を出てきた順に返す。値はすべて文字列であること。</summary>
        private static List<KeyValuePair<string, string>> ReadLine(string line)
        {
            Assert.That(line, Does.Not.Contain("\n").And.Not.Contain("\r"));
            using var document = JsonDocument.Parse(line);
            Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));

            var pairs = new List<KeyValuePair<string, string>>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                Assert.That(property.Value.ValueKind, Is.EqualTo(JsonValueKind.String), property.Name);
                pairs.Add(new KeyValuePair<string, string>(property.Name, property.Value.GetString()));
            }

            return pairs;
        }

        private static string[] Keys(List<KeyValuePair<string, string>> pairs)
        {
            return pairs.Select(p => p.Key).ToArray();
        }

        private static string Value(List<KeyValuePair<string, string>> pairs, string key)
        {
            return pairs.Single(p => p.Key == key).Value;
        }

        [Test]
        public void KillHasTypeTimeMatchAndBothPlayers()
        {
            var line = ServerEventLog.Kill(_time, MatchId, 0, KillerPuid, 1, VictimPuid, false);
            var pairs = ReadLine(line);

            Assert.That(Keys(pairs), Is.EqualTo(new[]
            {
                "type", "time", "matchId", "killerId", "killerProductUserId", "victimId", "victimProductUserId", "teamKill",
            }));
            Assert.That(Value(pairs, "type"), Is.EqualTo("kill"));
            Assert.That(Value(pairs, "time"), Is.EqualTo("2026-11-13T09:30:05.123Z"));
            Assert.That(Value(pairs, "matchId"), Is.EqualTo(MatchId));
            Assert.That(Value(pairs, "killerId"), Is.EqualTo("0"));
            Assert.That(Value(pairs, "killerProductUserId"), Is.EqualTo(KillerPuid));
            Assert.That(Value(pairs, "victimId"), Is.EqualTo("1"));
            Assert.That(Value(pairs, "victimProductUserId"), Is.EqualTo(VictimPuid));
            Assert.That(Value(pairs, "teamKill"), Is.EqualTo("false"));
        }

        [Test]
        public void KillLeavesOutUnknownProductUserIds()
        {
            var line = ServerEventLog.Kill(_time, MatchId, 3, null, 3, null, true);

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"kill\",\"time\":\"2026-11-13T09:30:05.123Z\",\"matchId\":\"" + MatchId
                + "\",\"killerId\":\"3\",\"victimId\":\"3\",\"teamKill\":\"true\"}"));
        }

        [Test]
        public void PlayerJoinedHasTeamAndRejoined()
        {
            var pairs = ReadLine(ServerEventLog.PlayerJoined(_time, MatchId, 2, KillerPuid, Team.Bravo, true));

            Assert.That(Keys(pairs), Is.EqualTo(new[]
            {
                "type", "time", "matchId", "playerId", "productUserId", "team", "rejoined",
            }));
            Assert.That(Value(pairs, "type"), Is.EqualTo("player_joined"));
            Assert.That(Value(pairs, "playerId"), Is.EqualTo("2"));
            Assert.That(Value(pairs, "team"), Is.EqualTo("Bravo"));
            Assert.That(Value(pairs, "rejoined"), Is.EqualTo("true"));
        }

        [Test]
        public void PlayerLeftWithoutProductUserIdHasOnlyThePlayerId()
        {
            var pairs = ReadLine(ServerEventLog.PlayerLeft(_time, MatchId, 5, null));

            Assert.That(Keys(pairs), Is.EqualTo(new[] { "type", "time", "matchId", "playerId" }));
            Assert.That(Value(pairs, "type"), Is.EqualTo("player_left"));
            Assert.That(Value(pairs, "playerId"), Is.EqualTo("5"));
        }

        [Test]
        public void MatchStartedHasThePlayerCount()
        {
            var pairs = ReadLine(ServerEventLog.MatchStarted(_time, MatchId, 2));

            Assert.That(Keys(pairs), Is.EqualTo(new[] { "type", "time", "matchId", "players" }));
            Assert.That(Value(pairs, "type"), Is.EqualTo("match_started"));
            Assert.That(Value(pairs, "players"), Is.EqualTo("2"));
        }

        [Test]
        public void MatchEndedHasTheWinnerAndScores()
        {
            var pairs = ReadLine(ServerEventLog.MatchEnded(_time, MatchId, Team.Alpha, 30, 12));

            Assert.That(Keys(pairs), Is.EqualTo(new[] { "type", "time", "matchId", "winner", "scoreAlpha", "scoreBravo" }));
            Assert.That(Value(pairs, "type"), Is.EqualTo("match_ended"));
            Assert.That(Value(pairs, "winner"), Is.EqualTo("Alpha"));
            Assert.That(Value(pairs, "scoreAlpha"), Is.EqualTo("30"));
            Assert.That(Value(pairs, "scoreBravo"), Is.EqualTo("12"));
        }

        [Test]
        public void MatchEndedInADrawSaysDraw()
        {
            var pairs = ReadLine(ServerEventLog.MatchEnded(_time, MatchId, null, 4, 4));

            Assert.That(Value(pairs, "winner"), Is.EqualTo(MatchLog.Draw));
        }

        [Test]
        public void ServerEventsHaveNoMatchId()
        {
            var started = ReadLine(ServerEventLog.ServerStarted(_time, "0.2.0", NetTransportKind.Udp, "0.0.0.0:7777"));
            var stopping = ReadLine(ServerEventLog.ServerStopping(_time));

            Assert.That(Keys(started), Is.EqualTo(new[] { "type", "time", "version", "transport", "endpoint" }));
            Assert.That(Value(started, "type"), Is.EqualTo("server_started"));
            Assert.That(Value(started, "transport"), Is.EqualTo("udp"));
            Assert.That(Value(started, "endpoint"), Is.EqualTo("0.0.0.0:7777"));
            Assert.That(Keys(stopping), Is.EqualTo(new[] { "type", "time" }));
            Assert.That(Value(stopping, "type"), Is.EqualTo("server_stopping"));
        }

        [Test]
        public void EveryLineReadsBackWithFlatJsonObject()
        {
            var lines = new[]
            {
                ServerEventLog.ServerStarted(_time, "0.2.0-dirty", NetTransportKind.Eos, "eos"),
                ServerEventLog.MatchStarted(_time, MatchId, 0),
                ServerEventLog.PlayerJoined(_time, MatchId, 1, null, Team.Alpha, false),
                ServerEventLog.Kill(_time, MatchId, 1, null, 2, VictimPuid, false),
                ServerEventLog.PlayerLeft(_time, MatchId, 1, null),
                ServerEventLog.MatchEnded(_time, MatchId, Team.Bravo, 0, 1),
                ServerEventLog.ServerStopping(_time),
            };

            foreach (var line in lines)
            {
                var ok = FlatJsonObject.TryParse(line, out var values, out var error);
                Assert.That(ok, Is.True, line + ": " + error);
                Assert.That(values["type"], Is.EqualTo(ReadLine(line)[0].Value));
            }
        }

        [Test]
        public void NewMatchIdsAreDistinctHex()
        {
            var first = ServerEventLog.NewMatchId();
            var second = ServerEventLog.NewMatchId();

            Assert.That(first, Does.Match("^[0-9a-f]{32}$"));
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void RejectsNonUtcTime()
        {
            var local = new DateTime(2026, 11, 13, 9, 30, 5, DateTimeKind.Local);
            var unspecified = new DateTime(2026, 11, 13, 9, 30, 5, DateTimeKind.Unspecified);

            Assert.Throws<ArgumentException>(() => ServerEventLog.ServerStopping(local));
            Assert.Throws<ArgumentException>(() => ServerEventLog.ServerStopping(unspecified));
        }

        [Test]
        public void RejectsMissingMatchIdAndMalformedProductUserId()
        {
            Assert.Throws<ArgumentException>(() => ServerEventLog.MatchStarted(_time, null, 2));
            Assert.Throws<ArgumentException>(() => ServerEventLog.MatchStarted(_time, "", 2));
            Assert.Throws<ArgumentException>(() => ServerEventLog.PlayerLeft(_time, MatchId, 1, "Alice"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerEventLog.MatchStarted(_time, MatchId, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerEventLog.MatchEnded(_time, MatchId, null, -1, 0));
        }
    }
}
