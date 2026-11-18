using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 近接・ストリーク・設置物が使う、撃った弾でないダメージの口と、倒したことを知る口
    public sealed partial class ServerCombatTests
    {
        private const string _knifeId = "melee.knife";

        [Test]
        public void DealtDamageHurtsTheTargetAndTellsBothSides()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            var outcome = _combat.DealDamage(1, 2, 40, _knifeId, HitZone.Body, _alphaSpawn);

            Assert.That(outcome, Is.EqualTo(DamageOutcome.Damaged));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 40));
            var confirm = _transport.Read<HitConfirmMessage>(1, m => new HitConfirmMessage { Damage = m.Damage, Zone = m.Zone, Killed = m.Killed }).Single();
            Assert.That(confirm.Damage, Is.EqualTo(40));
            Assert.That(confirm.Zone, Is.EqualTo(HitZone.Body));
            Assert.That(confirm.Killed, Is.False);
            var taken = _transport.Read<DamageTakenMessage>(2, m => new DamageTakenMessage { HasSource = m.HasSource, SourcePosition = m.SourcePosition, Amount = m.Amount }).Single();
            Assert.That(taken.Amount, Is.EqualTo(40));
            Assert.That(taken.HasSource, Is.True);
            Assert.That(taken.SourcePosition, Is.EqualTo(_alphaSpawn));
            Assert.That(_log.ToString(), Does.Contain("[Combat] dealt attacker=1 victim=2 weapon=melee.knife zone=Body dmg=40 outcome=Damaged"));
        }

        [Test]
        public void ADealtKillIsCountedWithItsWeaponAndToldThroughPlayerKilled()
        {
            Start(Rules());
            var kills = new List<KillFeedEntry>();
            _combat.PlayerKilled += kills.Add;
            Join(1);
            Join(2);
            Step(0.2f);

            var outcome = _combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, _knifeId, HitZone.Head, null);

            Assert.That(outcome, Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_rules.Session.IsAlive(2), Is.False);
            Assert.That(_rules.Session.GetKills(1), Is.EqualTo(1));
            Assert.That(kills, Has.Count.EqualTo(1));
            Assert.That(kills[0].KillerId, Is.EqualTo(1));
            Assert.That(kills[0].VictimId, Is.EqualTo(2));
            Assert.That(kills[0].WeaponId, Is.EqualTo(_knifeId));
            Assert.That(kills[0].Headshot, Is.True);
            Assert.That(kills[0].Friendly, Is.False);
            var killed = _transport.Read<KilledMessage>(2, Copy).Single();
            Assert.That(killed.WeaponId, Is.EqualTo(_knifeId));
            Assert.That(_transport.Read<HitConfirmMessage>(1, m => new HitConfirmMessage { Killed = m.Killed }).Single().Killed, Is.True);
            var taken = _transport.Read<DamageTakenMessage>(2, m => new DamageTakenMessage { HasSource = m.HasSource }).Single();
            Assert.That(taken.HasSource, Is.False, "no source was given");
        }

        [Test]
        public void PlayerKilledAlsoTellsKillsByShots()
        {
            Start(Rules());
            var kills = new List<KillFeedEntry>();
            _combat.PlayerKilled += kills.Add;
            Join(1);
            Join(2);
            Step(0.2f);

            for (var i = 0; i < 4; i++)
            {
                FireAt(1, Body(2).Center);
                Step(0.2f);
            }

            Assert.That(kills, Has.Count.EqualTo(1), _log.ToString());
            Assert.That(kills[0].KillerId, Is.EqualTo(1));
            Assert.That(kills[0].VictimId, Is.EqualTo(2));
            Assert.That(kills[0].WeaponId, Is.EqualTo(TestRules.RifleId));
        }

        [Test]
        public void DamageFromAnAttackerWhoLeftIsCountedAsTheVictimsOwn()
        {
            Start(Rules());
            var kills = new List<KillFeedEntry>();
            _combat.PlayerKilled += kills.Add;
            Join(1);
            Join(2);
            Step(0.2f);
            _match.Leave(new ConnectionId(1));
            _host.OnDisconnected(new ConnectionId(1));

            var outcome = _combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, _knifeId, HitZone.Body, _alphaSpawn);

            Assert.That(outcome, Is.EqualTo(DamageOutcome.Killed));
            Assert.That(kills, Has.Count.EqualTo(1));
            Assert.That(kills[0].KillerId, Is.EqualTo(2));
            Assert.That(kills[0].IsSuicide, Is.True);
            Assert.That(kills[0].WeaponId, Is.EqualTo(_knifeId));
            Assert.That(_transport.Read<HitConfirmMessage>(1, m => m), Is.Empty);
            Assert.That(_transport.Read<DamageTakenMessage>(2, m => new DamageTakenMessage { Amount = m.Amount }).Single().Amount, Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Combat] dealt attacker=none victim=2 weapon=melee.knife zone=Body dmg=100 outcome=Killed"));
        }

        [Test]
        public void DamageToATeammateIsIgnoredWithFriendlyFireOff()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Join(3);
            Step(0.2f);
            Assert.That(_rules.Session.GetTeam(3), Is.EqualTo(_rules.Session.GetTeam(1)));

            var outcome = _combat.DealDamage(1, 3, 40, _knifeId, HitZone.Body, _alphaSpawn);

            Assert.That(outcome, Is.EqualTo(DamageOutcome.Ignored));
            Assert.That(_rules.Session.GetHealth(3), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_transport.Read<HitConfirmMessage>(1, m => m), Is.Empty);
            Assert.That(_transport.Read<DamageTakenMessage>(3, m => m), Is.Empty);
            Assert.That(_log.ToString(), Does.Not.Contain("[Combat] dealt"));
        }

        [Test]
        public void DamageToSomeoneNotInTheMatchIsIgnored()
        {
            Start(Rules());
            Join(1);
            Step(0.2f);

            Assert.That(_combat.DealDamage(1, 9, 40, _knifeId, HitZone.Body, null), Is.EqualTo(DamageOutcome.Ignored));
            Assert.That(_transport.Read<HitConfirmMessage>(1, m => m), Is.Empty);
        }

        [Test]
        public void DealtDamageRejectsANegativeAmountAndAMissingWeapon()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.Throws<ArgumentOutOfRangeException>(() => _combat.DealDamage(1, 2, -1, _knifeId, HitZone.Body, null));
            Assert.Throws<ArgumentNullException>(() => _combat.DealDamage(1, 2, 10, null, HitZone.Body, null));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }
    }
}
