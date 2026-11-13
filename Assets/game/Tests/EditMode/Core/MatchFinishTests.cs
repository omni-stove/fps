using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MatchFinishTests
    {
        [Test]
        public void ModeSessionFinishEndsOnlyWhilePlaying()
        {
            var session = new ModeSession(
                new TwoTeams(), 60f, Array.Empty<IScoreRule>(), null, Array.Empty<IWinCondition>());
            var ended = new List<Side?>();
            session.Ended += winner => ended.Add(winner);

            Assert.That(session.Finish(Side.Alpha), Is.False, "a waiting session does not end");

            session.Start();
            Assert.That(session.Finish(Side.Bravo), Is.True);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)Side.Bravo));

            Assert.That(session.Finish(Side.Alpha), Is.False, "an ended session keeps its winner");
            Assert.That(session.Winner, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(ended, Is.EqualTo(new List<Side?> { Side.Bravo }));
        }

        [Test]
        public void ModeSessionFinishRejectsASideOutsideTheLayout()
        {
            var session = new ModeSession(
                new TwoTeams(), 60f, Array.Empty<IScoreRule>(), null, Array.Empty<IWinCondition>());
            session.Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => session.Finish(new Side(5)));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void TeamDeathmatchSessionFinishEndsWithTheGivenWinnerAndWaitsToRestart()
        {
            var session = CreateSession(restartDelaySeconds: 5f);
            var ended = new List<Team?>();
            session.Ended += winner => ended.Add(winner);
            session.Join(0);
            session.Join(1);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));

            Assert.That(session.Finish(null), Is.True);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.Null);
            Assert.That(ended, Is.EqualTo(new List<Team?> { null }));
            Assert.That(session.IsRestartPending, Is.True);
            Assert.That(session.RestartRemainingSeconds, Is.EqualTo(5f));

            Assert.That(session.Finish(Team.Alpha), Is.False);
            Assert.That(ended, Has.Count.EqualTo(1));

            session.Tick(5f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void TeamDeathmatchSessionFinishDoesNothingBeforeTheMatchStarts()
        {
            var session = CreateSession(restartDelaySeconds: 5f);
            session.Join(0);

            Assert.That(session.Finish(Team.Alpha), Is.False);
            Assert.That(session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(session.IsRestartPending, Is.False);
        }

        private static TeamDeathmatchSession CreateSession(float restartDelaySeconds)
        {
            var points = new[]
            {
                new SpawnPoint(new Vector3(-20f, 0f, 0f), Team.Alpha),
                new SpawnPoint(new Vector3(20f, 0f, 0f), Team.Bravo),
            };
            return new TeamDeathmatchSession(
                new TeamDeathmatchSpec(10, 300f, 3f), 100, points, new SpawnSelector(), _ => Vector3.Zero, restartDelaySeconds);
        }
    }
}
