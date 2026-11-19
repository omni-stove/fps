using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // 2 チームより細かい組 (BR の分隊) で味方を決める口。ここでは奇数の Id と偶数の Id を組にし、チームとは食い違わせる
    public sealed class TeamDeathmatchSessionAlliesTests
    {
        private const int _maxHealth = 100;

        private static readonly SpawnPoint[] _points = { new SpawnPoint(new Vector3(-10f, 0f, 0f)), new SpawnPoint(new Vector3(10f, 0f, 0f)) };
        private static readonly int[] _thirdThenFirst = { 3, 1 };
        private static readonly int[] _thirdOnly = { 3 };

        private static GameRules Rules()
        {
            return GameRules.Default
                .WithFriendlyFire(FriendlyFire.Off)
                .WithActions(ActionRules.Default.WithDowned(Downed.Revive))
                .WithSpawnProtectionSeconds(0f)
                .WithRegenPerSecond(0f);
        }

        private static TeamDeathmatchSession Create(bool respawnsByRound = false, Func<int, int, bool> areAllies = null)
        {
            return new TeamDeathmatchSession(
                new TeamDeathmatchSpec(100, 600f, 5f, Rules(), respawnsByRound: respawnsByRound),
                _maxHealth,
                _points,
                new SpawnSelector(),
                _ => Vector3.Zero,
                null,
                TeamDeathmatchSession.DefaultReconnectGraceSeconds,
                null,
                areAllies ?? ((a, b) => a % 2 == b % 2));
        }

        // 1 と 3 は組が同じでチームが違い、1 と 4 はチームが同じで組が違う
        private static TeamDeathmatchSession Joined(bool respawnsByRound = false)
        {
            var session = Create(respawnsByRound);
            session.Join(1, null, Team.Alpha);
            session.Join(2, null, Team.Bravo);
            session.Join(3, null, Team.Bravo);
            session.Join(4, null, Team.Alpha);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            return session;
        }

        [Test]
        public void AlliesComeFromTheGivenGroupsInsteadOfTheTeams()
        {
            var session = Joined();

            Assert.That(session.AreAllies(1, 3), Is.True);
            Assert.That(session.AreAllies(1, 4), Is.False);
            Assert.That(session.AreAllies(1, 1), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AreAllies(1, 9));
        }

        [Test]
        public void WithoutGroupsTheTeamsDecide()
        {
            var session = new TeamDeathmatchSession(new TeamDeathmatchSpec(100, 600f, 5f, Rules()), _maxHealth, _points, new SpawnSelector(), _ => Vector3.Zero);
            session.Join(1, null, Team.Alpha);
            session.Join(2, null, Team.Bravo);
            session.Join(3, null, Team.Alpha);

            Assert.That(session.AreAllies(1, 3), Is.True);
            Assert.That(session.AreAllies(1, 2), Is.False);
        }

        [Test]
        public void FriendlyFireFollowsTheGroups()
        {
            var session = Joined();

            Assert.That(session.ApplyDamage(1, 3, 10), Is.EqualTo(DamageOutcome.Ignored), "an ally on the other team");
            Assert.That(session.ApplyDamage(1, 4, 10), Is.EqualTo(DamageOutcome.Damaged), "an enemy on the same team");
            Assert.That(session.GetHealth(3), Is.EqualTo(_maxHealth));
            Assert.That(session.GetHealth(4), Is.EqualTo(_maxHealth - 10));
        }

        [Test]
        public void DownsAndRevivesFollowTheGroups()
        {
            var session = Joined();

            Assert.That(session.ApplyDamage(4, 1, _maxHealth), Is.EqualTo(DamageOutcome.Downed), "3 is a standing ally of 1");
            Assert.That(session.TryRevive(4, 1, 1f), Is.False, "4 shares the team but not the group");
            Assert.That(session.TryRevive(3, 1, Rules().Actions.ReviveSeconds), Is.True);
            Assert.That(session.IsDowned(1), Is.False);
            Assert.That(session.IsAlive(1), Is.True);
        }

        [Test]
        public void AGroupWithNobodyStandingFallsTogether()
        {
            var session = Joined();
            var killed = new List<int>();
            session.Killed += (_, victimId, _) => killed.Add(victimId);

            Assert.That(session.ApplyDamage(4, 1, _maxHealth), Is.EqualTo(DamageOutcome.Downed));
            Assert.That(session.ApplyDamage(2, 3, _maxHealth), Is.EqualTo(DamageOutcome.Killed), "the last standing of the group is not downed");

            Assert.That(session.IsAlive(1), Is.False, "the downed ally has nobody left to revive them");
            Assert.That(killed, Is.EqualTo(_thirdThenFirst));
            Assert.That(session.IsAlive(2), Is.True);
            Assert.That(session.IsAlive(4), Is.True);
        }

        [Test]
        public void RespawnPlayerBringsBackOnlyTheFallen()
        {
            var session = Joined(respawnsByRound: true);
            var spawned = new List<int>();
            session.Spawned += (playerId, _) => spawned.Add(playerId);
            Assert.That(session.ApplyDamage(2, 3, _maxHealth), Is.EqualTo(DamageOutcome.Downed));
            Assert.That(session.RespawnPlayer(3), Is.False, "a downed player is not fallen yet");
            Assert.That(session.ApplyDamage(2, 3, _maxHealth), Is.EqualTo(DamageOutcome.Killed));
            session.Tick(10f);
            Assert.That(session.IsAlive(3), Is.False, "the session does not bring anyone back on its own");

            Assert.That(session.RespawnPlayer(3), Is.True);

            Assert.That(session.IsAlive(3), Is.True);
            Assert.That(session.GetHealth(3), Is.EqualTo(_maxHealth));
            Assert.That(spawned, Is.EqualTo(_thirdOnly));
            Assert.That(session.RespawnPlayer(3), Is.False, "a standing player is not brought back again");
            Assert.Throws<ArgumentOutOfRangeException>(() => session.RespawnPlayer(9));
        }
    }
}
