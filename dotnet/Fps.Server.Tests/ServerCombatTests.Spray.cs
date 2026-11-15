using System;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // 火炎 (RoundDelivery.Spray) は押した・離したの間をサーバーが数え、噴射の間合いごとに円錐の中の相手に当て直す
    public sealed partial class ServerCombatTests
    {
        private const float _sprayTickSeconds = 0.1f;
        private const float _sprayConeDegrees = 15f;
        private const int _sprayDamage = 2;

        // 押した瞬間に 1 回、その後は噴射の間合い (6 tick) ごと
        [TestCase(30, 6)]
        [TestCase(60, 11)]
        public void ASprayDamagesForAsLongAsItIsHeld(int heldTicks, int expectedHits)
        {
            Start(Rules(), rifle: Flamer());
            Join(1);
            Join(2);
            Step(0.2f);

            SprayPress(1, AimAt(1, Body(2).Center));
            for (var i = 0; i < heldTicks; i++)
            {
                Tick();
            }

            Release(1);
            Step(0.5f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - (_sprayDamage * expectedHits)), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Combat] net spray stop shooter=1 weapon=" + TestRules.RifleId + " ticks=" + expectedHits));
        }

        [Test]
        public void ASprayBurnsTheTargetAfterItIsReleased()
        {
            Start(Rules(), rifle: Flamer(burn: new StatusEffectSpec(2f, 0.5f, 3)));
            Join(1);
            Join(2);
            Step(0.2f);

            SprayPress(1, AimAt(1, Body(2).Center));
            Release(1);
            var sprayed = _rules.Session.GetHealth(2);
            Step(1.1f);

            Assert.That(sprayed, Is.EqualTo(NetMatchSettings.DefaultMaxHealth - _sprayDamage), _log.ToString());
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(sprayed - 6), "two burn ticks");
            var burn = _transport.Read<StatusEffectMessage>(2, m => new StatusEffectMessage { TargetId = m.TargetId, KindId = m.KindId }).First();
            Assert.That(burn.TargetId, Is.EqualTo(2));
            Assert.That(burn.KindId, Is.EqualTo(StatusEffectKinds.Burn));
        }

        // 半角 15 度の円錐。10 度ずらしても届き、30 度ずらすと届かない
        [TestCase(10f, true)]
        [TestCase(-10f, true)]
        [TestCase(30f, false)]
        [TestCase(-30f, false)]
        public void OnlyATargetInsideTheConeIsReached(float offAxisDegrees, bool reached)
        {
            Start(Rules(), rifle: Flamer());
            Join(1);
            Join(2);
            Step(0.2f);

            SprayPress(1, TurnedBy(AimAt(1, Body(2).Center), offAxisDegrees * MathF.PI / 180f));
            Release(1);

            var expected = reached ? NetMatchSettings.DefaultMaxHealth - _sprayDamage : NetMatchSettings.DefaultMaxHealth;
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(expected), _log.ToString());
        }

        [Test]
        public void ATargetBeyondTheRangeIsNotReached()
        {
            Start(Rules(), rifle: Flamer(range: 8f));
            Join(1);
            Join(2);
            Step(0.2f);

            SprayPress(1, AimAt(1, Body(2).Center));
            Step(0.5f);
            Release(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
        }

        [Test]
        public void AWallHidesTheTargetAndStopsTheFlame()
        {
            Start(Rules(), wall: true, rifle: Flamer());
            Join(1);
            Join(2);
            Step(0.2f);

            SprayPress(1, AimAt(1, Body(2).Center));
            Step(0.5f);
            Release(1);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
            var firing = _transport.Read<BeamStateMessage>(2, Copy).First();
            Assert.That(firing.Active, Is.True);
            Assert.That(firing.End.X, Is.EqualTo(-0.25f).Within(1e-3f), "the flame ends at the wall");
        }

        [Test]
        public void ASprayIsSentToEveryoneWhileItFiresAndEndsOnRelease()
        {
            Start(Rules(), rifle: Flamer());
            Join(1);
            Join(2);
            Step(0.2f);

            SprayPress(1, AimAt(1, Body(2).Center));
            Step(0.2f);
            Release(1);

            foreach (var receiver in new[] { 1, 2 })
            {
                var states = _transport.Read<BeamStateMessage>(receiver, Copy);
                Assert.That(states.Count, Is.GreaterThan(2));
                var firing = states.Take(states.Count - 1).ToList();
                Assert.That(firing.All(s => s.Active && s.ShooterId == 1 && s.WeaponId == TestRules.RifleId), Is.True);
                Assert.That(Vector3.Distance(firing[0].Start, Body(1).EyePosition), Is.LessThan(1e-3f));
                Assert.That(Vector3.Distance(firing[0].Start, firing[0].End), Is.EqualTo(12f).Within(1e-3f), "the flame passes through people");
                Assert.That(states[^1].Active, Is.False);
            }

            Assert.That(_transport.Read<ShotMessage>(2, m => m), Is.Empty, "a spray is shown by its own messages");
        }

        [Test]
        public void ASprayFollowsTheShootersTurn()
        {
            Start(Rules(), rifle: Flamer());
            Join(1);
            Join(2);
            Step(0.2f);

            SprayPress(1, AimAt(1, Body(2).Center));
            Turn(1, Body(1).Yaw + MathF.PI);
            var health = _rules.Session.GetHealth(2);
            Step(0.5f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(health), _log.ToString());
        }

        [Test]
        public void ASprayUsesARoundEachTickAndStopsWhenTheMagazineIsEmpty()
        {
            const int magazine = 3;
            Start(Rules(), rifle: Flamer(ammo: new AmmoSpec(magazineSize: magazine, reserveAmmo: 0)));
            Join(1);
            Join(2);
            Step(0.2f);

            SprayPress(1, AimAt(1, Body(2).Center));
            Step(1f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - (_sprayDamage * magazine)), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("net spray stop shooter=1 weapon=" + TestRules.RifleId + " ticks=" + magazine));
            Assert.That(_log.ToString(), Does.Contain("reason=" + ShotRejection.OutOfAmmo));
            Assert.That(_transport.Read<BeamStateMessage>(1, Copy).Last().Active, Is.False, "the shooter learns that the server stopped its spray");
        }

        [Test]
        public void AShotWithoutAPressSpraysOnce()
        {
            Start(Rules(), rifle: Flamer());
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);
            Step(0.5f);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - _sprayDamage), _log.ToString());
        }

        private static WeaponSpec Flamer(float range = 12f, AmmoSpec ammo = null, StatusEffectSpec burn = null)
        {
            var rifle = TestRules.Rifle();
            var round = new RoundSpec(RoundDelivery.Spray, range, spray: new SpraySpec(_sprayTickSeconds, _sprayConeDegrees));
            var impact = new ImpactSpec(_sprayDamage, DamageFalloff.None, rifle.Impact.StructureRadius, statusEffect: burn);

            // 散らばりで向きが揺れると円錐の縁の確かめがぶれるので、照準の部品は散らばり無しにする
            return new WeaponSpec(
                rifle.Id, rifle.NameKey, rifle.Category, rifle.Trigger, ammo ?? rifle.Ammo, round, impact, new AimSpec(), rifle.AmmoTypes, rifle.ShotNoiseRadius);
        }

        private Vector3 AimAt(int shooter, Vector3 target)
        {
            return target - Body(shooter).EyePosition;
        }

        private static Vector3 TurnedBy(Vector3 direction, float radians)
        {
            return Vector3.Transform(direction, Quaternion.CreateFromAxisAngle(Vector3.UnitY, radians));
        }

        private void SprayPress(int shooter, Vector3 direction)
        {
            Send(shooter, Trigger(TriggerEdge.Pressed, Body(shooter).EyePosition, direction));
        }
    }
}
