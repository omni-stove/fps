using System.Linq;
using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MatchRunnerTests
    {
        private const float TimeLimit = 30f;
        private const int Kills = 2;

        private GameObject _host;
        private BootstrapWorld _world;
        private MatchRunner _runner;
        private Match _match;
        private readonly List<MatchResult> _results = new List<MatchResult>();

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            CombatEvents.ClearSubscribers();
            _results.Clear();
            CombatEvents.MatchFinished += _results.Add;
            _host = new GameObject("Bootstrap");
            var bootstrap = _host.AddComponent<GameBootstrap>();
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            _world = bootstrap.Build();

            // Edit Mode では Start が走らないので、小さい設定の試合を直接渡して始める。
            _runner = _host.AddComponent<MatchRunner>();
            _match = new Match(TimeLimit, Kills, MatchRunner.PlayerId);
            LogAssert.Expect(LogType.Log, "[Match] state=Playing kills=0 remaining=30.0");
            _runner.Begin(_world, _match);
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null)
            {
                Object.DestroyImmediate(_world.Floor);
                Object.DestroyImmediate(_world.Player);
                Object.DestroyImmediate(_world.Light.gameObject);
                Object.DestroyImmediate(_world.TargetRoot);
            }

            Object.DestroyImmediate(_host);
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
        }

        private static IReadOnlyList<ScoreboardSection> PublishedSections()
        {
            var entries = GameSignals.Scoreboard.Value;
            Assert.That(entries, Is.Not.Null, "the scoreboard is published");
            return ScoreboardRows.Build(entries, Team.Alpha);
        }

        [Test]
        public void BeginShowsTheRemainingTimeOnTheHud()
        {
            var model = _world.HudModel;
            Assert.That(model.RemainingSeconds, Is.EqualTo(HudFormat.DisplayedSeconds(TimeLimit)));

            _runner.Tick(11f);

            Assert.That(model.RemainingSeconds, Is.EqualTo(19));
        }

        [Test]
        public void BeginStartsTheMatch()
        {
            Assert.That(_runner.IsStarted, Is.True);
            Assert.That(_runner.Match, Is.SameAs(_match));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_world.Mover.enabled, Is.True);
            Assert.That(_world.Weapon.enabled, Is.True);
        }

        [Test]
        public void KillingTargetDummiesWinsAndStopsThePlayer()
        {
            _world.Targets[0].Damageable.Kill();
            Assert.That(_match.Kills, Is.EqualTo(1));
            Assert.That(_match.IsOver, Is.False);

            LogAssert.Expect(LogType.Log, "[Match] state=Won kills=2 remaining=30.0");
            _world.Targets[1].Damageable.Kill();

            Assert.That(_match.Result, Is.EqualTo(MatchOutcome.Won));
            Assert.That(_world.Mover.enabled, Is.False, "movement stops once the match is over");
            Assert.That(_world.Weapon.enabled, Is.False, "shooting stops once the match is over");
        }

        [Test]
        public void RespawnedDummyCountsAgain()
        {
            var dummy = _world.Targets[0];
            dummy.Damageable.Kill();
            dummy.Respawn();

            LogAssert.Expect(LogType.Log, "[Match] state=Won kills=2 remaining=30.0");
            dummy.Damageable.Kill();

            Assert.That(_match.Result, Is.EqualTo(MatchOutcome.Won));
        }

        [Test]
        public void PlayerDeathLoses()
        {
            _runner.Tick(10f);

            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=0 remaining=20.0");
            _world.PlayerDamageable.Kill();

            Assert.That(_match.Result, Is.EqualTo(MatchOutcome.Lost));
            Assert.That(_world.Mover.enabled, Is.False);
            Assert.That(_world.Weapon.enabled, Is.False);
        }

        [Test]
        public void RunningOutOfTimeLoses()
        {
            _world.Targets[0].Damageable.Kill();

            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=1 remaining=0.0");
            _runner.Tick(TimeLimit + 1f);

            Assert.That(_match.Result, Is.EqualTo(MatchOutcome.Lost));
            Assert.That(_world.Mover.enabled, Is.False);
        }

        [Test]
        public void NothingChangesAfterTheMatchIsOver()
        {
            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=0 remaining=30.0");
            _world.PlayerDamageable.Kill();

            _world.Targets[0].Damageable.Kill();
            _world.Targets[1].Damageable.Kill();
            _runner.Tick(TimeLimit);

            Assert.That(_match.Result, Is.EqualTo(MatchOutcome.Lost));
            Assert.That(_match.Kills, Is.Zero);
        }

        [Test]
        public void WinningRaisesTheResultOnce()
        {
            _world.Targets[0].Damageable.Kill();
            Assert.That(_results, Is.Empty, "nothing is raised before the end");

            LogAssert.Expect(LogType.Log, "[Match] state=Won kills=2 remaining=30.0");
            _world.Targets[1].Damageable.Kill();

            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Won, 2, 0, 2) }));
        }

        [Test]
        public void DyingRaisesALossWithTheDeath()
        {
            _world.Targets[0].Damageable.Kill();

            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=1 remaining=30.0");
            _world.PlayerDamageable.Kill();

            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Lost, 1, 1, 1) }));
        }

        [Test]
        public void RunningOutOfTimeRaisesALossWithoutADeath()
        {
            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=0 remaining=0.0");
            _runner.Tick(TimeLimit);

            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Lost, 0, 0, 0) }));
        }

        [Test]
        public void NothingIsRaisedAgainAfterTheEnd()
        {
            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=0 remaining=30.0");
            _world.PlayerDamageable.Kill();

            _world.Targets[0].Damageable.Kill();
            _world.Targets[1].Damageable.Kill();
            _runner.Tick(TimeLimit);
            _world.PlayerDamageable.ResetHealth();
            _world.PlayerDamageable.Kill();

            Assert.That(_results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Lost, 0, 1, 0) }));
        }

        [Test]
        public void BeginRejectsASecondMatchOrABadOne()
        {
            Assert.Throws<InvalidOperationException>(() => _runner.Begin(_world, new Match(TimeLimit, Kills, MatchRunner.PlayerId)));

            var spare = new GameObject("SpareRunner").AddComponent<MatchRunner>();
            try
            {
                Assert.Throws<ArgumentNullException>(() => spare.Begin(null, new Match(TimeLimit, Kills, MatchRunner.PlayerId)));
                Assert.Throws<ArgumentNullException>(() => spare.Begin(_world, null));
                Assert.Throws<ArgumentException>(() => spare.Begin(_world, _match), "an already started match");
                Assert.Throws<ArgumentException>(() => spare.Begin(_world, new Match(TimeLimit, Kills, MatchRunner.PlayerId + 1)));
                Assert.Throws<InvalidOperationException>(() => spare.Tick(1f));
                Assert.That(spare.IsStarted, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(spare.gameObject);
            }
        }

        [Test]
        public void DestroyingTheRunnerStopsListening()
        {
            // Edit Mode では OnDestroy が呼ばれないので、破棄時に走る Stop を直接呼ぶ。
            _runner.Stop();
            Object.DestroyImmediate(_runner);

            _world.Targets[0].Damageable.Kill();
            _world.PlayerDamageable.Kill();

            Assert.That(_match.Kills, Is.Zero);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void BeginListsThePlayerAndTheTargetsOnTheScoreboard()
        {
            Assert.That(GameSignals.Scoreboard.Value, Is.SameAs(_runner.PublishedScoreboard));
            var sections = PublishedSections();

            Assert.That(sections[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(sections[0].Rows.Count(), Is.EqualTo(1));
            var player = sections[0].Rows[0];
            Assert.That(player.PlayerId, Is.EqualTo(MatchRunner.PlayerId));
            Assert.That(player.Name, Is.EqualTo(ScoreboardName.LocalPlayer));
            Assert.That(player.IsLocal, Is.True);
            Assert.That(player.Connection, Is.EqualTo(ScoreboardConnection.None));

            Assert.That(sections[1].Team, Is.EqualTo(Team.Bravo));
            Assert.That(sections[1].Rows.Count(), Is.EqualTo(_world.Targets.Count));
            for (var i = 0; i < _world.Targets.Count; i++)
            {
                var target = sections[1].Rows[i];
                Assert.That(target.PlayerId, Is.EqualTo(MatchRunner.PlayerId + i + 1));
                Assert.That(target.Name, Is.EqualTo(ScoreboardName.Target(i + 1)));
                Assert.That(target.IsLocal, Is.False);
                Assert.That(target.Kills, Is.Zero);
                Assert.That(target.Deaths, Is.Zero);
            }
        }

        [Test]
        public void KillsAndDeathsUpdateTheScoreboardRightAway()
        {
            var published = new List<IReadOnlyList<ScoreboardEntry>>();
            GameSignals.Scoreboard.Changed += published.Add;
            var firstTargetId = MatchRunner.PlayerId + 1;

            _world.Targets[0].Damageable.Kill();

            Assert.That(published, Has.Count.EqualTo(1), "a kill publishes once");
            var sections = PublishedSections();
            Assert.That(sections[0].Rows[0].Kills, Is.EqualTo(1));
            Assert.That(_runner.TargetDeaths(firstTargetId), Is.EqualTo(1));
            var targets = sections[1].Rows;
            Assert.That(targets[targets.Count - 1].PlayerId, Is.EqualTo(firstTargetId), "the target killed the most is listed last");
            Assert.That(targets[targets.Count - 1].Deaths, Is.EqualTo(1));
            Assert.That(sections[1].Deaths, Is.EqualTo(1));

            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=1 remaining=30.0");
            _world.PlayerDamageable.Kill();

            Assert.That(_runner.PlayerDeaths, Is.EqualTo(1));
            Assert.That(PublishedSections()[0].Rows[0].Deaths, Is.EqualTo(1));
            Assert.That(published[published.Count - 1], Is.Not.SameAs(published[0]), "each publish is a new list");
            GameSignals.Scoreboard.Changed -= published.Add;
        }

        [Test]
        public void StopClearsThePublishedScoreboard()
        {
            _runner.Stop();

            Assert.That(GameSignals.Scoreboard.Value, Is.Null);
            Assert.That(_runner.PublishedScoreboard, Is.Null);
        }

        [Test]
        public void StopKeepsAScoreboardPublishedByAnotherRunner()
        {
            var other = new[]
            {
                new ScoreboardEntry(7, ScoreboardName.Literal("other"), Team.Alpha, 0, 0, 0, ScoreboardConnection.None, true),
            };
            GameSignals.Scoreboard.Publish(other);

            _runner.Stop();

            Assert.That(GameSignals.Scoreboard.Value, Is.SameAs(other));
        }

        [Test]
        public void DeathsAfterTheMatchIsOverAreNotCounted()
        {
            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=0 remaining=30.0");
            _world.PlayerDamageable.Kill();
            _world.Targets[0].Damageable.Kill();

            Assert.That(_runner.TargetDeaths(MatchRunner.PlayerId + 1), Is.Zero);
            Assert.That(PublishedSections()[1].Deaths, Is.Zero);
        }

        [Test]
        public void DefaultsHaveNoTimeLimitAndAReachableKillCount()
        {
            Assert.That(_runner.KillsToWin, Is.EqualTo(MatchRunner.DefaultKillsToWin));
            Assert.That(MatchRunner.DefaultKillsToWin, Is.EqualTo(10));
            Assert.That(_runner.TimeLimitSeconds, Is.EqualTo(0f), "the range has no timer by default");
            Assert.That(float.IsPositiveInfinity(MatchRunner.MatchTimeLimit(_runner.TimeLimitSeconds)), Is.True);
            Assert.That(MatchRunner.MatchTimeLimit(45f), Is.EqualTo(45f));
            Assert.That(MatchRunner.RestartActionPath, Is.EqualTo(WeaponController.ReloadActionPath), "R restarts");
        }

        [Test]
        public void TimeLimitAndKillsToWinRejectBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.TimeLimitSeconds = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.KillsToWin = 0);

            _runner.TimeLimitSeconds = 90f;
            _runner.KillsToWin = 3;
            Assert.That(_runner.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(_runner.KillsToWin, Is.EqualTo(3));
        }

        [Test]
        public void WinningShowsTheResultOnTheHud()
        {
            var model = _world.HudModel;
            Assert.That(model.SoloResult, Is.Null);

            _world.Targets[0].Damageable.Kill();
            LogAssert.Expect(LogType.Log, "[Match] state=Won kills=2 remaining=30.0");
            _world.Targets[1].Damageable.Kill();

            Assert.That(model.SoloResult, Is.EqualTo(MatchOutcome.Won));
            Assert.That(model.HasResult, Is.False, "a solo match has no winning team");
        }

        [Test]
        public void DyingShowsTheLossOnTheHud()
        {
            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=0 remaining=30.0");
            _world.PlayerDamageable.Kill();

            Assert.That(_world.HudModel.SoloResult, Is.EqualTo(MatchOutcome.Lost));
        }

        [Test]
        public void AMatchWithoutATimeLimitHidesTheTimerAndNeverRunsOut()
        {
            var host = new GameObject("NoLimitBootstrap");
            var bootstrap = host.AddComponent<GameBootstrap>();
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            var world = bootstrap.Build();
            try
            {
                var runner = host.AddComponent<MatchRunner>();
                var match = new Match(MatchRunner.MatchTimeLimit(runner.TimeLimitSeconds), Kills, MatchRunner.PlayerId);
                LogAssert.Expect(LogType.Log, "[Match] state=Playing kills=0 remaining=none");
                runner.Begin(world, match);

                runner.Tick(10000f);

                Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
                Assert.That(world.HudModel.HasTime, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(world.Floor);
                Object.DestroyImmediate(world.Player);
                Object.DestroyImmediate(world.Light.gameObject);
                Object.DestroyImmediate(world.TargetRoot);
                Object.DestroyImmediate(host);
            }
        }
    }
}
