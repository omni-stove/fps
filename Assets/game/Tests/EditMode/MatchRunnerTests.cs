using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class MatchRunnerTests
    {
        private const float TimeLimit = 30f;
        private const int Kills = 2;

        private GameObject _host;
        private BootstrapWorld _world;
        private MatchRunner _runner;
        private Match _match;

        [SetUp]
        public void SetUp()
        {
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
                // Edit Mode では Hud の OnDestroy が走らないので、PanelSettings もここで消す。
                if (_world.Hud != null)
                {
                    Object.DestroyImmediate(_world.Hud.gameObject);
                }

                if (_world.HudPanelSettings != null)
                {
                    Object.DestroyImmediate(_world.HudPanelSettings);
                }

                Object.DestroyImmediate(_world.Floor);
                Object.DestroyImmediate(_world.Player);
                Object.DestroyImmediate(_world.Light.gameObject);
                Object.DestroyImmediate(_world.TargetRoot);
            }

            Object.DestroyImmediate(_host);
        }

        [Test]
        public void BeginShowsTheRemainingTimeOnTheHud()
        {
            var model = _world.Hud.Model;
            Assert.That(model.TimeText, Is.EqualTo(HudFormat.Time(TimeLimit)));

            _runner.Tick(11f);

            Assert.That(model.TimeText, Is.EqualTo("00:19"));
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

            Assert.That(_match.Result, Is.EqualTo(MatchResult.Won));
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

            Assert.That(_match.Result, Is.EqualTo(MatchResult.Won));
        }

        [Test]
        public void PlayerDeathLoses()
        {
            _runner.Tick(10f);

            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=0 remaining=20.0");
            _world.PlayerDamageable.Kill();

            Assert.That(_match.Result, Is.EqualTo(MatchResult.Lost));
            Assert.That(_world.Mover.enabled, Is.False);
            Assert.That(_world.Weapon.enabled, Is.False);
        }

        [Test]
        public void RunningOutOfTimeLoses()
        {
            _world.Targets[0].Damageable.Kill();

            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=1 remaining=0.0");
            _runner.Tick(TimeLimit + 1f);

            Assert.That(_match.Result, Is.EqualTo(MatchResult.Lost));
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

            Assert.That(_match.Result, Is.EqualTo(MatchResult.Lost));
            Assert.That(_match.Kills, Is.Zero);
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
            var sections = _world.Hud.Scoreboard.Sections;

            Assert.That(sections[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(sections[0].Rows, Has.Count.EqualTo(1));
            var player = sections[0].Rows[0];
            Assert.That(player.PlayerId, Is.EqualTo(MatchRunner.PlayerId));
            Assert.That(player.Name, Is.EqualTo(ScoreboardRows.LocalPlayerName));
            Assert.That(player.IsLocal, Is.True);
            Assert.That(player.Connection, Is.EqualTo(ScoreboardConnection.None));

            Assert.That(sections[1].Team, Is.EqualTo(Team.Bravo));
            Assert.That(sections[1].Rows, Has.Count.EqualTo(_world.Targets.Count));
            for (var i = 0; i < _world.Targets.Count; i++)
            {
                var target = sections[1].Rows[i];
                Assert.That(target.PlayerId, Is.EqualTo(MatchRunner.PlayerId + i + 1));
                Assert.That(target.Name, Is.EqualTo(ScoreboardRows.TargetName(i + 1)));
                Assert.That(target.IsLocal, Is.False);
                Assert.That(target.Kills, Is.Zero);
                Assert.That(target.Deaths, Is.Zero);
            }
        }

        [Test]
        public void KillsAndDeathsUpdateTheScoreboardRightAway()
        {
            var scoreboard = _world.Hud.Scoreboard;
            var firstTargetId = MatchRunner.PlayerId + 1;

            _world.Targets[0].Damageable.Kill();

            Assert.That(scoreboard.Sections[0].Rows[0].Kills, Is.EqualTo(1));
            Assert.That(_runner.TargetDeaths(firstTargetId), Is.EqualTo(1));
            var targets = scoreboard.Sections[1].Rows;
            Assert.That(targets[targets.Count - 1].PlayerId, Is.EqualTo(firstTargetId), "the target killed the most is listed last");
            Assert.That(targets[targets.Count - 1].Deaths, Is.EqualTo(1));
            Assert.That(scoreboard.Sections[1].Deaths, Is.EqualTo(1));

            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=1 remaining=30.0");
            _world.PlayerDamageable.Kill();

            Assert.That(_runner.PlayerDeaths, Is.EqualTo(1));
            Assert.That(scoreboard.Sections[0].Rows[0].Deaths, Is.EqualTo(1));
        }

        [Test]
        public void DeathsAfterTheMatchIsOverAreNotCounted()
        {
            LogAssert.Expect(LogType.Log, "[Match] state=Lost kills=0 remaining=30.0");
            _world.PlayerDamageable.Kill();
            _world.Targets[0].Damageable.Kill();

            Assert.That(_runner.TargetDeaths(MatchRunner.PlayerId + 1), Is.Zero);
            Assert.That(_world.Hud.Scoreboard.Sections[1].Deaths, Is.Zero);
        }

        [Test]
        public void DefaultsAreReachableInTheBootstrapScene()
        {
            Assert.That(MatchRunner.KillsToWin, Is.GreaterThan(0));
            Assert.That(MatchRunner.TimeLimitSeconds, Is.GreaterThan(0f));
            Assert.That(MatchRunner.RestartActionPath, Is.EqualTo(WeaponController.ReloadActionPath), "R restarts");
        }
    }
}
