using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="DominationRunner"/> が、拠点にいるプレイヤーを Domination に伝えて占領・得点・終了まで進め、
    /// キルとデスを渡し、倒れた側を出現点へ戻し、HUD に <see cref="DominationView"/> を出すことを確かめる。
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

            _host = new GameObject("Bootstrap");
            _created.Add(_host);
            var bootstrap = _host.AddComponent<GameBootstrap>();
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            _world = bootstrap.Build();

            // 拠点はプレイヤーの足元 (原点) に置く。
            _zone = CreateZone("PointA", 0, Vector3.zero);

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
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

            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            CombatEvents.ClearSubscribers();
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
        public void HudShowsScoresTimeAndOwners()
        {
            Begin();

            var view = _world.Hud.gameObject.GetComponent<DominationView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(_runner.View, Is.SameAs(view));
            Assert.That(view.AlphaScoreText, Is.EqualTo("ALPHA 0"));
            Assert.That(view.BravoScoreText, Is.EqualTo("BRAVO 0"));
            Assert.That(view.TimeText, Is.EqualTo(HudFormat.Time(TimeLimit)));
            Assert.That(view.PointTexts, Is.EqualTo(new[] { "A --" }));
            Assert.That(view.ResultText, Is.Empty);

            LogAssert.Expect(LogType.Log, "[Domination] captured point=A team=Alpha");
            _runner.Tick(CaptureSeconds);
            _runner.Tick(1.5f);
            view.Refresh();

            Assert.That(view.AlphaScoreText, Is.EqualTo("ALPHA 15"));
            Assert.That(view.TimeText, Is.EqualTo("00:58"));
            Assert.That(view.PointTexts, Is.EqualTo(new[] { "A ALPHA" }));

            _runner.Tick(10f);
            Assert.That(view.ResultText, Is.EqualTo(Localizer.Resolve(HudFormat.Result(Team.Alpha))));
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
