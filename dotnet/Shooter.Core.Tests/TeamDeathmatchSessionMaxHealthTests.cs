using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // 1 人だけ体力の上限を替える口 (感染者の体) が、今の体力・出直し・回復・次の試合・抜けた人をどう扱うかを確かめる
    public sealed class TeamDeathmatchSessionMaxHealthTests
    {
        private static readonly int[] _onlyPlayer1 ={ 1 };

        private Dictionary<int, Vector3> _positions;

        [SetUp]
        public void SetUp()
        {
            _positions = new Dictionary<int, Vector3>();
        }

        [Test]
        public void RaisingTheCapKeepsTheCurrentHealthUntilHealed()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            Assert.That(session.ApplyDamage(2, 1, 30), Is.EqualTo(DamageOutcome.Damaged));

            Assert.That(session.SetMaxHealth(1, 150), Is.True);

            Assert.That(session.GetMaxHealth(1), Is.EqualTo(150));
            Assert.That(session.GetMaxHealth(2), Is.EqualTo(100), "the others keep the shared cap");
            Assert.That(session.MaxHealth, Is.EqualTo(100));
            Assert.That(session.GetHealth(1), Is.EqualTo(70), "raising the cap does not heal");
            Assert.That(session.Heal(1, 150), Is.EqualTo(80));
            Assert.That(session.GetHealth(1), Is.EqualTo(150));
            Assert.That(session.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Damaged), "150 health survives a 100 hit");
            Assert.That(session.GetHealth(1), Is.EqualTo(50));
        }

        [Test]
        public void LoweringTheCapCutsTheHealthAboveIt()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            var changed = new List<int>();
            session.PlayerChanged += changed.Add;

            Assert.That(session.SetMaxHealth(1, 60), Is.True);

            Assert.That(session.GetHealth(1), Is.EqualTo(60));
            Assert.That(changed, Is.EqualTo(_onlyPlayer1), "the cut health is told");
            Assert.That(session.Heal(1, 50), Is.Zero);
        }

        [Test]
        public void SettingTheSameCapDoesNothing()
        {
            var session = Create();
            session.Join(1);
            session.Join(2);
            var changed = 0;
            session.PlayerChanged += _ => changed++;

            Assert.That(session.SetMaxHealth(1, 100), Is.False);
            Assert.That(session.SetMaxHealth(1, 150), Is.True);
            Assert.That(session.SetMaxHealth(1, 150), Is.False);
            Assert.That(changed, Is.Zero, "raising the cap of someone at 100 does not change their health");
        }

        [Test]
        public void AFallenPlayerRespawnsWithTheChangedCap()
        {
            var session = Create(respawnSeconds: 1f);
            session.Join(1);
            session.Join(2);
            Assert.That(session.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(session.SetMaxHealth(1, 150), Is.True);
            Assert.That(session.IsAlive(1), Is.False, "changing the cap does not bring the fallen back");
            Assert.That(session.Heal(1, 150), Is.Zero);

            session.Tick(1.1f);

            Assert.That(session.IsAlive(1), Is.True);
            Assert.That(session.GetHealth(1), Is.EqualTo(150));
            Assert.That(session.GetMaxHealth(1), Is.EqualTo(150), "the cap stays over a respawn");
        }

        [Test]
        public void TheNextMatchAndLeavingGoBackToTheSharedCap()
        {
            var session = Create(reconnectGraceSeconds: 0f);
            session.Join(1);
            session.Join(2);
            session.Join(3);
            session.SetMaxHealth(1, 150);
            session.SetMaxHealth(3, 175);
            session.Finish(null);

            session.Restart();

            Assert.That(session.GetMaxHealth(1), Is.EqualTo(100));
            Assert.That(session.GetHealth(1), Is.EqualTo(100));

            session.SetMaxHealth(3, 175);
            session.Leave(3);
            session.Join(3);
            Assert.That(session.GetMaxHealth(3), Is.EqualTo(100), "a player who left and joined again starts with the shared cap");
            Assert.That(session.GetHealth(3), Is.EqualTo(100));
        }

        [Test]
        public void ChangingTheCapKeepsTheWaitBeforeRegeneration()
        {
            var session = Create(rules: GameRules.Default.WithSpawnProtectionSeconds(0f).WithRegenDelaySeconds(2f).WithRegenPerSecond(10f));
            session.Join(1);
            session.Join(2);
            session.ApplyDamage(2, 1, 50);
            session.Tick(1.5f);
            Assert.That(session.GetHealth(1), Is.EqualTo(50));

            session.SetMaxHealth(1, 150);
            session.Tick(1f);

            Assert.That(session.GetHealth(1), Is.EqualTo(55), "0.5 s of the wait was left, then 0.5 s of 10 per second");
        }

        [Test]
        public void ARevivedPlayerGetsTheDownedHealthUnderTheChangedCap()
        {
            var rules = GameRules.Default
                .WithSpawnProtectionSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithActions(ActionRules.Default.WithDowned(Downed.Revive));
            var session = Create(rules: rules);
            session.Join(1);
            session.Join(2);
            session.Join(3, null, Team.Alpha);
            Assert.That(session.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Downed));
            session.SetMaxHealth(1, 150);

            Assert.That(session.TryRevive(3, 1, rules.Actions.ReviveSeconds + 0.1f), Is.True);

            Assert.That(session.IsDowned(1), Is.False);
            Assert.That(session.GetMaxHealth(1), Is.EqualTo(150));
            var revived = session.GetHealth(1);
            Assert.That(revived, Is.GreaterThan(0).And.LessThan(150));
            Assert.That(session.Heal(1, 1000), Is.EqualTo(150 - revived), "the revived player heals up to the changed cap");
            Assert.That(session.GetHealth(1), Is.EqualTo(150));
        }

        [Test]
        public void RefusesAnUnknownPlayerOrACapBelowOne()
        {
            var session = Create();
            session.Join(1);

            Assert.That(() => session.SetMaxHealth(1, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => session.SetMaxHealth(9, 150), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => session.GetMaxHealth(9), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(session.GetMaxHealth(1), Is.EqualTo(100));
        }

        private TeamDeathmatchSession Create(float respawnSeconds = 3f, float reconnectGraceSeconds = 60f, GameRules rules = null)
        {
            var points = new[]
            {
                new SpawnPoint(new Vector3(-20f, 0f, 0f), Team.Alpha),
                new SpawnPoint(new Vector3(20f, 0f, 0f), Team.Bravo),
            };
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(10, 60f, respawnSeconds, rules ?? GameRules.Default.WithSpawnProtectionSeconds(0f).WithRegenPerSecond(0f)),
                100,
                points,
                new SpawnSelector(),
                id => _positions[id],
                null,
                reconnectGraceSeconds);
            session.Spawned += (id, point) => _positions[id] = point.Position;
            return session;
        }
    }
}
