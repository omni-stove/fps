using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class TeamDeathmatchSessionChangeTeamTests
    {
        private static readonly Vector3 _alphaBase = new Vector3(-20f, 0f, 0f);
        private static readonly Vector3 _bravoBase = new Vector3(20f, 0f, 0f);

        private Dictionary<int, Vector3> _positions;

        [SetUp]
        public void SetUp()
        {
            _positions = new Dictionary<int, Vector3>();
        }

        [Test]
        public void MovesAPlayerBetweenMatchesAndSpawnsThemWithTheNewTeam()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            session.Join(3);
            Assert.That(session.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            session.Finish(Team.Alpha);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));

            Assert.That(session.ChangeTeam(1, Team.Bravo), Is.True);

            Assert.That(session.GetTeam(1), Is.EqualTo(Team.Bravo));
            Assert.That(session.GetKills(1), Is.EqualTo(1));
            Assert.That(session.NextTeam, Is.EqualTo(Team.Alpha));

            session.Restart();

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.GetTeam(1), Is.EqualTo(Team.Bravo));
            Assert.That(session.GetKills(1), Is.EqualTo(0));
            Assert.That(_positions[1], Is.EqualTo(_bravoBase));
            Assert.That(_positions[3], Is.EqualTo(_alphaBase));
        }

        [Test]
        public void MovingToTheSameTeamDoesNothing()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            session.Finish(null);

            Assert.That(session.ChangeTeam(1, Team.Alpha), Is.False);
            Assert.That(session.GetTeam(1), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void RefusesToMoveDuringAMatchOrAnUnknownPlayer()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);

            Assert.That(() => session.ChangeTeam(1, Team.Bravo), Throws.InvalidOperationException);
            Assert.That(session.GetTeam(1), Is.EqualTo(Team.Alpha));

            session.Finish(null);
            Assert.That(() => session.ChangeTeam(9, Team.Bravo), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => session.ChangeTeam(1, (Team)7), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        private TeamDeathmatchSession Create()
        {
            var points = new[]
            {
                new SpawnPoint(_alphaBase, Team.Alpha),
                new SpawnPoint(_bravoBase, Team.Bravo),
            };
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(10, 60f, 3f, GameRules.Default.WithSpawnProtectionSeconds(0f)),
                100,
                points,
                new SpawnSelector(),
                id => _positions[id]);
            session.Spawned += (id, point) => _positions[id] = point.Position;
            return session;
        }
    }
}
