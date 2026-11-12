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

        [SetUp]
        public void SetUp()
        {
            _positions = new Dictionary<int, Vector3>();
            _events = new List<string>();
        }

        private TeamDeathmatchSession Create(int scoreLimit = 10, float timeLimitSeconds = 60f, float respawnSeconds = 3f)
        {
            var points = new[]
            {
                new SpawnPoint(_alphaBase, Team.Alpha),
                new SpawnPoint(_bravoBase, Team.Bravo),
            };
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(scoreLimit, timeLimitSeconds, respawnSeconds),
                _maxHealth,
                points,
                new SpawnSelector(),
                id => _positions[id]);

            // ネットワーク側と同じく、出現の知らせを受けたら位置を動かす
            session.Spawned += (id, point) =>
            {
                _positions[id] = point.Position;
                _events.Add("spawn " + id);
            };
            session.Started += () => _events.Add("started");
            session.Killed += (killer, victim) => _events.Add("kill " + killer + ">" + victim);
            session.ScoreChanged += (a, b) => _events.Add("score " + a + "-" + b);
            session.Ended += winner => _events.Add("ended " + (winner?.ToString() ?? "draw"));
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
            Assert.That(session.State, Is.EqualTo(TeamDeathmatchState.Waiting));

            session.Join(2);
            Assert.That(session.State, Is.EqualTo(TeamDeathmatchState.Playing));
            Assert.That(_events, Is.EqualTo(new[] { "spawn 1", "spawn 2", "started" }));
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
            Assert.That(_events, Is.EqualTo(new[] { "kill 1>2", "score 1-0" }));
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
            Assert.That(_events, Is.EqualTo(new[] { "kill 1>1" }));
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
            Assert.That(_events, Is.EqualTo(new[] { "spawn 2" }));
        }

        [Test]
        public void InstantRespawnComesAfterKillAndScore()
        {
            var session = Create(respawnSeconds: 0f);
            session.Join(1);
            session.Join(2);
            _events.Clear();

            session.ApplyDamage(1, 2, _maxHealth);

            Assert.That(_events, Is.EqualTo(new[] { "kill 1>2", "score 1-0", "spawn 2" }));
            Assert.That(session.IsAlive(2), Is.True);
        }

        [Test]
        public void EndsWhenScoreLimitReached()
        {
            var session = Create(scoreLimit: 1);
            session.Join(1);
            session.Join(2);
            _events.Clear();

            session.ApplyDamage(2, 1, _maxHealth);

            Assert.That(session.State, Is.EqualTo(TeamDeathmatchState.Ended));
            Assert.That(session.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(_events, Is.EqualTo(new[] { "kill 2>1", "score 0-1", "ended Bravo" }));
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

            Assert.That(session.State, Is.EqualTo(TeamDeathmatchState.Ended));
            Assert.That(session.Winner, Is.Null);
            Assert.That(_events, Is.EqualTo(new[] { "ended draw" }));
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
