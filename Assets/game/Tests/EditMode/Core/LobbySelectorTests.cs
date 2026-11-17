using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
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

            Assert.That(lobby.Attributes.Count, Is.EqualTo(2));
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
        public void AVoiceServerIsFullWhenThePlayersFillAllButTheServersSlot()
        {
            var open = new LobbyInfo("s", "server-s", ServerLobby.MaxMembers(4), Ids("server-s"), ServerLobby.Attributes("203.0.113.1:7777", _version, 3));
            var full = new LobbyInfo("s", "server-s", ServerLobby.MaxMembers(4), Ids("server-s"), ServerLobby.Attributes("203.0.113.1:7777", _version, 4));

            Assert.That(LobbySelector.ServerRejection(open, _version), Is.Null);
            Assert.That(LobbySelector.ServerRejection(full, _version), Is.EqualTo("full"));
        }

        [Test]
        public void APartyNeedsRoomForEveryMember()
        {
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 1, max: 4), _version, 3), Is.Null);
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 2, max: 4), _version, 3), Is.EqualTo("room for 2 (party of 3)"));
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 3, max: 4), _version, 3), Is.EqualTo("room for 1 (party of 3)"));
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 4, max: 4), _version, 3), Is.EqualTo("full"));
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 3, max: 4), _version, 1), Is.Null);
            Assert.That(LobbySelector.ServerRejection(Server("s", "203.0.113.1:7777", 0, "0.2.0", 4), _version, 3), Does.Contain("0.2.0"), "the version is checked before the room");
        }

        [Test]
        public void AVoiceServerLeavesTheServersSlotOutOfThePartysRoom()
        {
            var lobby = new LobbyInfo("s", "server-s", ServerLobby.MaxMembers(4), Ids("server-s"), ServerLobby.Attributes("203.0.113.1:7777", _version, 2));

            Assert.That(LobbySelector.ServerRejection(lobby, _version, 2), Is.Null);
            Assert.That(LobbySelector.ServerRejection(lobby, _version, 3), Is.EqualTo("room for 2 (party of 3)"));
        }

        [Test]
        public void PicksTheBusiestServerWithRoomForTheParty()
        {
            var lobbies = new List<LobbyInfo>
            {
                Server("busiest", "203.0.113.1:7777", 6),
                Server("busy", "203.0.113.2:7777", 5),
                Server("empty", "203.0.113.3:7777", 0),
            };

            Assert.That(LobbySelector.PickServer(lobbies, _version, 1).Id, Is.EqualTo("busiest"));
            Assert.That(LobbySelector.PickServer(lobbies, _version, 2).Id, Is.EqualTo("busiest"));
            Assert.That(LobbySelector.PickServer(lobbies, _version, 3).Id, Is.EqualTo("busy"));
            Assert.That(LobbySelector.PickServer(lobbies, _version, 4).Id, Is.EqualTo("empty"));
            Assert.That(LobbySelector.PickServer(lobbies, _version, 9), Is.Null);
        }

        private static LobbyInfo RatedServer(string id, long players, int? skillAverage, int max = 8)
        {
            var server = Server(id, "203.0.113.1:7777", players, max: max);
            if (!skillAverage.HasValue)
            {
                return server;
            }

            var attributes = new List<LobbyAttribute>(server.Attributes)
            {
                LobbyAttribute.OfNumber(ServerLobby.SkillAverageKey, skillAverage.Value),
            };
            return new LobbyInfo(id, server.OwnerUserId, max, server.Members, attributes);
        }

        [Test]
        public void PicksTheServerWhoseSkillAverageIsClosest()
        {
            var lobbies = new List<LobbyInfo>
            {
                RatedServer("strong", 6, 1800),
                RatedServer("near", 1, 1120),
                RatedServer("weak", 5, 800),
            };

            Assert.That(LobbySelector.PickServer(lobbies, _version, 1, 1000).Id, Is.EqualTo("near"));
            Assert.That(LobbySelector.PickServer(lobbies, _version, 1, 1700).Id, Is.EqualTo("strong"));
            Assert.That(LobbySelector.PickServer(lobbies, _version, 1, 700).Id, Is.EqualTo("weak"));
        }

        [Test]
        public void TakesAServerWithoutASkillAverageAsTheFarthest()
        {
            var lobbies = new List<LobbyInfo>
            {
                RatedServer("unrated", 7, null),
                RatedServer("far", 1, 3000),
            };

            Assert.That(LobbySelector.PickServer(lobbies, _version, 1, 1000).Id, Is.EqualTo("far"));
            Assert.That(
                LobbySelector.PickServer(new List<LobbyInfo> { lobbies[0] }, _version, 1, 1000).Id,
                Is.EqualTo("unrated"),
                "an unrated server is still picked when it is the only one with room");
        }

        [Test]
        public void KeepsTheBusiestFirstOrderWhenTheSkillGapsAreEqual()
        {
            var lobbies = new List<LobbyInfo>
            {
                RatedServer("b-above", 2, 1100),
                RatedServer("busy-below", 5, 900),
                RatedServer("a-above", 2, 1100),
                RatedServer("unrated-1", 7, null),
                RatedServer("unrated-0", 7, null),
            };

            Assert.That(LobbySelector.PickServer(lobbies, _version, 1, 1000).Id, Is.EqualTo("busy-below"));

            lobbies.RemoveAt(1);
            Assert.That(LobbySelector.PickServer(lobbies, _version, 1, 1000).Id, Is.EqualTo("a-above"), "equal gap and players fall back to the lobby id");

            var unrated = new List<LobbyInfo> { RatedServer("u-quiet", 1, null), lobbies[2], lobbies[3] };
            Assert.That(LobbySelector.PickServer(unrated, _version, 1, 1000).Id, Is.EqualTo("unrated-0"));
        }

        [Test]
        public void IgnoresSkillWhenTheSearcherHasNoValue()
        {
            var lobbies = new List<LobbyInfo>
            {
                RatedServer("near", 1, 1000),
                RatedServer("unrated-busy", 6, null),
            };

            Assert.That(LobbySelector.PickServer(lobbies, _version, 1, null).Id, Is.EqualTo("unrated-busy"));
            Assert.That(LobbySelector.PickServer(lobbies, _version).Id, Is.EqualTo("unrated-busy"));
        }

        [Test]
        public void SkillDoesNotOverrideRoomForTheParty()
        {
            var lobbies = new List<LobbyInfo>
            {
                RatedServer("near-but-crowded", 7, 1000),
                RatedServer("far-with-room", 2, 2000),
                null,
            };

            Assert.That(LobbySelector.PickServer(lobbies, _version, 2, 1000).Id, Is.EqualTo("far-with-room"));
        }

        [Test]
        public void SkillGapDoesNotOverflowAtTheIntLimits()
        {
            var high = RatedServer("high", 0, int.MaxValue);

            Assert.That(LobbySelector.SkillGap(high, int.MinValue), Is.EqualTo((long)int.MaxValue - int.MinValue));
            Assert.That(LobbySelector.SkillGap(RatedServer("u", 0, null), int.MinValue), Is.EqualTo(long.MaxValue));
            Assert.That(LobbySelector.SkillGap(high, null), Is.Zero);
            Assert.Throws<ArgumentNullException>(() => LobbySelector.SkillGap(null, 1000));
        }

        [Test]
        public void DescribesTheServersTooSmallForTheParty()
        {
            var lobbies = new List<LobbyInfo>
            {
                Server("a", "203.0.113.1:7777", 6),
                Server("f", "203.0.113.2:7777", 8),
                Server("b", "203.0.113.3:7777", 6),
                Server("c", "203.0.113.4:7777", 7),
            };

            Assert.That(
                LobbySelector.DescribeNoServer(lobbies, _version, 3),
                Is.EqualTo("no server to join among 4 found (room for 2 (party of 3) x2, full x1, room for 1 (party of 3) x1)"));
        }

        [Test]
        public void RejectsAPartySizeBelowOne()
        {
            var lobbies = new List<LobbyInfo> { Server("s", "203.0.113.1:7777", 0) };

            Assert.Throws<ArgumentOutOfRangeException>(() => LobbySelector.PickServer(lobbies, _version, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => LobbySelector.ServerRejection(lobbies[0], _version, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => LobbySelector.DescribeNoServer(lobbies, _version, -1));
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

        private static LobbyInfo PrivateServer(string id, string endpoint, long players)
        {
            var match = new NetMatchSettings(privateMatch: true, joinCode: "ABC234");
            return new LobbyInfo(id, "server-" + id, ServerLobby.MaxMembers(8), Ids("server-" + id), ServerLobby.Attributes(endpoint, _version, (int)players, match));
        }

        [Test]
        public void QuickMatchRejectsAPrivateServer()
        {
            Assert.That(LobbySelector.ServerRejection(PrivateServer("p", "203.0.113.1:7777", 0), _version), Is.EqualTo("private"));
            Assert.That(LobbySelector.ServerRejection(PrivateServer("p", "203.0.113.1:7777", 0), _version, 2), Is.EqualTo("private"));
        }

        [Test]
        public void QuickMatchTakesAZeroPrivateFlagAsPublic()
        {
            var attributes = new List<LobbyAttribute>(ServerLobby.Attributes("203.0.113.1:7777", _version, 0))
            {
                LobbyAttribute.OfNumber(ServerLobby.PrivateKey, 0),
            };
            var lobby = new LobbyInfo("s", "server-s", ServerLobby.MaxMembers(8), Ids("server-s"), attributes);

            Assert.That(LobbySelector.ServerRejection(lobby, _version), Is.Null);
        }

        [Test]
        public void PickServerSkipsPrivateServersEvenWhenBusier()
        {
            var lobbies = new List<LobbyInfo>
            {
                PrivateServer("a-private", "203.0.113.1:7777", 6),
                Server("b-public", "203.0.113.2:7777", 2),
            };

            Assert.That(LobbySelector.PickServer(lobbies, _version).Id, Is.EqualTo("b-public"));
            Assert.That(LobbySelector.PickServer(new List<LobbyInfo> { lobbies[0] }, _version), Is.Null);
        }

        [Test]
        public void DescribesPrivateServers()
        {
            var lobbies = new List<LobbyInfo>
            {
                PrivateServer("p1", "203.0.113.1:7777", 0),
                Server("f", "203.0.113.2:7777", 8),
                PrivateServer("p2", "203.0.113.3:7777", 1),
            };

            Assert.That(
                LobbySelector.DescribeNoServer(lobbies, _version),
                Is.EqualTo("no server to join among 3 found (private x2, full x1)"));
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
