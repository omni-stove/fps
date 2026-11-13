using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LobbySelectorTests
    {
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static string[] Ids(string commaSeparated)
        {
            return commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries);
        }

        private static LobbyInfo Lobby(string id, string owner, int max, string members)
        {
            return new LobbyInfo(id, owner, max, Ids(members));
        }

        [Test]
        public void InfoCountsMembersAndFreeSlots()
        {
            var lobby = Lobby("l1", "o", 4, "o,m1");

            Assert.That(lobby.MemberCount, Is.EqualTo(2));
            Assert.That(lobby.FreeSlots, Is.EqualTo(2));
            Assert.That(lobby.IsOwnedBy("O"), Is.True);
            Assert.That(lobby.HasMember("M1"), Is.True);
            Assert.That(lobby.HasMember("m2"), Is.False);
            Assert.That(lobby.IsOwnedBy(null), Is.False);
            Assert.That(lobby.HasMember(null), Is.False);
        }

        [Test]
        public void InfoDropsBlankAndDuplicateMembers()
        {
            var lobby = new LobbyInfo("l1", "o", 8, new List<string> { "o", "", " ", null, "O", "m1" });

            Assert.That(lobby.Members, Is.EqualTo(Ids("o,m1")));
        }

        [Test]
        public void FreeSlotsNeverGoesNegative()
        {
            var lobby = Lobby("l1", "o", 2, "o,m1,m2");

            Assert.That(lobby.FreeSlots, Is.Zero);
        }

        [Test]
        public void InfoDescribesMembersForTheLog()
        {
            var lobby = Lobby("l1", "o", 8, "o,m1");

            Assert.That(lobby.DescribeMembers(), Is.EqualTo("2/8 [o,m1]"));
            Assert.That(lobby.ToString(), Is.EqualTo("id=l1 owner=o members=2/8 [o,m1]"));
        }

        [Test]
        public void InfoRejectsInvalidValues()
        {
            Assert.Throws<ArgumentException>(() => Lobby(" ", "o", 8, "o"));
            Assert.Throws<ArgumentException>(() => Lobby("l1", "", 8, "o"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Lobby("l1", "o", 0, "o"));
            Assert.Throws<ArgumentNullException>(() => _ = new LobbyInfo("l1", "o", 8, null));
        }

        [Test]
        public void PicksTheFullestLobbyWithRoom()
        {
            var lobbies = new List<LobbyInfo>
            {
                Lobby("small", "o1", 8, "o1"),
                Lobby("full", "o2", 2, "o2,m1"),
                Lobby("busy", "o3", 8, "o3,m2,m3"),
            };

            Assert.That(LobbySelector.PickJoinable(lobbies, _self).Id, Is.EqualTo("busy"));
        }

        [Test]
        public void BreaksTiesByLobbyId()
        {
            var lobbies = new List<LobbyInfo>
            {
                Lobby("b", "o1", 8, "o1"),
                Lobby("a", "o2", 8, "o2"),
            };

            Assert.That(LobbySelector.PickJoinable(lobbies, _self).Id, Is.EqualTo("a"));
        }

        [Test]
        public void SkipsOwnLobbiesAndLobbiesAlreadyJoined()
        {
            var lobbies = new List<LobbyInfo>
            {
                Lobby("mine", _self, 8, _self),
                Lobby("joined", "o1", 8, "o1," + _self + ",m1"),
                null,
                Lobby("other", "o2", 8, "o2"),
            };

            Assert.That(LobbySelector.PickJoinable(lobbies, _self.ToUpperInvariant()).Id, Is.EqualTo("other"));
        }

        [Test]
        public void ReturnsNullWhenNothingIsJoinable()
        {
            var lobbies = new List<LobbyInfo> { Lobby("full", "o1", 2, "o1,m1"), Lobby("mine", _self, 8, _self) };

            Assert.That(LobbySelector.PickJoinable(lobbies, _self), Is.Null);
            Assert.That(LobbySelector.PickJoinable(new List<LobbyInfo>(), _self), Is.Null);
        }

        [Test]
        public void PickRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => LobbySelector.PickJoinable(null, _self));
            Assert.Throws<ArgumentException>(() => LobbySelector.PickJoinable(new List<LobbyInfo>(), " "));
        }

        private const string _version = "0.3.0";

        private static LobbyInfo Server(string id, string endpoint, long players, string version = _version, int max = 8)
        {
            var attributes = new List<LobbyAttribute> { LobbyAttribute.OfNumber(ServerLobby.PlayersKey, players) };
            if (endpoint != null)
            {
                attributes.Add(LobbyAttribute.OfText(ServerLobby.ServerKey, endpoint));
            }

            if (version != null)
            {
                attributes.Add(LobbyAttribute.OfText(ServerLobby.VersionKey, version));
            }

            return new LobbyInfo(id, "server-" + id, max, Ids("server-" + id), attributes);
        }

        [Test]
        public void InfoReadsAttributesByKindAndKeepsTheFirstOfAKey()
        {
            var lobby = new LobbyInfo("l1", "o", 8, Ids("o"), new List<LobbyAttribute>
            {
                LobbyAttribute.OfText("server", "203.0.113.10:7777"),
                null,
                LobbyAttribute.OfNumber("players", 3),
                LobbyAttribute.OfText("server", "198.51.100.1:7777"),
            });

            Assert.That(lobby.Attributes, Has.Count.EqualTo(2));
            Assert.That(lobby.TryGetText("server", out var server), Is.True);
            Assert.That(server, Is.EqualTo("203.0.113.10:7777"));
            Assert.That(lobby.TryGetNumber("players", out var players), Is.True);
            Assert.That(players, Is.EqualTo(3));
            Assert.That(lobby.TryGetNumber("server", out _), Is.False, "a text attribute is not a number");
            Assert.That(lobby.TryGetText("players", out _), Is.False, "a number attribute is not text");
            Assert.That(lobby.TryGetText("mode", out _), Is.False);
            Assert.That(Lobby("l2", "o", 8, "o").Attributes, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => _ = new LobbyInfo("l1", "o", 8, Ids("o"), null));
        }

        [Test]
        public void PicksTheBusiestServerWithRoomOnTheSameVersion()
        {
            var lobbies = new List<LobbyInfo>
            {
                Server("empty", "203.0.113.1:7777", 0),
                Server("full", "203.0.113.2:7777", 8),
                Server("busy", "203.0.113.3:7777", 5),
                Server("newer", "203.0.113.4:7777", 7, "0.4.0"),
                null,
            };

            Assert.That(LobbySelector.PickServer(lobbies, _version).Id, Is.EqualTo("busy"));
        }

        [Test]
        public void BreaksServerTiesByLobbyId()
        {
            var lobbies = new List<LobbyInfo>
            {
                Server("b", "203.0.113.1:7777", 2),
                Server("a", "203.0.113.2:7777", 2),
            };

            Assert.That(LobbySelector.PickServer(lobbies, _version).Id, Is.EqualTo("a"));
        }

        [Test]
        public void ServerIsFullAtTheLobbyMaximum()
        {
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 3, max: 4), _version), Is.Null);
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 4, max: 4), _version), Is.EqualTo("full"));
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 9, max: 4), _version), Is.EqualTo("full"));
        }

        [Test]
        public void RejectsServersThatCannotBeJoined()
        {
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 0, "0.2.0"), _version), Does.Contain("0.2.0").And.Contain(_version));
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 0, null), _version), Is.EqualTo("no version"));
            Assert.That(LobbySelector.ServerRejection(Server("s", null, 0), _version), Is.EqualTo("no server address"));
            Assert.That(LobbySelector.ServerRejection(Server("s", "0.0.0.0:7777", 0), _version), Is.EqualTo("no server address"));
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1", 0), _version), Is.EqualTo("no server address"));
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", -1), _version), Is.EqualTo("no player count"));
            Assert.That(LobbySelector.ServerRejection(Lobby("host", "o", 8, "o"), _version), Is.EqualTo("no version"), "a player-hosted lobby has no server attributes");
        }

        [Test]
        public void ComparesVersionsTheWayTheServerWritesThem()
        {
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 0, "0"), " "), Is.Null, "a blank version is written as 0");
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 0, "0.3.0"), " 0.3.0 "), Is.Null);
        }

        [Test]
        public void PickServerReturnsNullWhenNothingIsJoinable()
        {
            var lobbies = new List<LobbyInfo> { Server("full", "203.0.113.1:7777", 8), Server("old", "203.0.113.2:7777", 0, "0.2.0") };

            Assert.That(LobbySelector.PickServer(lobbies, _version), Is.Null);
            Assert.That(LobbySelector.PickServer(new List<LobbyInfo>(), _version), Is.Null);
            Assert.Throws<ArgumentNullException>(() => LobbySelector.PickServer(null, _version));
            Assert.Throws<ArgumentNullException>(() => LobbySelector.ServerRejection(null, _version));
        }

        [Test]
        public void DescribesWhyNoServerWasPicked()
        {
            var lobbies = new List<LobbyInfo>
            {
                Server("f1", "203.0.113.1:7777", 8),
                Server("old", "203.0.113.2:7777", 0, "0.2.0"),
                Server("f2", "203.0.113.3:7777", 8),
            };

            Assert.That(
                LobbySelector.DescribeNoServer(lobbies, _version),
                Is.EqualTo("no server to join among 3 found (full x2, version 0.2.0 (this build is 0.3.0) x1)"));
            Assert.That(LobbySelector.DescribeNoServer(new List<LobbyInfo>(), _version), Is.EqualTo("no server lobby found"));
            Assert.Throws<ArgumentNullException>(() => LobbySelector.DescribeNoServer(null, _version));
        }

        [Test]
        public void RetryScheduleWaitsBetweenAttemptsAndExpires()
        {
            var schedule = new RetrySchedule(3f, 10f);

            Assert.That(schedule.IsDue(0f), Is.False);
            Assert.That(schedule.IsExpired(100f), Is.False);

            schedule.Start(5f);
            Assert.That(schedule.IsDue(5f), Is.True);
            Assert.That(schedule.IsExpired(14.9f), Is.False);
            Assert.That(schedule.IsExpired(15f), Is.True);

            schedule.Wait(6f);
            Assert.That(schedule.IsDue(8.9f), Is.False);
            Assert.That(schedule.IsDue(9f), Is.True);
        }

        [TestCase(0f, 10f)]
        [TestCase(-1f, 10f)]
        [TestCase(1f, 0f)]
        [TestCase(float.NaN, 10f)]
        [TestCase(1f, float.PositiveInfinity)]
        public void RetryScheduleRejectsInvalidTimes(float interval, float timeout)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new RetrySchedule(interval, timeout));
        }
    }
}
