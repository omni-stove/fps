using System.Linq;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // 戦闘は敵味方を目的 (IMatchObjectiveRules.AreHostile) に聞く。既定はチームで決めるので、今の目的では味方撃ちの結果が変わらない
    public sealed partial class ServerCombatTests
    {
        [TestCase(FriendlyFire.Off, false)]
        [TestCase(FriendlyFire.On, true)]
        [TestCase(FriendlyFire.Reflect, false)]
        public void TheDefaultHostilityKeepsTheFriendlyFireRule(FriendlyFire friendlyFire, bool allyCanBeAfflicted)
        {
            Start(Rules().WithFriendlyFire(friendlyFire));
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            var (attacker, ally, enemy) = Sides();

            Assert.That(_rules.AreHostile(attacker, enemy), Is.True);
            Assert.That(_rules.AreHostile(attacker, ally), Is.False);
            Assert.That(_rules.AreHostile(attacker, attacker), Is.False);
            Assert.That(_combat.CanAfflict(attacker, enemy), Is.True);
            Assert.That(_combat.CanAfflict(attacker, ally), Is.EqualTo(allyCanBeAfflicted));
            Assert.That(_combat.CanAfflict(attacker, attacker), Is.True, "hurting oneself is always allowed");
        }

        [TestCase(FriendlyFire.Off, 0, 0)]
        [TestCase(FriendlyFire.On, 0, 10)]
        [TestCase(FriendlyFire.Reflect, 10, 0)]
        public void DamageToAnAllyLandsWhereTheFriendlyFireRuleSays(FriendlyFire friendlyFire, int attackerLoses, int allyLoses)
        {
            Start(Rules().WithFriendlyFire(friendlyFire));
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            var (attacker, ally, enemy) = Sides();
            var full = NetMatchSettings.DefaultMaxHealth;

            _combat.DamageOverTime(attacker, ally, 10, TestRules.RifleId);
            _combat.DamageOverTime(attacker, enemy, 10, TestRules.RifleId);

            Assert.That(_rules.Session.GetHealth(attacker), Is.EqualTo(full - attackerLoses), _log.ToString());
            Assert.That(_rules.Session.GetHealth(ally), Is.EqualTo(full - allyLoses));
            Assert.That(_rules.Session.GetHealth(enemy), Is.EqualTo(full - 10));
        }

        [TestCase(FriendlyFire.Off)]
        [TestCase(FriendlyFire.Reflect)]
        public void AnObjectiveThatMakesEveryoneHostileLetsEffectsReachTeammates(FriendlyFire friendlyFire)
        {
            var objective = new TestObjectiveRules { Hostile = (a, _, b, _) => a != b };
            Start(Rules().WithFriendlyFire(friendlyFire), objective: objective);
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            var (attacker, ally, enemy) = Sides();

            Assert.That(_rules.AreHostile(attacker, ally), Is.True);
            Assert.That(_rules.AreHostile(attacker, enemy), Is.True);
            Assert.That(_rules.AreHostile(attacker, attacker), Is.False);
            Assert.That(_combat.CanAfflict(attacker, ally), Is.True);
        }

        // Session も味方撃ちを目的の敵味方で決めるので、分隊で戦う目的ではチームが同じでも当たり、チームが違っても当たらないことがある
        [Test]
        public void TheSessionLandsDamageByTheHostilityOfTheObjective()
        {
            var objective = new TestObjectiveRules { Hostile = (a, _, b, _) => a != b };
            Start(Rules().WithFriendlyFire(FriendlyFire.Off), objective: objective);
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            var (attacker, ally, _) = Sides();

            _combat.DamageOverTime(attacker, ally, 10, TestRules.RifleId);

            Assert.That(_rules.Session.AreAllies(attacker, ally), Is.False);
            Assert.That(_rules.Session.GetHealth(ally), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 10), _log.ToString());
        }

        [Test]
        public void AnObjectiveThatMakesNobodyHostileKeepsEnemiesFromBeingHurt()
        {
            var objective = new TestObjectiveRules { Hostile = (_, _, _, _) => false };
            Start(Rules().WithFriendlyFire(FriendlyFire.Off), objective: objective);
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            var (attacker, _, enemy) = Sides();

            _combat.DamageOverTime(attacker, enemy, 10, TestRules.RifleId);

            Assert.That(_rules.Session.AreAllies(attacker, enemy), Is.True);
            Assert.That(_rules.Session.GetHealth(enemy), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
        }

        [Test]
        public void TheDefaultObjectiveRefusesToInteractAndHasNoBotGoal()
        {
            IMatchObjectiveRules objective = new TestObjectiveRules();

            var verdict = objective.Interact(1, 7);

            Assert.That(verdict.IsAccepted, Is.False);
            Assert.That(verdict.Refusal, Is.EqualTo(InteractRefusal.NotInteractable));
            Assert.That(objective.BotGoal(1), Is.Null);
            Assert.That(objective.AreHostile(1, Team.Alpha, 2, Team.Bravo), Is.True);
            Assert.That(objective.AreHostile(1, Team.Alpha, 3, Team.Alpha), Is.False);
        }

        [Test]
        public void ADefaultVerdictIsARefusal()
        {
            Assert.That(default(InteractVerdict).IsAccepted, Is.False);
            Assert.That(default(InteractVerdict).Refusal, Is.EqualTo(InteractRefusal.NotInteractable));
            Assert.That(InteractVerdict.Accepted.IsAccepted, Is.True);
            Assert.That(InteractVerdict.Accepted.Refusal, Is.EqualTo(InteractRefusal.None));
            var outOfReach = InteractRefusal.OutOfReach;
            Assert.That(InteractVerdict.Refused(outOfReach), Is.EqualTo(InteractVerdict.Refused(InteractRefusal.OutOfReach)));
            Assert.That(InteractVerdict.Refused(InteractRefusal.OutOfReach), Is.Not.EqualTo(InteractVerdict.Accepted));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => InteractVerdict.Refused(InteractRefusal.None));
        }

        private static readonly int[] _threePlayers = { 1, 2, 3 };

        // 3 人目がどちらの陣営に入るかは振り分けが決めるので、その人と同じ陣営の 1 人目を撃つ側、3 人目を味方、残りを敵にする
        private (int Attacker, int Ally, int Enemy) Sides()
        {
            var session = _rules.Session;
            var ids = _threePlayers;
            Assert.That(ids.All(session.HasPlayer), Is.True, _log.ToString());
            var allyTeam = session.GetTeam(3);
            var attacker = ids.First(id => id != 3 && session.GetTeam(id) == allyTeam);
            var enemy = ids.Single(id => session.GetTeam(id) != allyTeam);
            return (attacker, 3, enemy);
        }
    }
}
