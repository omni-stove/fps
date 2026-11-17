using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ServerEventLogBetweenMatchesTests
    {
        private const string _matchId = "6c1f3b2a8d4e4f7a9b0c2e5d7a1f4c83";
        private const string _puid = "0002aaaabbbbccccddddeeeeffff0000";
        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, 123, DateTimeKind.Utc);

        [Test]
        public void ChatNamesTheChannelAndLengthButNotTheText()
        {
            var line = ServerEventLog.Chat(_time, _matchId, 1, _puid, ChatChannel.Team, 12);

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"chat\",\"time\":\"2026-11-17T09:00:00.123Z\",\"matchId\":\"" + _matchId
                + "\",\"playerId\":\"1\",\"productUserId\":\"" + _puid + "\",\"channel\":\"Team\",\"length\":\"12\"}"));
        }

        [Test]
        public void ChatRefusedNamesTheReason()
        {
            var line = ServerEventLog.ChatRefused(_time, _matchId, 1, _puid, ChatRefusal.TooSoon);

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"chat_refused\",\"time\":\"2026-11-17T09:00:00.123Z\",\"matchId\":\"" + _matchId
                + "\",\"playerId\":\"1\",\"productUserId\":\"" + _puid + "\",\"reason\":\"TooSoon\"}"));
        }

        [Test]
        public void MapVoteNamesTheChosenMap()
        {
            var line = ServerEventLog.MapVote(_time, _matchId, 2, null, "NetArena02");

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"map_vote\",\"time\":\"2026-11-17T09:00:00.123Z\",\"matchId\":\"" + _matchId
                + "\",\"playerId\":\"2\",\"mapId\":\"NetArena02\"}"));
        }

        [Test]
        public void MapChangedNamesBothMaps()
        {
            var line = ServerEventLog.MapChanged(_time, _matchId, "NetArena01", "NetArena02");

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"map_changed\",\"time\":\"2026-11-17T09:00:00.123Z\",\"matchId\":\"" + _matchId
                + "\",\"fromMapId\":\"NetArena01\",\"toMapId\":\"NetArena02\"}"));
        }

        [Test]
        public void TeamBalancedNamesBothTeams()
        {
            var line = ServerEventLog.TeamBalanced(_time, _matchId, 3, null, Team.Alpha, Team.Bravo);

            Assert.That(line, Is.EqualTo(
                "{\"type\":\"team_balanced\",\"time\":\"2026-11-17T09:00:00.123Z\",\"matchId\":\"" + _matchId
                + "\",\"playerId\":\"3\",\"from\":\"Alpha\",\"to\":\"Bravo\"}"));
        }

        [Test]
        public void EveryNewLineReadsBackWithFlatJsonObject()
        {
            var lines = new[]
            {
                ServerEventLog.Chat(_time, _matchId, 1, null, ChatChannel.All, 1),
                ServerEventLog.ChatRefused(_time, _matchId, 1, null, ChatRefusal.Empty),
                ServerEventLog.MapVote(_time, _matchId, 1, _puid, "NetArena01"),
                ServerEventLog.MapChanged(_time, _matchId, "NetArena01", "NetArena01"),
                ServerEventLog.TeamBalanced(_time, _matchId, 1, _puid, Team.Bravo, Team.Alpha),
            };

            foreach (var line in lines)
            {
                Assert.That(FlatJsonObject.TryParse(line, out _, out var error), Is.True, line + ": " + error);
            }
        }

        [Test]
        public void InvalidValuesAreRefused()
        {
            Assert.That(() => ServerEventLog.Chat(_time, _matchId, 1, null, (ChatChannel)9, 3), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => ServerEventLog.Chat(_time, _matchId, 1, null, ChatChannel.All, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => ServerEventLog.ChatRefused(_time, _matchId, 1, null, ChatRefusal.None), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => ServerEventLog.ChatRefused(_time, _matchId, 1, null, (ChatRefusal)9), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => ServerEventLog.MapVote(_time, _matchId, 1, null, ""), Throws.ArgumentException);
            Assert.That(() => ServerEventLog.MapChanged(_time, _matchId, null, "NetArena02"), Throws.ArgumentException);
            Assert.That(() => ServerEventLog.TeamBalanced(_time, _matchId, 1, null, Team.Alpha, Team.Alpha), Throws.ArgumentException);
            Assert.That(() => ServerEventLog.TeamBalanced(_time, "", 1, null, Team.Alpha, Team.Bravo), Throws.ArgumentException);
        }
    }
}
