using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class TeamDeathmatchSessionTests
    {
        private const int _maxHealth = 100;

        private static readonly Vector3 _alphaBase = new Vector3(-20f, 0f, 0f);
        private static readonly Vector3 _bravoBase = new Vector3(20f, 0f, 0f);

        private Dictionary<int, Vector3> _positions;
        private List<string> _events;
        private List<int> _downedChanged;

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Events(params string[] expected) => expected;

        private static int[] Ids(params int[] expected) => expected;

        [SetUp]
        public void SetUp()
        {
            _positions = new Dictionary<int, Vector3>();
            _events = new List<string>();
            _downedChanged = new List<int>();
        }

        private TeamDeathmatchSession Create(
            int scoreLimit = 10,
            float timeLimitSeconds = 60f,
            float respawnSeconds = 3f,
            float? restartDelaySeconds = null,
            GameRules rules = null,
            FriendlyFire friendlyFire = FriendlyFire.Off)
        {
            var points = new[]
            {
                new SpawnPoint(_alphaBase, Team.Alpha),
                new SpawnPoint(_bravoBase, Team.Bravo),
            };
            var d = GameRules.Default;
            rules ??= new GameRules(
                friendlyFire,
                d.RespawnMode,
                d.WaveIntervalSeconds,
                d.RegenDelaySeconds,
                d.RegenPerSecond,
                d.DeathBody,
                d.DeathCamera,
                d.InfiniteReserve,
                d.ReserveAmmoMultiplier,
                d.Destruction);
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(scoreLimit, timeLimitSeconds, respawnSeconds, rules),
                _maxHealth,
                points,
                new SpawnSelector(),
                id => _positions[id],
                restartDelaySeconds);

            // ネットワーク側と同じく、出現の知らせを受けたら位置を動かす
            session.Spawned += (id, point) =>
            {
                _positions[id] = point.Position;
                _events.Add("spawn " + id);
            };
            session.Started += () => _events.Add("started");
            session.Restarted += () => _events.Add("restarted");
            session.Killed += (killer, victim) => _events.Add("kill " + killer + ">" + victim);
            session.ScoreChanged += (a, b) => _events.Add("score " + a + "-" + b);
            session.Ended += winner => _events.Add("ended " + (winner?.ToString() ?? "draw"));
            session.Downed += (attacker, victim) => _events.Add("down " + attacker + ">" + victim);
            session.Revived += (reviver, target) => _events.Add("revive " + reviver + ">" + target);
            session.DownedChanged += id => _downedChanged.Add(id);
            return session;
        }

        [Test]
        public void AssignsTeamsAlternatelyInJoinOrder()
        {
            var session = Create();

            Assert.That(session.Join(5), Is.EqualTo(Team.Alpha));
            Assert.That(session.Join(3), Is.EqualTo(Team.Bravo));
            Assert.That(session.Join(9), Is.EqualTo(Team.Alpha));
            Assert.That(session.GetTeam(3), Is.EqualTo(Team.Bravo));
            Assert.That(session.PlayerCount, Is.EqualTo(3));
        }

        [Test]
        public void SpawnsJoiningPlayerAtOwnTeamPoint()
        {
            var session = Create();

            session.Join(1);
            session.Join(2);

            Assert.That(_positions[1], Is.EqualTo(_alphaBase));
            Assert.That(_positions[2], Is.EqualTo(_bravoBase));
            Assert.That(session.GetHealth(1), Is.EqualTo(_maxHealth));
        }

        [Test]
        public void StartsWhenBothTeamsHavePlayers()
        {
            var session = Create();

            session.Join(1);
            Assert.That(session.State, Is.EqualTo(GameModeState.Waiting));

            session.Join(2);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_events, Is.EqualTo(Events("spawn 1", "spawn 2", "started")));
        }

        [Test]
        public void RejectsJoiningTwiceWithoutConsumingATeam()
        {
            var session = Create();
            session.Join(1);

            Assert.Throws<ArgumentException>(() => session.Join(1));
            Assert.That(session.NextTeam, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void DamageReducesHealthWithoutKilling()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);

            Assert.That(session.ApplyDamage(1, 2, 40), Is.EqualTo(DamageOutcome.Damaged));
            Assert.That(session.GetHealth(2), Is.EqualTo(60));
            Assert.That(session.GetScore(Team.Alpha), Is.Zero);
        }

        [Test]
        public void LethalDamageReportsKillAndScores()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            _events.Clear();

            Assert.That(session.ApplyDamage(1, 2, 150), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(session.IsAlive(2), Is.False);
            Assert.That(session.GetKills(1), Is.EqualTo(1));
            Assert.That(session.GetDeaths(2), Is.EqualTo(1));
            Assert.That(session.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(_events, Is.EqualTo(Events("kill 1>2", "score 1-0")));
        }

        [Test]
        public void IgnoresDamageToDeadPlayer()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, _maxHealth);

            Assert.That(session.ApplyDamage(1, 2, _maxHealth), Is.EqualTo(DamageOutcome.Ignored));
            Assert.That(session.GetKills(1), Is.EqualTo(1));
        }

        [Test]
        public void IgnoresFriendlyFireButCountsSuicide()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            session.Join(3);
            _events.Clear();

            Assert.That(session.ApplyDamage(1, 3, _maxHealth), Is.EqualTo(DamageOutcome.Ignored));
            Assert.That(session.GetHealth(3), Is.EqualTo(_maxHealth));

            Assert.That(session.ApplyDamage(1, 1, _maxHealth), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(session.GetDeaths(1), Is.EqualTo(1));
            Assert.That(session.GetScore(Team.Alpha), Is.Zero);
            Assert.That(session.GetScore(Team.Bravo), Is.Zero);
            Assert.That(_events, Is.EqualTo(Events("kill 1>1")));
        }

        [Test]
        public void FriendlyFireOffLeavesAllyAndStatsUntouched()
        {
            var session = Create(friendlyFire: FriendlyFire.Off);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            var changed = new List<int>();
            session.PlayerChanged += changed.Add;
            _events.Clear();

            Assert.That(session.ApplyDamage(1, 3, 40), Is.EqualTo(DamageOutcome.Ignored));
            Assert.That(session.ApplyDamage(1, 3, _maxHealth), Is.EqualTo(DamageOutcome.Ignored));

            Assert.That(session.GetHealth(1), Is.EqualTo(_maxHealth));
            Assert.That(session.GetHealth(3), Is.EqualTo(_maxHealth));
            Assert.That(session.GetDeaths(3), Is.Zero);
            Assert.That(session.GetKills(1), Is.Zero);
            Assert.That(changed, Is.Empty);
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void FriendlyFireOnDamagesAlly()
        {
            var session = Create(friendlyFire: FriendlyFire.On);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            var changed = new List<int>();
            session.PlayerChanged += changed.Add;

            Assert.That(session.ApplyDamage(1, 3, 40), Is.EqualTo(DamageOutcome.Damaged));

            Assert.That(session.GetHealth(3), Is.EqualTo(60));
            Assert.That(session.GetHealth(1), Is.EqualTo(_maxHealth));
            Assert.That(changed, Is.EqualTo(Ids(3)));
        }

        [Test]
        public void FriendlyFireOnKillCountsOnlyTheVictimsDeath()
        {
            var session = Create(scoreLimit: 1, friendlyFire: FriendlyFire.On);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            var changed = new List<int>();
            session.PlayerChanged += changed.Add;
            _events.Clear();

            Assert.That(session.ApplyDamage(1, 3, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(session.IsAlive(3), Is.False);
            Assert.That(session.GetDeaths(3), Is.EqualTo(1));
            Assert.That(session.GetKills(1), Is.Zero);
            Assert.That(session.GetDeaths(1), Is.Zero);
            Assert.That(session.GetScore(Team.Alpha), Is.Zero);
            Assert.That(session.GetScore(Team.Bravo), Is.Zero);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(changed, Is.EqualTo(Ids(3)));
            Assert.That(_events, Is.EqualTo(Events("kill 1>3")));
            Assert.That(session.ApplyDamage(1, 3, _maxHealth), Is.EqualTo(DamageOutcome.Ignored));
        }

        [Test]
        public void FriendlyFireOnInstantRespawnComesAfterKill()
        {
            var session = Create(respawnSeconds: 0f, friendlyFire: FriendlyFire.On);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            _events.Clear();

            session.ApplyDamage(1, 3, _maxHealth);

            Assert.That(_events, Is.EqualTo(Events("kill 1>3", "spawn 3")));
            Assert.That(session.GetHealth(3), Is.EqualTo(_maxHealth));
        }

        [Test]
        public void FriendlyFireOnStillScoresEnemyKills()
        {
            var session = Create(friendlyFire: FriendlyFire.On);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            _events.Clear();

            Assert.That(session.ApplyDamage(1, 2, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(session.GetKills(1), Is.EqualTo(1));
            Assert.That(session.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(_events, Is.EqualTo(Events("kill 1>2", "score 1-0")));
        }

        [Test]
        public void FriendlyFireReflectDamagesTheAttackerInstead()
        {
            var session = Create(friendlyFire: FriendlyFire.Reflect);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            var changed = new List<int>();
            session.PlayerChanged += changed.Add;

            Assert.That(session.ApplyDamage(1, 3, 40), Is.EqualTo(DamageOutcome.Damaged));

            Assert.That(session.GetHealth(1), Is.EqualTo(60));
            Assert.That(session.GetHealth(3), Is.EqualTo(_maxHealth));
            Assert.That(changed, Is.EqualTo(Ids(1)));
        }

        [Test]
        public void FriendlyFireReflectKillsTheAttackerAsADeathOnly()
        {
            var session = Create(friendlyFire: FriendlyFire.Reflect);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            var changed = new List<int>();
            session.PlayerChanged += changed.Add;
            _events.Clear();

            Assert.That(session.ApplyDamage(1, 3, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(session.IsAlive(1), Is.False);
            Assert.That(session.IsAlive(3), Is.True);
            Assert.That(session.GetHealth(3), Is.EqualTo(_maxHealth));
            Assert.That(session.GetDeaths(1), Is.EqualTo(1));
            Assert.That(session.GetKills(1), Is.Zero);
            Assert.That(session.GetDeaths(3), Is.Zero);
            Assert.That(session.GetScore(Team.Alpha), Is.Zero);
            Assert.That(session.GetScore(Team.Bravo), Is.Zero);
            Assert.That(changed, Is.EqualTo(Ids(1)));
            Assert.That(_events, Is.EqualTo(Events("kill 1>1")));
        }

        [Test]
        public void FriendlyFireReflectInstantRespawnComesAfterKill()
        {
            var session = Create(respawnSeconds: 0f, friendlyFire: FriendlyFire.Reflect);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            _events.Clear();

            session.ApplyDamage(1, 3, _maxHealth);

            Assert.That(_events, Is.EqualTo(Events("kill 1>1", "spawn 1")));
            Assert.That(session.GetHealth(1), Is.EqualTo(_maxHealth));
        }

        [Test]
        public void FriendlyFireReflectIgnoresShotsFromADeadAttacker()
        {
            var session = Create(friendlyFire: FriendlyFire.Reflect);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            session.ApplyDamage(2, 1, _maxHealth);
            _events.Clear();

            Assert.That(session.ApplyDamage(1, 3, _maxHealth), Is.EqualTo(DamageOutcome.Ignored));

            Assert.That(session.GetDeaths(1), Is.EqualTo(1));
            Assert.That(session.GetHealth(3), Is.EqualTo(_maxHealth));
            Assert.That(_events, Is.Empty);
        }

        [TestCase(FriendlyFire.On)]
        [TestCase(FriendlyFire.Reflect)]
        public void SuicideIsCountedTheSameInEveryFriendlyFireMode(FriendlyFire friendlyFire)
        {
            var session = Create(friendlyFire: friendlyFire);
            session.Join(1);
            session.Join(2);
            _events.Clear();

            Assert.That(session.ApplyDamage(1, 1, 40), Is.EqualTo(DamageOutcome.Damaged));
            Assert.That(session.GetHealth(1), Is.EqualTo(60));

            Assert.That(session.ApplyDamage(1, 1, _maxHealth), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(session.GetDeaths(1), Is.EqualTo(1));
            Assert.That(session.GetKills(1), Is.Zero);
            Assert.That(session.GetScore(Team.Alpha), Is.Zero);
            Assert.That(session.GetScore(Team.Bravo), Is.Zero);
            Assert.That(_events, Is.EqualTo(Events("kill 1>1")));
        }

        [Test]
        public void IgnoresDamageBeforeStart()
        {
            var session = Create();
            session.Join(1);

            Assert.That(session.ApplyDamage(1, 1, _maxHealth), Is.EqualTo(DamageOutcome.Ignored));
            Assert.That(session.IsAlive(1), Is.True);
        }

        [Test]
        public void IgnoresZeroDamageAndRejectsNegative()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);

            Assert.That(session.ApplyDamage(1, 2, 0), Is.EqualTo(DamageOutcome.Ignored));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ApplyDamage(1, 2, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ApplyDamage(1, 99, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ApplyDamage(99, 2, 10));
        }

        [Test]
        public void RespawnsWithFullHealthAfterRespawnSeconds()
        {
            var session = Create(respawnSeconds: 3f);
            session.Join(1);
            session.Join(2);
            _positions[2] = new Vector3(0f, 0f, 0f);
            session.ApplyDamage(1, 2, _maxHealth);
            _events.Clear();

            session.Tick(2.9f);
            Assert.That(session.IsAlive(2), Is.False);
            Assert.That(_events, Is.Empty);

            session.Tick(0.2f);
            Assert.That(session.GetHealth(2), Is.EqualTo(_maxHealth));
            Assert.That(_positions[2], Is.EqualTo(_bravoBase));
            Assert.That(_events, Is.EqualTo(Events("spawn 2")));
        }

        [Test]
        public void InstantRespawnComesAfterKillAndScore()
        {
            var session = Create(respawnSeconds: 0f);
            session.Join(1);
            session.Join(2);
            _events.Clear();

            session.ApplyDamage(1, 2, _maxHealth);

            Assert.That(_events, Is.EqualTo(Events("kill 1>2", "score 1-0", "spawn 2")));
            Assert.That(session.IsAlive(2), Is.True);
        }

        private static GameRules RegenRules(float regenDelaySeconds, float regenPerSecond)
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                regenDelaySeconds,
                regenPerSecond,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off);
        }

        [Test]
        public void RegeneratesAfterDelayAndRaisesPlayerChanged()
        {
            var session = Create(rules: RegenRules(2f, 10f));
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, 50);
            var changed = new List<int>();
            session.PlayerChanged += changed.Add;

            session.Tick(2f);
            Assert.That(session.GetHealth(2), Is.EqualTo(50));
            Assert.That(changed, Is.Empty);

            session.Tick(1f);
            Assert.That(session.GetHealth(2), Is.EqualTo(60));
            Assert.That(changed, Is.EqualTo(Ids(2)));
        }

        [Test]
        public void DamageResetsRegenDelay()
        {
            var session = Create(rules: RegenRules(2f, 10f));
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, 50);
            session.Tick(1.5f);

            session.ApplyDamage(1, 2, 10);
            session.Tick(1.5f);
            Assert.That(session.GetHealth(2), Is.EqualTo(40));

            session.Tick(1f);
            Assert.That(session.GetHealth(2), Is.EqualTo(45));
        }

        [Test]
        public void DefaultRulesDoNotRegenerate()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, 40);
            var changed = new List<int>();
            session.PlayerChanged += changed.Add;

            session.Tick(30f);

            Assert.That(session.GetHealth(2), Is.EqualTo(60));
            Assert.That(changed, Is.Empty);
        }

        [Test]
        public void RespawnedPlayerRegeneratesWithNewHealth()
        {
            var session = Create(respawnSeconds: 3f, rules: RegenRules(2f, 10f));
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, _maxHealth);
            session.Tick(3.1f);
            Assert.That(session.GetHealth(2), Is.EqualTo(_maxHealth));

            session.ApplyDamage(1, 2, 50);
            session.Tick(2f);
            session.Tick(1f);

            Assert.That(session.GetHealth(2), Is.EqualTo(60));
        }

        [Test]
        public void DoesNotRegenerateAfterMatchEnds()
        {
            var session = Create(scoreLimit: 1, rules: RegenRules(0f, 10f));
            session.Join(1);
            session.Join(2);
            session.Join(3);
            session.ApplyDamage(2, 3, 50);
            session.ApplyDamage(2, 1, _maxHealth);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));

            session.Tick(10f);

            Assert.That(session.GetHealth(3), Is.EqualTo(50));
        }

        [Test]
        public void EndsWhenScoreLimitReached()
        {
            var session = Create(scoreLimit: 1);
            session.Join(1);
            session.Join(2);
            _events.Clear();

            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(_events, Is.EqualTo(Events("kill 2>1", "score 0-1", "ended Bravo")));
            Assert.That(session.ApplyDamage(1, 2, _maxHealth), Is.EqualTo(DamageOutcome.Ignored));
        }

        [Test]
        public void EndsInDrawWhenTimeRunsOutWithEqualScores()
        {
            var session = Create(timeLimitSeconds: 5f);
            session.Join(1);
            session.Join(2);
            _events.Clear();

            session.Tick(5f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.Null);
            Assert.That(_events, Is.EqualTo(Events("ended draw")));
        }

        [Test]
        public void LeavingCancelsPendingRespawnAndKeepsScore()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, _maxHealth);
            _events.Clear();

            session.Leave(2);
            session.Tick(10f);

            Assert.That(session.HasPlayer(2), Is.False);
            Assert.That(session.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(_events, Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Leave(2));
        }

        [Test]
        public void RespawnAvoidsEnemiesNearOwnBase()
        {
            var points = new[]
            {
                new SpawnPoint(new Vector3(-20f, 0f, 0f), Team.Alpha),
                new SpawnPoint(new Vector3(-20f, 0f, 40f), Team.Alpha),
                new SpawnPoint(_bravoBase, Team.Bravo),
            };
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(10, 60f, 1f), _maxHealth, points, new SpawnSelector(), id => _positions[id]);
            SpawnPoint lastSpawn = null;
            session.Spawned += (id, point) =>
            {
                _positions[id] = point.Position;
                lastSpawn = point;
            };
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(2, 1, _maxHealth);

            // 敵が最初の Alpha 地点のすぐそばに来ている
            _positions[2] = new Vector3(-21f, 0f, 0f);
            session.Tick(1f);

            Assert.That(lastSpawn, Is.SameAs(points[1]));
        }

        [Test]
        public void FindsNearestLivingEnemy()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            session.Join(3);
            session.Join(4);
            _positions[1] = new Vector3(0f, 0f, 0f);
            _positions[2] = new Vector3(5f, 0f, 0f);
            _positions[4] = new Vector3(3f, 0f, 0f);

            Assert.That(session.FindNearestEnemy(1), Is.EqualTo(4));

            session.ApplyDamage(1, 4, _maxHealth);
            Assert.That(session.FindNearestEnemy(1), Is.EqualTo(2));

            session.ApplyDamage(1, 2, _maxHealth);
            Assert.That(session.FindNearestEnemy(1), Is.Null);
        }

        [Test]
        public void FindsNoEnemyForDeadPlayerOrBeforeStart()
        {
            var session = Create();
            session.Join(1);
            Assert.That(session.FindNearestEnemy(1), Is.Null);

            session.Join(2);
            Assert.That(session.FindNearestEnemy(1), Is.EqualTo(2));

            session.ApplyDamage(2, 1, _maxHealth);
            Assert.That(session.FindNearestEnemy(1), Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.FindNearestEnemy(99));
        }

        [Test]
        public void RestartsAfterDelayWithInitialScoresKillsAndTime()
        {
            var session = Create(scoreLimit: 1, timeLimitSeconds: 60f, restartDelaySeconds: 10f);
            session.Join(1);
            session.Join(2);
            session.Tick(5f);
            session.ApplyDamage(1, 2, _maxHealth);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.IsRestartPending, Is.True);
            Assert.That(session.RestartRemainingSeconds, Is.EqualTo(10f));

            session.Tick(9.5f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.RestartRemainingSeconds, Is.EqualTo(0.5f));

            _events.Clear();
            session.Tick(0.5f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.Winner, Is.Null);
            Assert.That(session.IsRestartPending, Is.False);
            Assert.That(session.RestartRemainingSeconds, Is.Zero);
            Assert.That(session.RemainingSeconds, Is.EqualTo(60f));
            Assert.That(session.GetScore(Team.Alpha), Is.Zero);
            Assert.That(session.GetScore(Team.Bravo), Is.Zero);
            Assert.That(session.GetKills(1), Is.Zero);
            Assert.That(session.GetDeaths(2), Is.Zero);
            Assert.That(_events, Is.EqualTo(Events("restarted", "score 0-0", "spawn 1", "spawn 2", "started")));
        }

        [Test]
        public void RestartRespawnsEveryoneWithFullHealthAtOwnTeamPoint()
        {
            // 倒された 2 が終了までにリスポーンしないよう、リスポーン待ちを試合より長くする
            var session = Create(timeLimitSeconds: 5f, respawnSeconds: 10f, restartDelaySeconds: 1f);
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(2, 1, 30);
            session.ApplyDamage(1, 2, _maxHealth);
            _positions[1] = new Vector3(3f, 0f, 3f);
            _positions[2] = new Vector3(-3f, 0f, -3f);
            session.Tick(5f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.IsAlive(2), Is.False);

            session.Tick(1f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.GetHealth(1), Is.EqualTo(_maxHealth));
            Assert.That(session.GetHealth(2), Is.EqualTo(_maxHealth));
            Assert.That(session.IsAlive(2), Is.True);
            Assert.That(_positions[1], Is.EqualTo(_alphaBase));
            Assert.That(_positions[2], Is.EqualTo(_bravoBase));
        }

        [Test]
        public void RestartKeepsTeams()
        {
            var session = Create(scoreLimit: 1, restartDelaySeconds: 0f);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            session.ApplyDamage(2, 1, _maxHealth);

            session.Tick(0f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.GetTeam(1), Is.EqualTo(Team.Alpha));
            Assert.That(session.GetTeam(2), Is.EqualTo(Team.Bravo));
            Assert.That(session.GetTeam(3), Is.EqualTo(Team.Alpha));
            Assert.That(session.NextTeam, Is.EqualTo(Team.Bravo));
            Assert.That(session.PlayerCount, Is.EqualTo(3));
        }

        [Test]
        public void TickThatEndsTheMatchDoesNotCountTowardRestart()
        {
            var session = Create(timeLimitSeconds: 5f, restartDelaySeconds: 2f);
            session.Join(1);
            session.Join(2);

            session.Tick(6f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.RestartRemainingSeconds, Is.EqualTo(2f));

            session.Tick(1.5f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));

            session.Tick(0.5f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void RestartedMatchCanEndAndRestartAgain()
        {
            var session = Create(scoreLimit: 1, restartDelaySeconds: 1f);
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, _maxHealth);
            session.Tick(1f);
            _events.Clear();

            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(session.GetScore(Team.Alpha), Is.Zero);
            Assert.That(session.GetScore(Team.Bravo), Is.EqualTo(1));
            Assert.That(session.RestartRemainingSeconds, Is.EqualTo(1f));
            Assert.That(_events, Is.EqualTo(Events("kill 2>1", "score 0-1", "ended Bravo")));

            session.Tick(1f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void StaysEndedWithoutRestartDelay()
        {
            var session = Create(scoreLimit: 1);
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, _maxHealth);

            session.Tick(1000f);

            Assert.That(session.RestartDelaySeconds, Is.Null);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.IsRestartPending, Is.False);
            Assert.That(session.RestartRemainingSeconds, Is.Zero);
        }

        [Test]
        public void RestartWaitsForPlayersWhenATeamIsEmpty()
        {
            var session = Create(scoreLimit: 1, restartDelaySeconds: 1f);
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, _maxHealth);
            session.Leave(2);
            _events.Clear();

            session.Tick(1f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(session.IsRestartPending, Is.False);
            Assert.That(_events, Is.EqualTo(Events("restarted", "score 0-0", "spawn 1")));

            // 割り当ての順番は抜けた人がいても戻らないので、次の 2 人目で Bravo がそろう
            _events.Clear();
            Assert.That(session.Join(3), Is.EqualTo(Team.Alpha));
            Assert.That(session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(session.Join(4), Is.EqualTo(Team.Bravo));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_events, Is.EqualTo(Events("spawn 3", "spawn 4", "started")));
        }

        [Test]
        public void ManualRestartOnlyAfterEnd()
        {
            var session = Create(scoreLimit: 1);
            session.Join(1);
            session.Join(2);

            Assert.Throws<InvalidOperationException>(() => session.Restart());

            session.ApplyDamage(1, 2, _maxHealth);
            session.Restart();

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.GetScore(Team.Alpha), Is.Zero);
        }

        private static GameRules RulesWith(RespawnMode respawnMode, float waveIntervalSeconds = GameRules.DefaultWaveIntervalSeconds)
        {
            return new GameRules(
                FriendlyFire.Off, respawnMode, waveIntervalSeconds, 0f, 0f, DeathBody.Vanish, DeathCamera.None, false, 1f, Destruction.Off);
        }

        [Test]
        public void IndividualReportsRespawnRemaining()
        {
            var session = Create(respawnSeconds: 3f);
            session.Join(1);
            session.Join(2);

            Assert.That(session.GetRespawnRemaining(2), Is.Null);

            session.ApplyDamage(1, 2, _maxHealth);
            session.Tick(1f);

            Assert.That(session.GetRespawnRemaining(2), Is.EqualTo(2f));
            Assert.That(session.SecondsUntilNextWave, Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.GetRespawnRemaining(99));
        }

        [Test]
        public void WaveRespawnsDeadPlayersTogetherAtTheWave()
        {
            var session = Create(respawnSeconds: 3f, rules: RulesWith(RespawnMode.Wave, 10f));
            session.Join(1);
            session.Join(2);
            session.Join(3);
            session.Join(4);
            session.ApplyDamage(1, 2, _maxHealth);
            session.Tick(4f);
            session.ApplyDamage(3, 4, _maxHealth);
            _events.Clear();

            Assert.That(session.GetRespawnRemaining(2), Is.EqualTo(6f));
            Assert.That(session.GetRespawnRemaining(4), Is.EqualTo(6f));

            session.Tick(5f);
            Assert.That(_events, Is.Empty);
            Assert.That(session.IsAlive(2), Is.False);

            session.Tick(1f);

            Assert.That(_events, Is.EqualTo(Events("spawn 2", "spawn 4")));
            Assert.That(session.GetHealth(2), Is.EqualTo(_maxHealth));
            Assert.That(session.GetHealth(4), Is.EqualTo(_maxHealth));
        }

        [Test]
        public void WaveZeroRespawnSecondsDoesNotRespawnOnTheKill()
        {
            var session = Create(respawnSeconds: 0f, rules: RulesWith(RespawnMode.Wave, 10f));
            session.Join(1);
            session.Join(2);
            _events.Clear();

            session.ApplyDamage(1, 2, _maxHealth);

            Assert.That(_events, Is.EqualTo(Events("kill 1>2", "score 1-0")));
            Assert.That(session.IsAlive(2), Is.False);
            Assert.That(session.GetRespawnRemaining(2), Is.EqualTo(10f));
        }

        [Test]
        public void WaveRestartResetsWaveTimes()
        {
            var session = Create(scoreLimit: 1, respawnSeconds: 0f, restartDelaySeconds: 1f, rules: RulesWith(RespawnMode.Wave, 10f));
            session.Join(1);
            session.Join(2);
            session.Tick(8f);
            session.ApplyDamage(1, 2, _maxHealth);

            session.Tick(1f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.SecondsUntilNextWave, Is.EqualTo(10f));
        }

        [Test]
        public void NoneWipeOutEndsOnTheNextTick()
        {
            var session = Create(respawnSeconds: 0f, rules: RulesWith(RespawnMode.None));
            session.Join(1);
            session.Join(2);
            _events.Clear();

            session.ApplyDamage(1, 2, _maxHealth);

            Assert.That(_events, Is.EqualTo(Events("kill 1>2", "score 1-0")));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.IsEliminated(2), Is.True);
            Assert.That(session.GetRespawnRemaining(2), Is.Null);

            session.Tick(0f);

            Assert.That(_events, Is.EqualTo(Events("kill 1>2", "score 1-0", "ended Alpha")));
            Assert.That(session.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(session.IsAlive(2), Is.False);
        }

        [Test]
        public void NoneTradeKillInTheSameTickIsDraw()
        {
            var session = Create(rules: RulesWith(RespawnMode.None));
            session.Join(1);
            session.Join(2);

            session.ApplyDamage(1, 2, _maxHealth);
            Assert.That(session.ApplyDamage(2, 1, _maxHealth), Is.EqualTo(DamageOutcome.Killed));
            session.Tick(0.1f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.Null);
            Assert.That(_events[_events.Count - 1], Is.EqualTo("ended draw"));
        }

        [Test]
        public void NoneLeavingLastSurvivorEndsTheMatch()
        {
            var session = Create(rules: RulesWith(RespawnMode.None));
            session.Join(1);
            session.Join(2);
            session.Join(3);
            session.ApplyDamage(2, 1, _maxHealth);

            session.Leave(3);
            session.Tick(0f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void NoneRestartBringsEveryoneBack()
        {
            var session = Create(restartDelaySeconds: 2f, rules: RulesWith(RespawnMode.None));
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, _maxHealth);
            session.Tick(0f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));

            session.Tick(2f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.IsAlive(2), Is.True);
            Assert.That(session.IsEliminated(2), Is.False);

            session.Tick(1f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
        }

        // ceil(_maxHealth * 0.3)
        private const int _downedHealth = 30;

        private const float _bleedoutSeconds = 15f;

        private const float _reviveSeconds = 3f;

        private static GameRules DownedRules(
            FriendlyFire friendlyFire = FriendlyFire.Off,
            RespawnMode respawnMode = RespawnMode.Individual,
            float regenDelaySeconds = 0f,
            float regenPerSecond = 0f)
        {
            var actions = new ActionRules(
                Downed.Revive, _bleedoutSeconds, 0.3f, _reviveSeconds, Cover.Off, BlindFire.Off, ActiveReload.Off);
            return new GameRules(
                friendlyFire,
                respawnMode,
                GameRules.DefaultWaveIntervalSeconds,
                regenDelaySeconds,
                regenPerSecond,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off,
                actions);
        }

        /// <summary>Alpha に 1 と 3、Bravo に 2 と 4 を入れて始め、ここまでのイベントを消す。</summary>
        private TeamDeathmatchSession CreateTwoOnTwo(
            GameRules rules,
            int scoreLimit = 10,
            float respawnSeconds = 3f,
            float? restartDelaySeconds = null)
        {
            var session = Create(scoreLimit: scoreLimit, respawnSeconds: respawnSeconds, restartDelaySeconds: restartDelaySeconds, rules: rules);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            session.Join(4);
            _events.Clear();
            _downedChanged.Clear();
            return session;
        }

        [Test]
        public void DownedOffKillsImmediatelyAsBefore()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            session.Join(3);
            session.Join(4);
            _events.Clear();

            Assert.That(session.ApplyDamage(2, 1, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(_events, Is.EqualTo(Events("kill 2>1", "score 0-1")));
            Assert.That(session.IsAlive(1), Is.False);
            Assert.That(session.IsDowned(1), Is.False);
            Assert.That(session.GetDownedRemaining(1), Is.Null);
            Assert.That(session.GetReviveProgress(1), Is.Zero);
            Assert.That(session.TryRevive(3, 1, 1f), Is.False);
            Assert.That(_downedChanged, Is.Empty);
        }

        [Test]
        public void LethalDamageDownsInsteadOfKilling()
        {
            var session = CreateTwoOnTwo(DownedRules());

            Assert.That(session.ApplyDamage(2, 1, _maxHealth), Is.EqualTo(DamageOutcome.Downed));

            Assert.That(_events, Is.EqualTo(Events("down 2>1")));
            Assert.That(_downedChanged, Is.EqualTo(Ids(1)));
            Assert.That(session.IsAlive(1), Is.True);
            Assert.That(session.IsDowned(1), Is.True);
            Assert.That(session.GetHealth(1), Is.EqualTo(_downedHealth));
            Assert.That(session.GetDownedRemaining(1), Is.EqualTo(_bleedoutSeconds));
            Assert.That(session.GetScore(Team.Bravo), Is.Zero);
            Assert.That(session.GetDeaths(1), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.IsDowned(99));
        }

        [Test]
        public void DownedPlayerIsNotCountedAsStanding()
        {
            var session = CreateTwoOnTwo(DownedRules());
            session.ApplyDamage(1, 2, _maxHealth);

            Assert.That(session.FindNearestEnemy(1), Is.EqualTo(4));
            Assert.That(session.FindNearestEnemy(2), Is.Null);
        }

        [Test]
        public void FinishingADownedPlayerCreditsTheLastEnemy()
        {
            var session = CreateTwoOnTwo(DownedRules());
            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.ApplyDamage(4, 1, 10), Is.EqualTo(DamageOutcome.Damaged));
            Assert.That(session.GetHealth(1), Is.EqualTo(_downedHealth - 10));

            Assert.That(session.ApplyDamage(4, 1, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(_events, Is.EqualTo(Events("down 2>1", "kill 4>1", "score 0-1")));
            Assert.That(_downedChanged, Is.EqualTo(Ids(1, 1)));
            Assert.That(session.IsAlive(1), Is.False);
            Assert.That(session.IsDowned(1), Is.False);
            Assert.That(session.GetKills(4), Is.EqualTo(1));
            Assert.That(session.GetKills(2), Is.Zero);
            Assert.That(session.GetDeaths(1), Is.EqualTo(1));
            Assert.That(session.ApplyDamage(4, 1, 10), Is.EqualTo(DamageOutcome.Ignored));
        }

        [Test]
        public void BleedingOutCreditsTheEnemyWhoDowned()
        {
            var session = CreateTwoOnTwo(DownedRules());
            session.ApplyDamage(2, 1, _maxHealth);
            session.ApplyDamage(4, 1, 10);

            session.Tick(_bleedoutSeconds - 1f);
            Assert.That(session.IsDowned(1), Is.True);
            Assert.That(session.GetDownedRemaining(1), Is.EqualTo(1f));

            session.Tick(1f);

            Assert.That(_events, Is.EqualTo(Events("down 2>1", "kill 2>1", "score 0-1")));
            Assert.That(session.IsAlive(1), Is.False);
            Assert.That(session.GetKills(2), Is.EqualTo(1));
            Assert.That(session.GetKills(4), Is.Zero);
        }

        [Test]
        public void DownedPlayerDoesNotRegenerate()
        {
            var session = CreateTwoOnTwo(DownedRules(regenDelaySeconds: 0f, regenPerSecond: 10f));
            session.ApplyDamage(2, 1, _maxHealth);

            session.Tick(2f);

            Assert.That(session.GetHealth(1), Is.EqualTo(_downedHealth));
        }

        [Test]
        public void HoldingReviveRaisesTheDownedAlly()
        {
            var session = CreateTwoOnTwo(DownedRules());
            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.TryRevive(3, 1, 1f), Is.True);
            Assert.That(session.GetReviveProgress(1), Is.EqualTo(1f / _reviveSeconds).Within(1e-6f));
            session.Tick(1f);
            Assert.That(session.TryRevive(3, 1, 1f), Is.True);
            session.Tick(1f);
            Assert.That(session.TryRevive(3, 1, 1f), Is.True);

            Assert.That(_events, Is.EqualTo(Events("down 2>1", "revive 3>1")));
            Assert.That(_downedChanged, Is.EqualTo(Ids(1, 1)));
            Assert.That(session.IsDowned(1), Is.False);
            Assert.That(session.IsAlive(1), Is.True);
            Assert.That(session.GetHealth(1), Is.EqualTo(_downedHealth));
            Assert.That(session.GetDownedRemaining(1), Is.Null);
            Assert.That(session.GetReviveProgress(1), Is.Zero);
            Assert.That(session.GetDeaths(1), Is.Zero);
        }

        [Test]
        public void RevivedPlayerCanBeDownedAgain()
        {
            var session = CreateTwoOnTwo(DownedRules());
            session.ApplyDamage(2, 1, _maxHealth);
            session.TryRevive(3, 1, _reviveSeconds);

            Assert.That(session.ApplyDamage(2, 1, _downedHealth), Is.EqualTo(DamageOutcome.Downed));
            Assert.That(session.GetHealth(1), Is.EqualTo(_downedHealth));
            Assert.That(session.GetDownedRemaining(1), Is.EqualTo(_bleedoutSeconds));
        }

        [Test]
        public void StoppingReviveStartsOver()
        {
            var session = CreateTwoOnTwo(DownedRules());
            session.ApplyDamage(2, 1, _maxHealth);

            session.TryRevive(3, 1, 2f);
            session.Tick(1f);
            session.Tick(1f);

            Assert.That(session.GetReviveProgress(1), Is.Zero);

            session.TryRevive(3, 1, 2f);
            session.Tick(1f);
            Assert.That(session.IsDowned(1), Is.True);

            session.TryRevive(3, 1, 1f);
            Assert.That(session.IsDowned(1), Is.False);
        }

        [Test]
        public void ReviveIsRejectedUnlessAStandingAllyRevivesADownedPlayer()
        {
            var session = Create(rules: DownedRules());
            for (var id = 1; id <= 6; id++)
            {
                session.Join(id);
            }

            Assert.That(session.TryRevive(3, 1, 1f), Is.False, "target is not downed");

            session.ApplyDamage(2, 1, _maxHealth);
            session.ApplyDamage(2, 3, _maxHealth);

            Assert.That(session.TryRevive(2, 1, 1f), Is.False, "enemy");
            Assert.That(session.TryRevive(1, 1, 1f), Is.False, "self");
            Assert.That(session.TryRevive(3, 1, 1f), Is.False, "downed reviver");
            Assert.That(session.TryRevive(5, 1, 1f), Is.True);
            Assert.That(session.GetReviveProgress(1), Is.EqualTo(1f / _reviveSeconds).Within(1e-6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.TryRevive(5, 1, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.TryRevive(99, 1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.TryRevive(5, 99, 1f));
        }

        [Test]
        public void OnlyOneAllyCanReviveTheSameTargetInATick()
        {
            var session = Create(rules: DownedRules());
            for (var id = 1; id <= 6; id++)
            {
                session.Join(id);
            }

            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.TryRevive(3, 1, 2f), Is.True);
            Assert.That(session.TryRevive(5, 1, 2f), Is.False);
            Assert.That(session.IsDowned(1), Is.True);
        }

        [Test]
        public void SuicideKillsWithoutDowning()
        {
            var session = CreateTwoOnTwo(DownedRules());

            Assert.That(session.ApplyDamage(1, 1, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(_events, Is.EqualTo(Events("kill 1>1")));
            Assert.That(session.IsDowned(1), Is.False);
            Assert.That(session.IsAlive(1), Is.False);
        }

        [Test]
        public void FriendlyFireOffIgnoresShotsAtADownedAlly()
        {
            var session = CreateTwoOnTwo(DownedRules(FriendlyFire.Off));
            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.ApplyDamage(3, 1, _maxHealth), Is.EqualTo(DamageOutcome.Ignored));
            Assert.That(session.GetHealth(1), Is.EqualTo(_downedHealth));
        }

        [Test]
        public void FriendlyFireOnAllyFinishingADownedPlayerCreditsTheEnemy()
        {
            var session = CreateTwoOnTwo(DownedRules(FriendlyFire.On));
            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.ApplyDamage(3, 1, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(_events, Is.EqualTo(Events("down 2>1", "kill 2>1", "score 0-1")));
            Assert.That(session.GetKills(2), Is.EqualTo(1));
            Assert.That(session.GetKills(3), Is.Zero);
        }

        [Test]
        public void FriendlyFireOnAllyDownedPlayerBleedsOutAsADeathOnly()
        {
            // 出血で倒れた回の Tick 全体がリスポーン待ちに数えられるので、出現が同じ Tick に出ないよう長く待たせる
            var session = CreateTwoOnTwo(DownedRules(FriendlyFire.On), respawnSeconds: 100f);

            Assert.That(session.ApplyDamage(3, 1, _maxHealth), Is.EqualTo(DamageOutcome.Downed));
            session.Tick(_bleedoutSeconds);

            Assert.That(_events, Is.EqualTo(Events("down 3>1", "kill 3>1")));
            Assert.That(session.GetDeaths(1), Is.EqualTo(1));
            Assert.That(session.GetKills(3), Is.Zero);
            Assert.That(session.GetScore(Team.Alpha), Is.Zero);
            Assert.That(session.GetScore(Team.Bravo), Is.Zero);
        }

        [Test]
        public void FriendlyFireReflectHitsTheShooterNotTheDownedAlly()
        {
            var session = CreateTwoOnTwo(DownedRules(FriendlyFire.Reflect));
            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.ApplyDamage(3, 1, 10), Is.EqualTo(DamageOutcome.Damaged));

            Assert.That(session.GetHealth(1), Is.EqualTo(_downedHealth));
            Assert.That(session.GetHealth(3), Is.EqualTo(_maxHealth - 10));
        }

        [Test]
        public void FriendlyFireReflectKillsTheShooterWithoutDowningAndWipesTheTeam()
        {
            var session = CreateTwoOnTwo(DownedRules(FriendlyFire.Reflect));
            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.ApplyDamage(3, 1, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(_events, Is.EqualTo(Events("down 2>1", "kill 3>3", "kill 2>1", "score 0-1")));
            Assert.That(session.IsDowned(3), Is.False);
            Assert.That(session.IsAlive(1), Is.False);
        }

        [Test]
        public void LastStandingPlayerIsKilledWithoutDowning()
        {
            var session = Create(rules: DownedRules());
            session.Join(1);
            session.Join(2);
            _events.Clear();

            Assert.That(session.ApplyDamage(2, 1, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(_events, Is.EqualTo(Events("kill 2>1", "score 0-1")));
            Assert.That(_downedChanged, Is.Empty);
        }

        [Test]
        public void WipingOutTheTeamKillsTheDownedPlayers()
        {
            var session = CreateTwoOnTwo(DownedRules());
            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.ApplyDamage(4, 3, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(_events, Is.EqualTo(Events("down 2>1", "kill 4>3", "score 0-1", "kill 2>1", "score 0-2")));
            Assert.That(session.IsAlive(1), Is.False);
            Assert.That(session.IsAlive(3), Is.False);
            Assert.That(session.IsDowned(1), Is.False);
        }

        [Test]
        public void DeadAllyAwaitingRespawnDoesNotKeepTheDownedAlive()
        {
            var session = CreateTwoOnTwo(DownedRules(), respawnSeconds: 10f);
            session.ApplyDamage(1, 1, _maxHealth);
            Assert.That(session.IsAlive(1), Is.False);

            Assert.That(session.ApplyDamage(2, 3, _maxHealth), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(session.IsDowned(3), Is.False);
        }

        [Test]
        public void LeavingLastStandingAllyKillsTheDowned()
        {
            var session = CreateTwoOnTwo(DownedRules());
            session.ApplyDamage(2, 1, _maxHealth);

            session.Leave(3);

            Assert.That(_events, Is.EqualTo(Events("down 2>1", "kill 2>1", "score 0-1")));
            Assert.That(session.IsAlive(1), Is.False);
        }

        [Test]
        public void BleedoutAfterTheDownerLeftCountsOnlyTheDeath()
        {
            // 出血で倒れた回の Tick 全体がリスポーン待ちに数えられるので、出現が同じ Tick に出ないよう長く待たせる
            var session = CreateTwoOnTwo(DownedRules(), respawnSeconds: 100f);
            session.ApplyDamage(2, 1, _maxHealth);

            session.Leave(2);
            session.Tick(_bleedoutSeconds);

            Assert.That(_events, Is.EqualTo(Events("down 2>1", "kill 1>1")));
            Assert.That(session.GetDeaths(1), Is.EqualTo(1));
            Assert.That(session.GetScore(Team.Bravo), Is.Zero);
        }

        [Test]
        public void NoneEndsTheMatchWhenTheTeamIsDownedOrDead()
        {
            var session = CreateTwoOnTwo(DownedRules(respawnMode: RespawnMode.None));
            session.ApplyDamage(2, 1, _maxHealth);
            session.Tick(0f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));

            session.ApplyDamage(4, 3, _maxHealth);
            session.Tick(0f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(session.IsEliminated(1), Is.False);
        }

        [Test]
        public void RespawnClearsTheDownedState()
        {
            var session = CreateTwoOnTwo(DownedRules(), respawnSeconds: 3f);
            session.ApplyDamage(2, 1, _maxHealth);
            session.ApplyDamage(2, 1, _maxHealth);

            session.Tick(3f);

            Assert.That(session.IsAlive(1), Is.True);
            Assert.That(session.IsDowned(1), Is.False);
            Assert.That(session.GetHealth(1), Is.EqualTo(_maxHealth));
            Assert.That(session.GetDownedRemaining(1), Is.Null);
            Assert.That(session.ApplyDamage(2, 1, _maxHealth), Is.EqualTo(DamageOutcome.Downed));
        }

        [Test]
        public void RestartClearsPlayersLeftDownedAtTheEnd()
        {
            var session = CreateTwoOnTwo(DownedRules(), scoreLimit: 1, restartDelaySeconds: 1f);
            session.ApplyDamage(1, 2, _maxHealth);
            session.ApplyDamage(4, 1, _maxHealth);

            // 4 を倒した 1 点で上限に届いて終わるので、全滅した Bravo の 2 も Alpha の 1 もダウンのまま残る
            session.ApplyDamage(3, 4, _maxHealth);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.IsDowned(1), Is.True);
            Assert.That(session.IsDowned(2), Is.True);
            Assert.That(session.TryRevive(3, 1, _reviveSeconds), Is.False);
            _downedChanged.Clear();

            session.Tick(1f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.IsDowned(1), Is.False);
            Assert.That(session.IsDowned(2), Is.False);
            Assert.That(session.GetHealth(1), Is.EqualTo(_maxHealth));
            Assert.That(_downedChanged, Is.EqualTo(Ids(1, 2)));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidRestartDelay(float restartDelaySeconds)
        {
            var points = new[] { new SpawnPoint(Vector3.Zero) };

            Assert.Throws<ArgumentOutOfRangeException>(() => new TeamDeathmatchSession(
                new TeamDeathmatchSpec(10, 60f, 3f), 100, points, new SpawnSelector(), _ => Vector3.Zero, restartDelaySeconds));
        }

        [Test]
        public void RejectsInvalidConstruction()
        {
            var spec = new TeamDeathmatchSpec(10, 60f, 3f);
            var both = new[] { new SpawnPoint(Vector3.Zero) };
            var alphaOnly = new[] { new SpawnPoint(Vector3.Zero, Team.Alpha) };
            var selector = new SpawnSelector();
            Func<int, Vector3> positionOf = _ => Vector3.Zero;

            Assert.Throws<ArgumentNullException>(() => new TeamDeathmatchSession(null, 100, both, selector, positionOf));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TeamDeathmatchSession(spec, 0, both, selector, positionOf));
            Assert.Throws<ArgumentNullException>(() => new TeamDeathmatchSession(spec, 100, null, selector, positionOf));
            Assert.Throws<ArgumentException>(() => new TeamDeathmatchSession(spec, 100, new SpawnPoint[] { null }, selector, positionOf));
            Assert.Throws<ArgumentException>(() => new TeamDeathmatchSession(spec, 100, alphaOnly, selector, positionOf));
            Assert.Throws<ArgumentNullException>(() => new TeamDeathmatchSession(spec, 100, both, null, positionOf));
            Assert.Throws<ArgumentNullException>(() => new TeamDeathmatchSession(spec, 100, both, selector, null));
        }
    }
}
