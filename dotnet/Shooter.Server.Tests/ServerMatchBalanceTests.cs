using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerMatchBalanceTests
    {
        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

        private StringWriter _log;
        private StringWriter _json;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void MovesTheLatestJoinerEvenWhenABotHasALargerId()
        {
            var match = Create(1);
            var events = new List<string>();
            match.TeamChanged += (player, from) => events.Add("moved " + player.PlayerId + " " + from + ">" + player.Team);
            match.Restarted += () => events.Add("restarted");

            Join(match, 1);
            Join(match, 2);
            var bot = match.JoinBot(BotRoster.FirstBotPlayerId);
            Join(match, 3);
            Join(match, 4);
            Assert.That(bot.Team, Is.EqualTo(Team.Alpha));
            Assert.That(match.Leave(new ConnectionId(2)), Is.True);
            Assert.That(match.Leave(new ConnectionId(3)), Is.True);

            // Alpha に 1・Bot・4 の順で入って 3 対 0
            Assert.That(match.End(null), Is.True);
            match.Tick(1f);

            Assert.That(events, Is.EqualTo(Events("moved 4 Alpha>Bravo", "restarted")));
            Assert.That(bot.Team, Is.EqualTo(Team.Alpha));
            Assert.That(match.CountPlayers(Team.Alpha), Is.EqualTo(2));
            Assert.That(match.CountPlayers(Team.Bravo), Is.EqualTo(1));
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void ZeroLimitMovesNobody()
        {
            var match = Create(0);
            var moved = 0;
            match.TeamChanged += (_, _) => moved++;
            Join(match, 1);
            Join(match, 2);
            Join(match, 3);
            Join(match, 5);
            Assert.That(match.Leave(new ConnectionId(2)), Is.True);
            Assert.That(match.Leave(new ConnectionId(5)), Is.True);

            Assert.That(match.End(null), Is.True);
            match.Tick(1f);

            Assert.That(moved, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Not.Contain("balanced"));
        }

        [Test]
        public void KeepsThePartyIdAndTheJoinOrder()
        {
            var match = Create(1);

            var first = match.Join(new ConnectionId(1), "device1", null, "party-1");
            var second = match.Join(new ConnectionId(2), "device2", null, "");

            Assert.That(first.PartyId, Is.EqualTo("party-1"));
            Assert.That(second.PartyId, Is.Null);
            Assert.That(second.JoinOrder, Is.GreaterThan(first.JoinOrder));
            Assert.That(() => match.Join(new ConnectionId(3), "device3", null, "not a party"), Throws.ArgumentException);
            Assert.That(match.PlayerCount, Is.EqualTo(2));
        }

        [Test]
        public void ANegativeLimitIsRefused()
        {
            Assert.That(() => Create(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Events(params string[] expected) => expected;

        private ServerMatch Create(int maxTeamSizeDifference)
        {
            return new ServerMatch(1f, _log, new ServerEventWriter(true, _json, _log), () => _time, maxTeamSizeDifference);
        }

        private static void Join(ServerMatch match, int id)
        {
            match.Join(new ConnectionId((ulong)id), "device" + id);
        }
    }
}
