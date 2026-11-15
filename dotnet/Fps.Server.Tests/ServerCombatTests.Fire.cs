using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // 焼夷グレネードの燃える範囲が、中にいる人に燃焼をかけ続け、出た人は燃焼が切れるまでで止まる
    public sealed partial class ServerCombatTests
    {
        private const string _fireWeaponId = "b9e59060-e2b8-4109-a693-0a87015e6817";

        private static readonly AreaEffectSpec _fireArea = new AreaEffectSpec(2f, 10f, 0.5f, 5, visualId: "fire");

        private static readonly GrenadeSpec _incendiary = new GrenadeSpec(
            _fireWeaponId,
            "grenade.incendiary",
            new ImpactSpec(damage: 0, areaEffect: _fireArea),
            fuseSeconds: 1f);

        [Test]
        public void SomeoneInTheFireKeepsBurningAndStopsOnceTheBurnRunsOutAfterLeaving()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var burn = FireArea.Affliction(_fireArea);

            _combat.AreaEffects.Spread(_fireArea, Body(2).Position, 1, _fireWeaponId);
            Step(1.5f);

            Assert.That(_combat.StatusEffects.RemainingSeconds(2, StatusEffectKinds.Burn), Is.GreaterThan(burn.DurationSeconds - 0.5f), "still topped up inside");
            Assert.That(_combat.StatusEffects.RemainingSeconds(1, StatusEffectKinds.Burn), Is.Zero, "the thrower is far away");
            var inside = _rules.Session.GetHealth(2);
            Assert.That(inside, Is.LessThan(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[StatusEffect] afflict source=1 victim=2 kind=burn weapon=" + _fireWeaponId));

            _context.Bodies.Teleport(2, new Vector3(20f, _bravoSpawn.Y, 0f), 0f, _host.TickCount);
            Step(1f);
            var left = _rules.Session.GetHealth(2);
            Assert.That(left, Is.LessThan(inside), "it keeps burning after leaving");

            Step(burn.DurationSeconds);
            var burntOut = _rules.Session.GetHealth(2);
            Assert.That(_combat.StatusEffects.RemainingSeconds(2, StatusEffectKinds.Burn), Is.Zero);
            Step(1f);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(burntOut), "nothing more once the burn runs out");
            Assert.That(_combat.AreaEffects.Count, Is.EqualTo(1), "the fire is still there");
        }

        [Test]
        public void TheFireStopsBurningWhenItGoesOut()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            _combat.AreaEffects.Spread(_fireArea, Body(2).Position, 1, _fireWeaponId);
            Step(_fireArea.DurationSeconds + FireArea.DefaultBurnSeconds + 0.5f);

            Assert.That(_combat.AreaEffects.Count, Is.Zero);
            Assert.That(_combat.StatusEffects.RemainingSeconds(2, StatusEffectKinds.Burn), Is.Zero);
        }

        [Test]
        public void ABurnFromTheFireIsCreditedToTheThrowerAndTheGrenade()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var deadly = new AreaEffectSpec(2f, 4f, 0.5f, NetMatchSettings.DefaultMaxHealth, visualId: "fire");

            _combat.AreaEffects.Spread(deadly, Body(2).Position, 1, _fireWeaponId);
            Step(1f);

            var killed = _transport.Read<KilledMessage>(2, Copy).Single();
            Assert.That(killed.KillerId, Is.EqualTo(1));
            Assert.That(killed.VictimId, Is.EqualTo(2));
            Assert.That(killed.WeaponId, Is.EqualTo(_fireWeaponId));
        }

        [Test]
        public void AFireWhoseThrowerIsGoneStillBurns()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            _combat.AreaEffects.Spread(_fireArea, Body(2).Position, null, _fireWeaponId);
            Step(1f);

            Assert.That(_combat.StatusEffects.RemainingSeconds(2, StatusEffectKinds.Burn), Is.GreaterThan(0f));
            Assert.That(_log.ToString(), Does.Contain("[StatusEffect] afflict source=none victim=2 kind=burn"));
        }

        [Test]
        public void AnIncendiaryGrenadeLeavesAFireWhereItGoesOff()
        {
            Start(Rules(), grenades: new GrenadeCatalog(new[] { _incendiary }));
            Join(1);
            Join(2);
            Step(0.2f);

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX });
            Step(_incendiary.FuseSeconds + 0.1f);

            Assert.That(_combat.AreaEffects.Count, Is.EqualTo(1));
            var spread = _transport.Read<AreaEffectMessage>(2, Copy).Single();
            Assert.That(spread.KindId, Is.EqualTo("fire"));
            Assert.That(spread.Radius, Is.EqualTo(_fireArea.Radius));
            Assert.That(_log.ToString(), Does.Not.Contain("[Grenade] net exploded"), "an incendiary does not explode");
        }
    }
}
