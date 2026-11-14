using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerMatchTests
    {
        private static readonly DateTime _time = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

        private StringWriter _log;
        private StringWriter _json;
        private ServerMatch _match;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _match = new ServerMatch(5f, _log, new ServerEventWriter(true, _json, _log), () => _time);
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void PlayersAlternateTeamsAndTheMatchStartsWhenBothTeamsHaveSomeone()
        {
            var first = _match.Join(new ConnectionId(1), "key1");
            Assert.That(_match.State, Is.EqualTo(GameModeState.Waiting));

            var second = _match.Join(new ConnectionId(2), "key2");

            Assert.That(first.Team, Is.EqualTo(Team.Alpha));
            Assert.That(first.PlayerId, Is.EqualTo(1));
            Assert.That(second.Team, Is.EqualTo(Team.Bravo));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_log.ToString(), Does.Contain("[Match] joined player=1 team=Alpha"));
            Assert.That(_log.ToString(), Does.Contain("[Match] started players=2"));
            Assert.That(JsonLines(), Is.EqualTo(new[]
            {
                "{\"type\":\"player_joined\",\"time\":\"2026-11-13T09:00:00.000Z\",\"matchId\":\"" + _match.MatchId + "\",\"playerId\":\"1\",\"team\":\"Alpha\",\"rejoined\":\"false\"}",
                "{\"type\":\"player_joined\",\"time\":\"2026-11-13T09:00:00.000Z\",\"matchId\":\"" + _match.MatchId + "\",\"playerId\":\"2\",\"team\":\"Bravo\",\"rejoined\":\"false\"}",
                "{\"type\":\"match_started\",\"time\":\"2026-11-13T09:00:00.000Z\",\"matchId\":\"" + _match.MatchId + "\",\"players\":\"2\"}",
            }));
        }

        [Test]
        public void NewPlayersJoinTheSmallerTeamThenTheLowerScore()
        {
            _match.Join(new ConnectionId(1), null);
            _match.Join(new ConnectionId(2), null);
            _match.Leave(new ConnectionId(1));
            Assert.That(_match.Join(new ConnectionId(3), null).Team, Is.EqualTo(Team.Alpha));

            _match.SetScores(4, 1);
            _match.Leave(new ConnectionId(3));
            _match.Leave(new ConnectionId(2));
            Assert.That(_match.Join(new ConnectionId(4), null).Team, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void LeavingWritesTheLeftEventOnce()
        {
            _match.Join(new ConnectionId(1), null);

            Assert.That(_match.Leave(new ConnectionId(1)), Is.True);
            Assert.That(_match.Leave(new ConnectionId(1)), Is.False);

            Assert.That(_log.ToString(), Does.Contain("[Match] left player=1"));
            Assert.That(JsonLines().Count(line => line.Contains("\"player_left\"", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(_match.PlayerCount, Is.Zero);
        }

        [Test]
        public void EndingWritesTheResultAndTheNextMatchGetsANewIdAfterTheDelay()
        {
            StartTwoPlayers();
            var firstId = _match.MatchId;
            var ended = new List<Team?>();
            var restarted = 0;
            _match.Ended += winner => ended.Add(winner);
            _match.Restarted += () => restarted++;
            _match.SetScores(30, 12);

            Assert.That(_match.End(Team.Alpha), Is.True);
            Assert.That(_match.End(Team.Bravo), Is.False);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(ended, Is.EqualTo(new Team?[] { Team.Alpha }));
            Assert.That(JsonLines().Last(), Is.EqualTo(
                "{\"type\":\"match_ended\",\"time\":\"2026-11-13T09:00:00.000Z\",\"matchId\":\"" + firstId
                + "\",\"winner\":\"Alpha\",\"scoreAlpha\":\"30\",\"scoreBravo\":\"12\"}"));

            _match.Tick(4.5f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_match.RestartRemainingSeconds, Is.EqualTo(0.5f).Within(1e-4f));

            _match.Tick(0.5f);
            Assert.That(restarted, Is.EqualTo(1));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.MatchId, Is.Not.EqualTo(firstId));
            Assert.That(_match.AlphaScore, Is.Zero);
            Assert.That(JsonLines().Last(), Does.Contain("\"match_started\"").And.Contain(_match.MatchId));
            Assert.That(_log.ToString(), Does.Contain("[Match] ended winner=Alpha"));
            Assert.That(_log.ToString(), Does.Contain("[Match] restart"));
        }

        [Test]
        public void DrawIsWrittenAsDraw()
        {
            StartTwoPlayers();

            _match.End(null);

            Assert.That(JsonLines().Last(), Does.Contain("\"winner\":\"draw\""));
        }

        [Test]
        public void NextMatchWaitsWhenATeamIsEmpty()
        {
            StartTwoPlayers();
            _match.End(null);
            _match.Leave(new ConnectionId(2));

            _match.Tick(5f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Waiting));
        }

        [Test]
        public void EndBeforeTheMatchStartsDoesNothing()
        {
            Assert.That(_match.End(Team.Bravo), Is.False);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Waiting));
        }

        [Test]
        public void ConnectionsThatDoNotFitAPlayerIdAreRefused()
        {
            Assert.That(ServerMatch.TryGetPlayerId(new ConnectionId((ulong)BotRoster.FirstBotPlayerId), out _), Is.False);
            Assert.That(ServerMatch.TryGetPlayerId(ConnectionId.None, out _), Is.False);
            Assert.Throws<ArgumentException>(() => _match.Join(new ConnectionId(ulong.MaxValue), null));

            _match.Join(new ConnectionId(1), null);
            Assert.Throws<ArgumentException>(() => _match.Join(new ConnectionId(1), null));
        }

        [Test]
        public void TextFormatBuildsNoJson()
        {
            var match = new ServerMatch(0f, _log, new ServerEventWriter(false, _json, _log), () => _time);

            match.Join(new ConnectionId(1), null);
            match.Join(new ConnectionId(2), null);

            Assert.That(_json.ToString(), Is.Empty);
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
        }

        private void StartTwoPlayers()
        {
            _match.Join(new ConnectionId(1), null);
            _match.Join(new ConnectionId(2), null);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
        }

        private string[] JsonLines()
        {
            return _json.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
