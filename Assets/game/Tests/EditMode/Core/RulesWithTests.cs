using System;
using System.Reflection;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary><see cref="GameRules"/> と <see cref="ActionRules"/> の With 系メソッドが 1 項目だけを差し替えた複製を返すことを確かめる。</summary>
    public sealed class RulesWithTests
    {
        // 既定とどの項目も違う値にしておき、差し替えなかった項目が既定に戻っていないことまで見分けられるようにする。
        private static readonly ActionRules _actions = new ActionRules(
            Downed.Revive, 20f, 0.5f, 4f, Cover.On, BlindFire.On, ActiveReload.On, Dodge.On, Vault.On, Grenade.On, 2);

        private static readonly GameRules _rules = new GameRules(
            FriendlyFire.On,
            RespawnMode.Wave,
            12f,
            3f,
            7f,
            DeathBody.Collapse,
            DeathCamera.Killer,
            true,
            1.5f,
            Destruction.On,
            _actions,
            2f,
            FallDamage.On,
            HudDensity.Hardcore,
            MatchObjective.Domination,
            4,
            60f);

        [Test]
        public void EachGameRulesWithReplacesOnlyItsValue()
        {
            var otherActions = ActionRules.Default;
            var cases = new (string Property, Func<GameRules, GameRules> With, object Value)[]
            {
                (nameof(GameRules.FriendlyFire), r => r.WithFriendlyFire(FriendlyFire.Off), FriendlyFire.Off),
                (nameof(GameRules.RespawnMode), r => r.WithRespawnMode(RespawnMode.Individual), RespawnMode.Individual),
                (nameof(GameRules.WaveIntervalSeconds), r => r.WithWaveIntervalSeconds(30f), 30f),
                (nameof(GameRules.RegenDelaySeconds), r => r.WithRegenDelaySeconds(0f), 0f),
                (nameof(GameRules.RegenPerSecond), r => r.WithRegenPerSecond(0f), 0f),
                (nameof(GameRules.DeathBody), r => r.WithDeathBody(DeathBody.Vanish), DeathBody.Vanish),
                (nameof(GameRules.DeathCamera), r => r.WithDeathCamera(DeathCamera.None), DeathCamera.None),
                (nameof(GameRules.InfiniteReserve), r => r.WithInfiniteReserve(false), false),
                (nameof(GameRules.ReserveAmmoMultiplier), r => r.WithReserveAmmoMultiplier(0f), 0f),
                (nameof(GameRules.Destruction), r => r.WithDestruction(Destruction.Off), Destruction.Off),
                (nameof(GameRules.Actions), r => r.WithActions(otherActions), otherActions),
                (nameof(GameRules.SpawnProtectionSeconds), r => r.WithSpawnProtectionSeconds(0f), 0f),
                (nameof(GameRules.FallDamage), r => r.WithFallDamage(FallDamage.Off), FallDamage.Off),
                (nameof(GameRules.HudDensity), r => r.WithHudDensity(HudDensity.Full), HudDensity.Full),
                (nameof(GameRules.Objective), r => r.WithObjective(MatchObjective.None), MatchObjective.None),
                (nameof(GameRules.MinPlayers), r => r.WithMinPlayers(0), 0),
                (nameof(GameRules.IdleKickSeconds), r => r.WithIdleKickSeconds(0f), 0f),
            };

            AssertEveryPropertyHasACase(typeof(GameRules), cases.Length);
            foreach (var c in cases)
            {
                var copy = c.With(_rules);

                Assert.That(copy, Is.Not.SameAs(_rules), c.Property);
                AssertReplacedOnly(_rules, copy, c.Property, c.Value);
            }
        }

        [Test]
        public void EachActionRulesWithReplacesOnlyItsValue()
        {
            var cases = new (string Property, Func<ActionRules, ActionRules> With, object Value)[]
            {
                (nameof(ActionRules.Downed), a => a.WithDowned(Downed.Off), Downed.Off),
                (nameof(ActionRules.DownedBleedoutSeconds), a => a.WithDownedBleedoutSeconds(5f), 5f),
                (nameof(ActionRules.DownedHealthFraction), a => a.WithDownedHealthFraction(1f), 1f),
                (nameof(ActionRules.ReviveSeconds), a => a.WithReviveSeconds(1f), 1f),
                (nameof(ActionRules.BlindFire), a => a.WithBlindFire(BlindFire.Off), BlindFire.Off),
                (nameof(ActionRules.ActiveReload), a => a.WithActiveReload(ActiveReload.Off), ActiveReload.Off),
                (nameof(ActionRules.Dodge), a => a.WithDodge(Dodge.Off), Dodge.Off),
                (nameof(ActionRules.Vault), a => a.WithVault(Vault.Off), Vault.Off),
                (nameof(ActionRules.Grenade), a => a.WithGrenade(Grenade.Off), Grenade.Off),
                (nameof(ActionRules.GrenadeCount), a => a.WithGrenadeCount(0), 0),
            };

            // Cover は BlindFire が On のままでは Off にできないので、BlindFire を切った値から確かめる。
            var withoutBlindFire = _actions.WithBlindFire(BlindFire.Off);
            AssertReplacedOnly(withoutBlindFire, withoutBlindFire.WithCover(Cover.Off), nameof(ActionRules.Cover), Cover.Off);

            AssertEveryPropertyHasACase(typeof(ActionRules), cases.Length + 1);
            foreach (var c in cases)
            {
                var copy = c.With(_actions);

                Assert.That(copy, Is.Not.SameAs(_actions), c.Property);
                AssertReplacedOnly(_actions, copy, c.Property, c.Value);
            }
        }

        [Test]
        public void WithLeavesTheSourceUnchanged()
        {
            _rules.WithRegenPerSecond(1f).WithHudDensity(HudDensity.Full);
            _actions.WithDodge(Dodge.Off).WithGrenadeCount(9);

            Assert.That(_rules.RegenPerSecond, Is.EqualTo(7f));
            Assert.That(_rules.HudDensity, Is.EqualTo(HudDensity.Hardcore));
            Assert.That(_actions.Dodge, Is.EqualTo(Dodge.On));
            Assert.That(_actions.GrenadeCount, Is.EqualTo(2));
        }

        [Test]
        public void WithChainsReplaceSeveralValues()
        {
            var rules = GameRules.Default
                .WithInfiniteReserve(true)
                .WithActions(ActionRules.Default.WithCover(Cover.On).WithBlindFire(BlindFire.On).WithGrenade(Grenade.On).WithGrenadeCount(3));

            Assert.That(rules.InfiniteReserve, Is.True);
            Assert.That(rules.Actions.Cover, Is.EqualTo(Cover.On));
            Assert.That(rules.Actions.BlindFire, Is.EqualTo(BlindFire.On));
            Assert.That(rules.Actions.Grenade, Is.EqualTo(Grenade.On));
            Assert.That(rules.Actions.GrenadeCount, Is.EqualTo(3));
            Assert.That(GameRules.Default.InfiniteReserve, Is.False, "the shared default is not changed");
            Assert.That(ActionRules.Default.Cover, Is.EqualTo(Cover.Off), "the shared default is not changed");
        }

        [Test]
        public void GameRulesWithRejectsInvalidValuesLikeTheConstructor()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithFriendlyFire((FriendlyFire)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithRespawnMode((RespawnMode)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithWaveIntervalSeconds(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithRegenDelaySeconds(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithRegenPerSecond(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithDeathBody((DeathBody)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithDeathCamera((DeathCamera)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithReserveAmmoMultiplier(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithDestruction((Destruction)99));
            Assert.Throws<ArgumentNullException>(() => _rules.WithActions(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithSpawnProtectionSeconds(-0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithFallDamage((FallDamage)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithHudDensity((HudDensity)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithObjective((MatchObjective)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithMinPlayers(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rules.WithIdleKickSeconds(float.NaN));
        }

        [Test]
        public void ActionRulesWithRejectsInvalidValuesLikeTheConstructor()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithDowned((Downed)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithDownedBleedoutSeconds(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithDownedHealthFraction(1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithReviveSeconds(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithCover((Cover)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithBlindFire((BlindFire)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithActiveReload((ActiveReload)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithDodge((Dodge)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithVault((Vault)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithGrenade((Grenade)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _actions.WithGrenadeCount(-1));
        }

        [Test]
        public void ActionRulesWithKeepsBlindFireTiedToCover()
        {
            Assert.Throws<ArgumentException>(() => _actions.WithCover(Cover.Off), "blind fire is still on");
            Assert.Throws<ArgumentException>(() => ActionRules.Default.WithBlindFire(BlindFire.On), "cover is still off");
        }

        private static void AssertEveryPropertyHasACase(Type type, int caseCount)
        {
            Assert.That(
                InstanceProperties(type).Length,
                Is.EqualTo(caseCount),
                $"every property of {type.Name} needs a With method and a case here");
        }

        private static void AssertReplacedOnly<T>(T source, T copy, string replaced, object value)
        {
            foreach (var property in InstanceProperties(typeof(T)))
            {
                var actual = property.GetValue(copy);
                if (property.Name == replaced)
                {
                    Assert.That(actual, Is.EqualTo(value), $"{replaced} is replaced");
                }
                else
                {
                    Assert.That(actual, Is.EqualTo(property.GetValue(source)), $"{property.Name} is kept when replacing {replaced}");
                }
            }
        }

        private static PropertyInfo[] InstanceProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        }
    }
}
