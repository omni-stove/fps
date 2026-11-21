using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 目的がチームを決める試合 (Horde) では、パーティと人数より目的のチームを先にする
    public sealed class ServerMatchFixedTeamTests
    {
        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

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
        public void TheObjectiveTeamComesBeforeThePartyAndTheHeadcount()
        {
            var match = Create(1);
            match.FixedTeam = isBot => isBot ? Team.Bravo : Team.Alpha;

            var first = match.Join(new ConnectionId(1), "device1", partyId: "party-a");
            var second = match.Join(new ConnectionId(2), "device2", partyId: "party-b");
            var third = match.Join(new ConnectionId(3), "device3");
            var bot = match.JoinBot(BotRoster.FirstBotPlayerId);

            Assert.That(first.Team, Is.EqualTo(Team.Alpha));
            Assert.That(second.Team, Is.EqualTo(Team.Alpha));
            Assert.That(third.Team, Is.EqualTo(Team.Alpha));
            Assert.That(bot.Team, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void ANullTeamLeavesTheChoiceToTheMatch()
        {
            var match = Create(0);
            match.FixedTeam = _ => null;

            var first = match.Join(new ConnectionId(1), "device1");
            var second = match.Join(new ConnectionId(2), "device2");

            Assert.That(first.Team, Is.Not.EqualTo(second.Team), "the headcount splits them as before");
        }

        [Test]
        public void TheNextMatchMovesEveryoneToTheObjectiveTeamInsteadOfBalancing()
        {
            var match = Create(1);
            var events = new List<string>();
            match.TeamChanged += (player, from) => events.Add("moved " + player.PlayerId + " " + from + ">" + player.Team);
            var first = match.Join(new ConnectionId(1), "device1");
            var second = match.Join(new ConnectionId(2), "device2");
            var bot = match.JoinBot(BotRoster.FirstBotPlayerId);
            Assert.That(second.Team, Is.Not.EqualTo(first.Team));
            var humanOnBravo = first.Team == Team.Bravo ? first : second;
            var botFrom = bot.Team;

            match.FixedTeam = isBot => isBot ? Team.Bravo : Team.Alpha;
            Assert.That(match.End(null), Is.True);
            match.Tick(1f);

            Assert.That(first.Team, Is.EqualTo(Team.Alpha));
            Assert.That(second.Team, Is.EqualTo(Team.Alpha), "the headcount is not balanced against the objective");
            Assert.That(bot.Team, Is.EqualTo(Team.Bravo));
            Assert.That(events, Does.Contain("moved " + humanOnBravo.PlayerId + " Bravo>Alpha"));
            if (botFrom != Team.Bravo)
            {
                Assert.That(events, Does.Contain("moved " + bot.PlayerId + " Alpha>Bravo"));
            }

            Assert.That(match.CountPlayers(Team.Alpha), Is.EqualTo(2));
            Assert.That(match.CountPlayers(Team.Bravo), Is.EqualTo(1));
        }

        private ServerMatch Create(int maxTeamSizeDifference)
        {
            return new ServerMatch(1f, _log, new ServerEventWriter(true, _json, _log), () => _time, maxTeamSizeDifference);
        }
    }
}
