using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class TeamDeathmatchRoundsTests
    {
        private const int _maxHealth = 100;

        private static readonly string[] _everyoneSpawned = { "spawn 1", "spawn 2", "spawn 3" };

        private Dictionary<int, Vector3> _positions;
        private List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _positions = new Dictionary<int, Vector3>();
            _events = new List<string>();
        }

        private TeamDeathmatchSession Create(bool respawnsByRound, RespawnMode respawnMode = RespawnMode.None, float respawnSeconds = 0f)
        {
            var points = new[]
            {
                new SpawnPoint(new Vector3(-20f, 0f, 0f), Team.Alpha),
                new SpawnPoint(new Vector3(20f, 0f, 0f), Team.Bravo),
            };
            var rules = GameRules.Default.WithRespawnMode(respawnMode).WithSpawnProtectionSeconds(0f)
                .WithActions(ActionRules.Default.WithDowned(Downed.Off));
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(10, 60f, respawnSeconds, rules, respawnsByRound: respawnsByRound),
                _maxHealth,
                points,
                new SpawnSelector(),
                id => _positions[id]);
            session.Spawned += (id, point) =>
            {
                _positions[id] = point.Position;
                _events.Add("spawn " + id);
            };
            session.Ended += winner => _events.Add("ended " + (winner?.ToString() ?? "draw"));
            return session;
        }

        [Test]
        public void WithoutRespawnAWipeEndsTheMatch()
        {
            var session = Create(respawnsByRound: false);
            session.Join(1);
            session.Join(2);

            session.ApplyDamage(1, 2, _maxHealth);
            session.Tick(0.1f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
        }

        [Test]
        public void RespawningByRoundNeitherEndsOnAWipeNorRespawns()
        {
            var session = Create(respawnsByRound: true, RespawnMode.Individual, respawnSeconds: 0f);
            session.Join(1);
            session.Join(2);
            _events.Clear();

            Assert.That(session.ApplyDamage(1, 2, _maxHealth), Is.EqualTo(DamageOutcome.Killed));
            session.Tick(5f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.IsAlive(2), Is.False);
            Assert.That(session.IsEliminated(2), Is.False);
            Assert.That(session.GetRespawnRemaining(2), Is.Null);
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void ADeadPlayerKilledAgainAfterRespawnAllIsCounted()
        {
            var session = Create(respawnsByRound: true);
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(1, 2, _maxHealth);
            session.RespawnAll();

            Assert.That(session.ApplyDamage(1, 2, _maxHealth), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(session.GetKills(1), Is.EqualTo(2));
            Assert.That(session.GetDeaths(2), Is.EqualTo(2));
        }

        [Test]
        public void RespawnAllBringsEveryoneBackAndKeepsTheStats()
        {
            var session = Create(respawnsByRound: true);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            session.ApplyDamage(1, 2, _maxHealth);
            session.ApplyDamage(2, 3, 40);
            var remaining = session.RemainingSeconds;
            _events.Clear();

            session.RespawnAll();

            Assert.That(_events, Is.EqualTo(_everyoneSpawned));
            Assert.That(session.IsAlive(2), Is.True);
            Assert.That(session.GetHealth(2), Is.EqualTo(_maxHealth));
            Assert.That(session.GetHealth(3), Is.EqualTo(_maxHealth));
            Assert.That(session.GetKills(1), Is.EqualTo(1));
            Assert.That(session.GetDeaths(2), Is.EqualTo(1));
            Assert.That(session.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(session.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
        }
    }
}
