using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DominationObjectiveTests
    {
        private static readonly Vector3 _onA = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 _onB = new Vector3(20f, 0f, 0f);
        private static readonly Vector3 _away = new Vector3(0f, 0f, 50f);

        [Test]
        public void TakesThePointCountFromTheMap()
        {
            var objective = Create();

            Assert.That(objective.Mode.PointCount, Is.EqualTo(2));
            Assert.That(objective.Map.Count, Is.EqualTo(2));
            Assert.That(objective.Mode.State, Is.EqualTo(GameModeState.Waiting));
        }

        [Test]
        public void RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentNullException>(() => new DominationObjective(null, 1f, 1f, 10, 60f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DominationObjective(Map(), 0f, 1f, 10, 60f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DominationObjective(Map(), 1f, 1f, 0, 60f));
        }

        [Test]
        public void DoesNotStartUntilTheMatchIsPlaying()
        {
            var objective = Create();
            objective.SetPlayer(1, Team.Alpha);
            objective.Locate(1, _onA);

            Assert.That(objective.Tick(false, 10f), Is.False);

            Assert.That(objective.Mode.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(objective.Mode.GetControl(0), Is.Zero);
        }

        [Test]
        public void PlayerOnAPointCapturesItAndThenScores()
        {
            var objective = Create(captureSeconds: 4f);
            objective.SetPlayer(1, Team.Alpha);
            objective.SetPlayer(2, Team.Bravo);

            Assert.That(objective.Locate(1, _onA), Is.EqualTo((int?)0));
            Assert.That(objective.Locate(2, _away), Is.Null);

            objective.Tick(true, 2f);
            Assert.That(objective.Mode.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(objective.Mode.GetControl(0), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(objective.Mode.GetOwner(0), Is.Null);

            objective.Tick(true, 2f);
            Assert.That(objective.Mode.GetOwner(0), Is.EqualTo((Team?)Team.Alpha));

            objective.Tick(true, 3f);
            Assert.That(objective.Mode.GetScore(Team.Alpha), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(objective.Mode.GetScore(Team.Bravo), Is.Zero);
            Assert.That(objective.DecideWinner(), Is.EqualTo((Team?)Team.Alpha));
        }

        [Test]
        public void BothTeamsOnAPointStopTheProgress()
        {
            var objective = Create(captureSeconds: 4f);
            objective.SetPlayer(1, Team.Alpha);
            objective.SetPlayer(2, Team.Bravo);
            objective.Locate(1, _onA);
            objective.Locate(2, _away);
            objective.Tick(true, 1f);

            objective.Locate(2, _onA);
            objective.Tick(true, 2f);

            Assert.That(objective.Mode.IsContested(0), Is.True);
            Assert.That(objective.Mode.GetControl(0), Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void PlayerWithoutAPositionLeavesThePoint()
        {
            var objective = Create(captureSeconds: 4f);
            objective.SetPlayer(1, Team.Alpha);
            objective.Locate(1, _onA);
            objective.Tick(true, 1f);

            Assert.That(objective.Locate(1, null), Is.Null);
            Assert.That(objective.Mode.GetPlayerPoint(1), Is.Null);

            // 誰もいなければ中立へ戻る
            objective.Tick(true, 1f);
            Assert.That(objective.Mode.GetControl(0), Is.Zero);
        }

        [Test]
        public void ReachingTheScoreLimitEndsTheObjectiveOnce()
        {
            var objective = Create(captureSeconds: 1f, scoreLimit: 5);
            objective.SetPlayer(1, Team.Bravo);
            objective.Locate(1, _onB);
            objective.Tick(true, 1f);

            Assert.That(objective.Tick(true, 10f), Is.True);
            Assert.That(objective.Mode.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(objective.DecideWinner(), Is.EqualTo((Team?)Team.Bravo));

            Assert.That(objective.Tick(true, 1f), Is.False, "an ended objective does not end again");
        }

        [Test]
        public void EqualPointsAreADraw()
        {
            var objective = Create();

            Assert.That(objective.DecideWinner(), Is.Null);
            Assert.That(DominationObjective.Leader(3f, 3f), Is.Null);
            Assert.That(DominationObjective.Leader(3f, 2f), Is.EqualTo((Team?)Team.Alpha));
            Assert.That(DominationObjective.Leader(2f, 3f), Is.EqualTo((Team?)Team.Bravo));
        }

        [Test]
        public void SetPlayerMovesAPlayerToAnotherTeam()
        {
            var objective = Create();
            objective.SetPlayer(1, Team.Alpha);
            objective.SetPlayer(1, Team.Alpha);
            Assert.That(objective.PlayerCount, Is.EqualTo(1));

            objective.SetPlayer(1, Team.Bravo);

            Assert.That(objective.PlayerCount, Is.EqualTo(1));
            Assert.That(objective.Mode.GetTeam(1), Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void RemovePlayerForgetsThePlayer()
        {
            var objective = Create();
            objective.SetPlayer(1, Team.Alpha);

            Assert.That(objective.RemovePlayer(1), Is.True);
            Assert.That(objective.RemovePlayer(1), Is.False);
            Assert.That(objective.HasPlayer(1), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => objective.Locate(1, _onA));
        }

        [Test]
        public void ResetKeepsPlayersAndClearsPointsAndOwners()
        {
            var objective = Create(captureSeconds: 1f);
            objective.SetPlayer(1, Team.Alpha);
            objective.Locate(1, _onA);
            objective.Tick(true, 1f);
            objective.Tick(true, 2f);

            objective.Reset();

            Assert.That(objective.Mode.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(objective.Mode.GetOwner(0), Is.Null);
            Assert.That(objective.Mode.GetScore(Team.Alpha), Is.Zero);
            Assert.That(objective.HasPlayer(1), Is.True);
            Assert.That(objective.Mode.GetPlayerPoint(1), Is.Null);
        }

        private static DominationObjective Create(float captureSeconds = 4f, int scoreLimit = 100)
        {
            return new DominationObjective(Map(), captureSeconds, 1f, scoreLimit, 300f);
        }

        private static ControlPointMap Map()
        {
            return new ControlPointMap(new[]
            {
                new ControlPointArea(0, _onA, 5f, 6f),
                new ControlPointArea(1, _onB, 5f, 6f),
            });
        }
    }
}
