using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// <see cref="DominationRunner"/> が、拠点にいるプレイヤーを Domination に伝えて占領・得点・終了まで進め、
    /// キルとデスを渡し、倒れた側を出現点へ戻し、試合の状態を <see cref="GameSignals.Domination"/> に出すことを確かめる。
    /// NavMesh の上を歩く敵と R での再開は Play で確かめる (手順は PR 本文)。
    /// </summary>
    public sealed class DominationRunnerTests
    {
        // 1 秒で占領、1 秒に 10 点、30 点で勝ち、制限時間 60 秒、倒れて 2 秒で戻る。
        private const float CaptureSeconds = 1f;
        private const float PointsPerSecond = 10f;
        private const int ScoreLimit = 30;
        private const float TimeLimit = 60f;
        private const float RespawnSeconds = 2f;

        private static readonly Vector3 EnemyStart = new Vector3(20f, 1f, 20f);

        private readonly List<Object> _created = new List<Object>();
        private GameObject _host;
        private BootstrapWorld _world;
        private DominationRunner _runner;
        private ControlPointZone _zone;
        private EnemyAgent _enemy;
        private Damageable _enemyBody;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();

            _host = new GameObject("Bootstrap");
            _created.Add(_host);
            var bootstrap = _host.AddComponent<GameBootstrap>();
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            _world = bootstrap.Build();

            // 拠点はプレイヤーの足元 (原点) に置く。
            _zone = CreateZone("PointA", 0, Vector3.zero);

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _created.Add(_rifle);

            // Edit Mode では Awake が走らないので、敵は自分で初期化する。
            var enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyObject.name = "Enemy-1";
            enemyObject.transform.position = EnemyStart;
            _created.Add(enemyObject);
            _enemy = enemyObject.AddComponent<EnemyAgent>();

            // カプセルの中心が Transform の位置なので、足元は 1m 下 (出現点の比較をこの値で書く)。
            enemyObject.GetComponent<NavMeshAgent>().baseOffset = 1f;
            _enemy.WeaponDefinition = _rifle;
            _enemy.Id = "enemy-1";
            Assert.That(_enemy.TryInitialize(), Is.True);
            _enemyBody = enemyObject.GetComponent<Damageable>();

            _runner = _host.AddComponent<DominationRunner>();
            _runner.CaptureSeconds = CaptureSeconds;
            _runner.PointsPerSecond = PointsPerSecond;
            _runner.ScoreLimit = ScoreLimit;
            _runner.TimeLimitSeconds = TimeLimit;
            _runner.RespawnSeconds = RespawnSeconds;
        }

        [TearDown]
        public void TearDown()
        {
            if (_runner != null)
            {
                _runner.Stop();
            }

            if (_world != null)
            {
                Object.DestroyImmediate(_world.Floor);
                Object.DestroyImmediate(_world.Player);
                Object.DestroyImmediate(_world.Light.gameObject);
                Object.DestroyImmediate(_world.TargetRoot);
            }

            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
        }

        [Test]
        public void BeginAddsThePlayerAndEnemiesAndStarts()
        {
            Begin();

            var domination = _runner.Domination;
            Assert.That(domination.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(domination.PlayerCount, Is.EqualTo(2));
            Assert.That(domination.GetTeam(DominationRunner.PlayerId), Is.EqualTo(Team.Alpha));
            Assert.That(domination.GetTeam(1), Is.EqualTo(Team.Bravo));
            Assert.That(domination.PointCount, Is.EqualTo(1));
            Assert.That(_runner.Zones, Is.EqualTo(new[] { _zone }));
            Assert.That(_world.TargetRoot.activeSelf, Is.False, "target dummies are not part of the match");
        }

        [Test]
        public void BeginPaintsTheTeamsFromTheOfflinePlayersSideUntilStopped()
        {
            Assert.That(GameSignals.PaletteViewer.Value, Is.Null);

            Begin();
            Assert.That(GameSignals.PaletteViewer.Value, Is.EqualTo((Team?)TeamColors.OfflineViewer));

            _runner.Stop();
            Assert.That(GameSignals.PaletteViewer.Value, Is.Null);

            GameSignals.PaletteViewer.Publish(Team.Bravo);
            _runner.Stop();
            Assert.That(GameSignals.PaletteViewer.Value, Is.EqualTo((Team?)Team.Bravo), "a viewer published by someone else stays");
        }

        [Test]
        public void StandingOnThePointCapturesItAndScores()
        {
            Begin();

            LogAssert.Expect(LogType.Log, "[Domination] captured point=A team=Alpha");
            _runner.Tick(CaptureSeconds);

            var domination = _runner.Domination;
            Assert.That(domination.GetPlayerPoint(DominationRunner.PlayerId), Is.EqualTo(0));
            Assert.That(domination.GetPlayerPoint(1), Is.Null, "the enemy stands outside the point");
            Assert.That(domination.GetOwner(0), Is.EqualTo(Team.Alpha));
            Assert.That(_zone.Owner, Is.EqualTo(Team.Alpha));
            Assert.That(_zone.Occupants, Is.EqualTo(new[] { _world.PlayerDamageable }));

            _runner.Tick(1f);

            Assert.That(domination.GetScore(Team.Alpha), Is.EqualTo(PointsPerSecond).Within(1e-4f));
        }

        [Test]
        public void ReachingTheScoreLimitEndsWithTheWinnerAndStopsThePlayer()
        {
            Begin();
            LogAssert.Expect(LogType.Log, "[Domination] captured point=A team=Alpha");
            _runner.Tick(CaptureSeconds);

            LogAssert.Expect(LogType.Log, "[Domination] state=Ended A=30 B=0 remaining=56.0");
            LogAssert.Expect(LogType.Log, "[Domination] ended winner=Alpha A=30 B=0");
            _runner.Tick(3f);

            Assert.That(_runner.Domination.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(_world.Mover.enabled, Is.False);
            Assert.That(_world.Weapon.enabled, Is.False);
        }

        [Test]
        public void RunningOutOfTimeWithNoPointsIsADraw()
        {
            _zone.transform.position = new Vector3(-20f, 0f, -20f);
            Begin();

            LogAssert.Expect(LogType.Log, "[Domination] ended winner=draw A=0 B=0");
            _runner.Tick(TimeLimit);

            Assert.That(_runner.Domination.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Domination.Winner, Is.Null);
        }

        [Test]
        public void WinningRaisesThePlayersResultOnce()
        {
            var results = RecordResults();
            Begin();
            _runner.Tick(CaptureSeconds);
            Assert.That(results, Is.Empty, "nothing is raised before the end");

            _runner.Tick(3f);

            Assert.That(results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Won, 0, 0, ScoreLimit) }));
        }

        [Test]
        public void LosingRaisesThePlayersDeathAndAlphasScore()
        {
            _zone.transform.position = EnemyStart;
            var results = RecordResults();
            Begin();

            using (CombatEvents.Attribute(_enemy.gameObject, "rifle"))
            {
                _world.PlayerDamageable.Kill();
            }

            _runner.Tick(CaptureSeconds);
            _runner.Tick(3f);

            Assert.That(_runner.Domination.Winner, Is.EqualTo((Team?)Team.Bravo));
            Assert.That(results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Lost, 0, 1, 0) }));
        }

        [Test]
        public void ATimeUpWithNoPointsRaisesADraw()
        {
            _zone.transform.position = new Vector3(-20f, 0f, -20f);
            var results = RecordResults();
            Begin();

            using (CombatEvents.Attribute(_world.Player, "rifle"))
            {
                _enemyBody.Kill();
            }

            _runner.Tick(TimeLimit);

            Assert.That(_runner.Domination.Winner, Is.Null);
            Assert.That(results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Draw, 1, 0, 0) }));
        }

        [Test]
        public void NothingIsRaisedAgainAfterTheEnd()
        {
            var results = RecordResults();
            Begin();
            _runner.Tick(CaptureSeconds);
            _runner.Tick(3f);

            _runner.Tick(TimeLimit);
            using (CombatEvents.Attribute(_world.Player, "rifle"))
            {
                _enemyBody.Kill();
            }

            _runner.Tick(1f);

            Assert.That(results, Is.EqualTo(new[] { new MatchResult(MatchOutcome.Won, 0, 0, ScoreLimit) }));
        }

        [Test]
        public void EnemyShotByThePlayerCountsAsAKillAndRespawnsLater()
        {
            Begin();
            _enemy.transform.position = new Vector3(-10f, 1f, 5f);

            LogAssert.Expect(LogType.Log, "[Domination] kill killer=0 victim=1");
            using (CombatEvents.Attribute(_world.Player, "rifle"))
            {
                _enemyBody.Kill();
            }

            var domination = _runner.Domination;
            Assert.That(domination.GetKills(DominationRunner.PlayerId), Is.EqualTo(1));
            Assert.That(domination.GetDeaths(1), Is.EqualTo(1));
            Assert.That(_enemy.Brain.IsDead, Is.True);

            _runner.Tick(RespawnSeconds - 0.5f);
            Assert.That(_enemyBody.IsDead, Is.True, "the enemy waits before coming back");

            LogAssert.Expect(LogType.Log, "[Domination] respawn player=1 team=Bravo at=(20.00, 0.00, 20.00)");
            _runner.Tick(1f);

            Assert.That(_enemyBody.IsDead, Is.False);
            Assert.That(_enemyBody.Health.Current, Is.EqualTo(_enemyBody.Health.Max));
            Assert.That(_enemy.Brain.IsDead, Is.False);
            Assert.That(_enemy.Weapon.AmmoInMagazine, Is.EqualTo(_enemy.Weapon.Spec.MagazineSize));
        }

        [Test]
        public void PlayerShotByTheEnemyCountsForTheEnemyAndCannotHoldThePoint()
        {
            Begin();

            LogAssert.Expect(LogType.Log, "[Domination] kill killer=1 victim=0");
            using (CombatEvents.Attribute(_enemy.gameObject, "rifle"))
            {
                _world.PlayerDamageable.Kill();
            }

            var domination = _runner.Domination;
            Assert.That(domination.GetKills(1), Is.EqualTo(1));
            Assert.That(domination.GetDeaths(DominationRunner.PlayerId), Is.EqualTo(1));
            Assert.That(_world.Mover.enabled, Is.False, "a dead player cannot move");
            Assert.That(_world.Weapon.enabled, Is.False, "a dead player cannot shoot");

            _runner.Tick(1f);
            Assert.That(domination.GetPlayerPoint(DominationRunner.PlayerId), Is.Null, "a dead player is not on the point");
            Assert.That(domination.GetOwner(0), Is.Null);
        }

        [Test]
        public void DeadPlayerRespawnsAtTheAlphaSpawnPoint()
        {
            var spawn = new GameObject("AlphaSpawn").transform;
            _created.Add(spawn.gameObject);
            spawn.position = new Vector3(3f, 0f, -4f);
            _runner.SetSpawnPoints(Team.Alpha, new[] { spawn });
            Begin();

            LogAssert.Expect(LogType.Log, "[Domination] death player=0");
            _world.PlayerDamageable.Kill();

            LogAssert.Expect(LogType.Log, "[Domination] respawn player=0 team=Alpha at=(3.00, 0.00, -4.00)");
            _runner.Tick(RespawnSeconds);

            Assert.That(_world.PlayerDamageable.IsDead, Is.False);
            Assert.That(_world.Player.transform.position, Is.EqualTo(new Vector3(3f, GameBootstrap.PlayerHeight / 2f, -4f)));
            Assert.That(_world.Mover.enabled, Is.True);
            Assert.That(_world.Weapon.enabled, Is.True);
        }

        [Test]
        public void BeginAndTickPublishTheScoresTimeAndPoints()
        {
            var published = new List<DominationStatus>();
            GameSignals.Domination.Changed += published.Add;

            Begin();

            Assert.That(published, Has.Count.EqualTo(1), "Begin publishes once");
            var status = GameSignals.Domination.Value;
            Assert.That(status, Is.SameAs(published[0]));
            Assert.That(status, Is.SameAs(_runner.Status));
            Assert.That(status.AlphaScore, Is.EqualTo(0f));
            Assert.That(status.BravoScore, Is.EqualTo(0f));
            Assert.That(status.RemainingSeconds, Is.EqualTo(TimeLimit));
            Assert.That(status.Points, Is.EqualTo(new[] { new ControlPointStatus(null, 0, false) }));
            Assert.That(status.IsEnded, Is.False);

            LogAssert.Expect(LogType.Log, "[Domination] captured point=A team=Alpha");
            _runner.Tick(CaptureSeconds);
            _runner.Tick(1.5f);

            Assert.That(published, Has.Count.EqualTo(3), "each tick publishes");
            status = GameSignals.Domination.Value;
            Assert.That(status.AlphaScore, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(status.RemainingSeconds, Is.EqualTo(TimeLimit - CaptureSeconds - 1.5f).Within(1e-4f));
            Assert.That(status.Points, Is.EqualTo(new[] { new ControlPointStatus(Team.Alpha, ControlPointStatus.FullPercent, false) }));

            _runner.Tick(10f);

            status = GameSignals.Domination.Value;
            Assert.That(status.IsEnded, Is.True);
            Assert.That(status.Winner, Is.EqualTo(Team.Alpha));

            _runner.Tick(1f);
            Assert.That(published, Has.Count.EqualTo(4), "an ended match no longer publishes");
            GameSignals.Domination.Changed -= published.Add;
        }

        [Test]
        public void StopClearsThePublishedStatus()
        {
            Begin();

            _runner.Stop();

            Assert.That(GameSignals.Domination.Value, Is.Null);
            Assert.That(_runner.Status, Is.Null);
        }

        [Test]
        public void StopKeepsAStatusPublishedByAnotherRunner()
        {
            Begin();
            var other = new DominationStatus(0f, 0f, 1f, null, false, null);
            GameSignals.Domination.Publish(other);

            _runner.Stop();

            Assert.That(GameSignals.Domination.Value, Is.SameAs(other));
        }

        [Test]
        public void BeginPublishesTheParticipantsOnTheScoreboard()
        {
            Begin();

            var entries = GameSignals.Scoreboard.Value;
            Assert.That(entries, Is.SameAs(_runner.PublishedScoreboard));
            Assert.That(entries, Is.EqualTo(new[]
            {
                new ScoreboardEntry(DominationRunner.PlayerId, ScoreboardName.LocalPlayer, Team.Alpha, 0, 0, ScoreboardConnection.None, true),
                new ScoreboardEntry(1, ScoreboardName.Literal("enemy-1"), Team.Bravo, 0, 0, ScoreboardConnection.None, false),
            }));
        }

        [Test]
        public void AKillPublishesTheNewCounts()
        {
            Begin();
            var before = GameSignals.Scoreboard.Value;

            LogAssert.Expect(LogType.Log, "[Domination] kill killer=0 victim=1");
            using (CombatEvents.Attribute(_world.Player, "rifle"))
            {
                _enemyBody.Kill();
            }

            var entries = GameSignals.Scoreboard.Value;
            Assert.That(entries, Is.Not.SameAs(before), "each publish is a new list");
            Assert.That(entries[0].Kills, Is.EqualTo(1));
            Assert.That(entries[0].Deaths, Is.Zero);
            Assert.That(entries[1].Kills, Is.Zero);
            Assert.That(entries[1].Deaths, Is.EqualTo(1));
        }

        [Test]
        public void StopClearsThePublishedScoreboard()
        {
            Begin();

            _runner.Stop();

            Assert.That(GameSignals.Scoreboard.Value, Is.Null);
            Assert.That(_runner.PublishedScoreboard, Is.Null);
        }

        [Test]
        public void StopKeepsAScoreboardPublishedByAnotherRunner()
        {
            Begin();
            var other = new[]
            {
                new ScoreboardEntry(7, ScoreboardName.Literal("other"), Team.Alpha, 0, 0, ScoreboardConnection.None, true),
            };
            GameSignals.Scoreboard.Publish(other);

            _runner.Stop();

            Assert.That(GameSignals.Scoreboard.Value, Is.SameAs(other));
        }

        [Test]
        public void DuplicatePointIndicesAreRejected()
        {
            var second = CreateZone("PointB", 0, new Vector3(30f, 0f, 0f));

            var e = Assert.Throws<ArgumentException>(
                () => _runner.Begin(_world, new[] { _zone, second }, new[] { _enemy }, _runner.CreateSpec(2)));
            StringAssert.Contains("used more than once", e.Message);
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void PointCountMustMatchTheSpec()
        {
            Assert.Throws<ArgumentException>(
                () => _runner.Begin(_world, new[] { _zone }, new[] { _enemy }, _runner.CreateSpec(2)));
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void InvalidSettingsAreRejectedBySpec()
        {
            _runner.ScoreLimit = 0;

            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreateSpec(1));
        }

        [Test]
        public void TheTuningAssetDecidesTheCaptureTimeAndTheScoreLimit()
        {
            var tuning = Tuning();
            tuning.CaptureSeconds = 2f;
            tuning.PointsPerSecond = 5f;
            tuning.ScoreLimit = 10;
            tuning.TimeLimitSeconds = TimeLimit;
            _runner.Definition = tuning;

            Begin();

            _runner.Tick(CaptureSeconds);
            Assert.That(_runner.Domination.GetOwner(0), Is.Null, "the runner's own one second is not used");

            LogAssert.Expect(LogType.Log, "[Domination] captured point=A team=Alpha");
            _runner.Tick(1f);
            Assert.That(_runner.Domination.GetOwner(0), Is.EqualTo(Team.Alpha));

            LogAssert.Expect(LogType.Log, "[Domination] state=Ended A=10 B=0 remaining=56.0");
            LogAssert.Expect(LogType.Log, "[Domination] ended winner=Alpha A=10 B=0");
            _runner.Tick(2f);

            Assert.That(_runner.Domination.Winner, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void TheTuningAssetDecidesTheRespawnSeconds()
        {
            var tuning = Tuning();
            tuning.TimeLimitSeconds = TimeLimit;
            tuning.RespawnSeconds = RespawnSeconds * 2f;
            _runner.Definition = tuning;
            Assert.That(_runner.EffectiveRespawnSeconds, Is.EqualTo(RespawnSeconds * 2f));

            Begin();

            LogAssert.Expect(LogType.Log, "[Domination] death player=0");
            _world.PlayerDamageable.Kill();

            _runner.Tick(RespawnSeconds);
            Assert.That(_world.PlayerDamageable.IsDead, Is.True, "the runner's own respawn seconds are not used");

            _runner.Tick(RespawnSeconds);
            Assert.That(_world.PlayerDamageable.IsDead, Is.False);
        }

        [Test]
        public void WithoutTuningTheRunnerUsesItsOwnValues()
        {
            Assert.That(_runner.Definition, Is.Null);
            Assert.That(_runner.EffectiveRespawnSeconds, Is.EqualTo(RespawnSeconds));

            var spec = _runner.CreateSpec(2);

            Assert.That(spec.PointCount, Is.EqualTo(2));
            Assert.That(spec.CaptureSeconds, Is.EqualTo(CaptureSeconds));
            Assert.That(spec.PointsPerSecond, Is.EqualTo(PointsPerSecond));
            Assert.That(spec.ScoreLimit, Is.EqualTo(ScoreLimit));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(TimeLimit));
        }

        [Test]
        public void ANewTuningAssetHoldsTheDefaults()
        {
            var tuning = Tuning();
            var spec = tuning.ToSpec(3);

            Assert.That(spec.PointCount, Is.EqualTo(3));
            Assert.That(spec.CaptureSeconds, Is.EqualTo(DominationRunner.DefaultCaptureSeconds));
            Assert.That(spec.PointsPerSecond, Is.EqualTo(DominationRunner.DefaultPointsPerSecond));
            Assert.That(spec.ScoreLimit, Is.EqualTo(DominationRunner.DefaultScoreLimit));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(DominationRunner.DefaultTimeLimitSeconds));
            Assert.That(spec.MaxCapturers, Is.EqualTo(DominationSpec.DefaultMaxCapturers));
            Assert.That(tuning.RespawnSeconds, Is.EqualTo(DominationRunner.DefaultRespawnSeconds));
            Assert.DoesNotThrow(tuning.Validate);
        }

        [Test]
        public void AnInvalidTuningAssetIsRejectedWithItsName()
        {
            var tuning = Tuning();
            tuning.ScoreLimit = 0;
            _runner.Definition = tuning;

            var e = Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreateSpec(1));
            StringAssert.Contains("'DominationTuning'", e.Message);
            Assert.That(e.ParamName, Is.EqualTo("scoreLimit"));

            tuning.ScoreLimit = 1;
            tuning.RespawnSeconds = -1f;
            Assert.Throws<ArgumentOutOfRangeException>(tuning.Validate);
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreateSpec(1));
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void CopyFromTakesEveryValueOfTheTuningAsset()
        {
            var tuning = Tuning();
            tuning.CaptureSeconds = 3f;
            tuning.PointsPerSecond = 4f;
            tuning.ScoreLimit = 50;
            tuning.TimeLimitSeconds = 90f;
            tuning.MaxCapturers = 2;
            tuning.RespawnSeconds = 6f;

            _runner.CopyFrom(tuning);

            Assert.That(_runner.CaptureSeconds, Is.EqualTo(3f));
            Assert.That(_runner.PointsPerSecond, Is.EqualTo(4f));
            Assert.That(_runner.ScoreLimit, Is.EqualTo(50));
            Assert.That(_runner.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(_runner.MaxCapturers, Is.EqualTo(2));
            Assert.That(_runner.RespawnSeconds, Is.EqualTo(6f));
            Assert.That(_runner.Definition, Is.Null, "copying does not wire the asset");
            Assert.Throws<ArgumentNullException>(() => _runner.CopyFrom(null));
        }

        [Test]
        public void ZoneScanCountsOnlyLivingCandidatesInside()
        {
            var inside = _world.PlayerDamageable;
            _zone.Scan(new[] { inside, _enemyBody, null });

            Assert.That(_zone.Occupants, Is.EqualTo(new[] { inside }));

            inside.Kill();
            _zone.Scan(new[] { inside });
            Assert.That(_zone.Occupants, Is.Empty);
        }

        private void Begin()
        {
            LogAssert.Expect(LogType.Log, "[Domination] joined player=0 team=Alpha name=" + _world.PlayerDamageable.Id);
            LogAssert.Expect(LogType.Log, "[Domination] joined player=1 team=Bravo name=enemy-1");
            LogAssert.Expect(LogType.Log, "[Domination] state=Playing A=0 B=0 remaining=60.0");
            _runner.Begin(_world, new[] { _zone }, new[] { _enemy }, _runner.CreateSpec(1));
        }

        private static List<MatchResult> RecordResults()
        {
            var results = new List<MatchResult>();
            CombatEvents.MatchFinished += results.Add;
            return results;
        }

        private DominationDefinition Tuning()
        {
            var tuning = ScriptableObject.CreateInstance<DominationDefinition>();
            tuning.name = "DominationTuning";
            _created.Add(tuning);
            return tuning;
        }

        private ControlPointZone CreateZone(string name, int index, Vector3 position)
        {
            var zoneObject = new GameObject(name);
            _created.Add(zoneObject);
            zoneObject.transform.position = position;
            var zone = zoneObject.AddComponent<ControlPointZone>();
            zone.Index = index;
            zone.Radius = 5f;
            return zone;
        }
    }
}
