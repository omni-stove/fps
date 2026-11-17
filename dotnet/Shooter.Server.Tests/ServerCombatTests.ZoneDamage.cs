using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed partial class ServerCombatTests
    {
        // 目の高さ (中心から 0.6) は頭の判定 (高さ 2 のカプセルの 85% 以上、中心から 0.7 以上) より下なので、その上を狙う。
        private Vector3 Head(int id)
        {
            return Body(id).Center + new Vector3(0f, 0.8f, 0f);
        }

        [Test]
        public void AHeadshotIsMultipliedByTheHeadMultiplierOfTheRules()
        {
            Start(GameRulesJson.Parse("{\"zoneDamage\": {\"head\": 3}}", "memory", Rules()));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Head(2));

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 75), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("zone=Head dmg=75"));
        }

        [Test]
        public void TheBodyMultiplierOfTheRulesScalesABodyShot()
        {
            Start(Rules().WithZoneDamage(new ZoneDamageMultipliers(3.0, 1.2, 0.75, 2.0)));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 30), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("zone=Body dmg=30"));
        }

        [Test]
        public void WithoutZoneDamageInTheRulesAHeadshotUsesTheDefaultMultiplier()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Head(2));

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 50), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("zone=Head dmg=50"));
        }
    }
}
