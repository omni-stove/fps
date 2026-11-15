using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    public sealed class ClientWeaponTests
    {
        private const int _playerId = 3;
        private const string _rifleId = "3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13";
        private const string _beamId = "6a1d0c2e-4b3f-4e5a-8c7d-9e0f1a2b3c4d";
        private const string _sprayId = "7b2e1d3f-5c4a-4f6b-9d8e-0f1a2b3c4d5e";
        private const string _ammoTypeId = "8c3f2e4a-6d5b-4a7c-8e9f-1a2b3c4d5e6f";

        [Test]
        public void AnAutoTriggerFiresOnceAnIntervalWhileHeldAndReportsTheEdges()
        {
            var trigger = new ClientTrigger(Rifle(FireMode.Auto, fireInterval: 0.1f));

            var press = trigger.Pull(true, true);
            Assert.That(press.Shots, Is.EqualTo(1));
            Assert.That(press.Pressed, Is.True);
            Assert.That(press.Released, Is.False);
            Assert.That(trigger.IsHeld, Is.True);

            trigger.Tick(0.05f);
            Assert.That(trigger.Pull(true, false).Shots, Is.Zero, "the interval has not passed");

            trigger.Tick(0.05f);
            var held = trigger.Pull(true, false);
            Assert.That(held.Shots, Is.EqualTo(1));
            Assert.That(held.Pressed, Is.False);

            var release = trigger.Pull(false, false);
            Assert.That(release.Shots, Is.Zero);
            Assert.That(release.Released, Is.True);
            Assert.That(trigger.IsHeld, Is.False);
        }

        [Test]
        public void ASemiTriggerFiresOnAPressAndReleaseInTheSameFrame()
        {
            var trigger = new ClientTrigger(Rifle(FireMode.Semi, fireInterval: 0.2f));

            var tap = trigger.Pull(false, true);

            Assert.That(tap.Shots, Is.EqualTo(1));
            Assert.That(tap.Pressed, Is.True);
            Assert.That(tap.Released, Is.True);
            Assert.That(trigger.IsHeld, Is.False);
            Assert.That(trigger.Pull(true, true).Shots, Is.Zero, "pressing again within the interval does not fire");
        }

        [Test]
        public void TheTriggerFiresAtTheSamePaceAsTheFireTrigger()
        {
            var spec = Rifle(FireMode.Auto, fireInterval: 0.1f);
            var client = new ClientTrigger(spec);
            var reference = new FireTrigger(spec);
            var clientShots = 0;
            var referenceShots = 0;
            for (var frame = 0; frame < 120; frame++)
            {
                var pressed = frame % 40 == 0;
                var held = frame % 40 < 30;
                client.Tick(1f / 60f);
                reference.Tick(1f / 60f);
                clientShots += client.Pull(held, pressed).Shots;
                referenceShots += reference.Pull(held, pressed) ? 1 : 0;
            }

            Assert.That(clientShots, Is.EqualTo(referenceShots));
            Assert.That(clientShots, Is.GreaterThan(3));
        }

        [Test]
        public void DroppingTheTriggerLetsGoOfAHeldPress()
        {
            var trigger = new ClientTrigger(Rifle(FireMode.Auto));
            Assert.That(trigger.Drop(), Is.False);

            trigger.Pull(true, true);

            Assert.That(trigger.Drop(), Is.True);
            Assert.That(trigger.IsHeld, Is.False);
        }

        [Test]
        public void OnlyBeamsAndSpraysAreFiredByPressAndRelease()
        {
            Assert.That(new ClientTrigger(Rifle(FireMode.Auto)).IsContinuous, Is.False);
            Assert.That(new ClientTrigger(Beam()).IsContinuous, Is.True);
            Assert.That(new ClientTrigger(Spray()).IsContinuous, Is.True);
            Assert.That(new ClientTrigger(Rifle(FireMode.Auto)).BlocksAds, Is.False);
        }

        [Test]
        public void TheFireRequestCarriesTheChargeTheServersAmmoTypeTheLockAndTheEdge()
        {
            var request = new FireRequestMessage();
            var owner = Armed(new WeaponSync { WeaponCount = 1, AmmoTypeIndex = 2 }).Owner;

            ClientFire.Fill(request, owner, _rifleId, new TriggerPull(1, true, false, 0.75f), TriggerEdge.Pressed, 9);

            Assert.That(request.WeaponId, Is.EqualTo(_rifleId));
            Assert.That(request.ChargeFraction, Is.EqualTo(0.75f));
            Assert.That(request.AmmoTypeIndex, Is.EqualTo(2));
            Assert.That(request.HasLockTarget, Is.True);
            Assert.That(request.LockTargetId, Is.EqualTo(9));
            Assert.That(request.Trigger, Is.EqualTo(TriggerEdge.Pressed));

            ClientFire.Fill(request, owner, _rifleId, new TriggerPull(1, false, false, 0f), TriggerEdge.None, null);

            Assert.That(request.HasLockTarget, Is.False);
            Assert.That(request.LockTargetId, Is.Zero);
            Assert.That(request.Trigger, Is.EqualTo(TriggerEdge.None));
        }

        [Test]
        public void AHeatWeaponFiresWithoutAMagazineUntilItOverheats()
        {
            var standing = new CharacterMotorState(new Vector3(0f, 1.1f, 0f), Vector3.Zero, true, false);
            var heat = Heated();

            Assert.That(
                ClientFire.Decide(Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 0, Heat = 0.5f }), _playerId, standing, heat),
                Is.EqualTo(FireDecision.Fire));
            Assert.That(
                ClientFire.Decide(Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 0, Heat = 1f }), _playerId, standing, heat),
                Is.EqualTo(FireDecision.Hold));
            Assert.That(
                ClientFire.Decide(Armed(new WeaponSync { WeaponCount = 1, AmmoInMagazine = 0 }), _playerId, standing, Rifle(FireMode.Auto)),
                Is.EqualTo(FireDecision.Reload),
                "a magazine weapon still asks to reload");
        }

        [Test]
        public void TheNewWeaponMessagesGoToTheirHooksAndOthersAreLeftAlone()
        {
            var hooks = new ClientWeaponHooks(Find);

            Assert.That(hooks.Handle(new ProjectileSpawnedMessage { ProjectileId = 1, WeaponId = _rifleId }), Is.True);
            Assert.That(hooks.Handle(new ProjectileEndedMessage { ProjectileId = 1 }), Is.True);
            Assert.That(hooks.Handle(new BeamStateMessage { ShooterId = 2, WeaponId = _beamId, Active = true }), Is.True);
            Assert.That(hooks.Handle(new BeamStateMessage { ShooterId = 2, WeaponId = _sprayId, Active = true }), Is.True);
            Assert.That(hooks.Handle(new BeamStateMessage { ShooterId = 2, WeaponId = "unknown" }), Is.True);
            Assert.That(hooks.Handle(new StatusEffectMessage { TargetId = 2, KindId = "burn", RemainingSeconds = 1f }), Is.True);
            Assert.That(hooks.Handle(new AreaEffectMessage { AreaId = 1, KindId = "smoke", Radius = 2f, RemainingSeconds = 3f }), Is.True);
            Assert.That(hooks.Handle(new ShotMessage()), Is.False);
            Assert.That(hooks.Handle(new PingRemovedMessage()), Is.False);

            hooks.Apply(new SnapshotMessage());
            hooks.LocalFired(new FireRequestMessage { WeaponId = _beamId, Trigger = TriggerEdge.Pressed }, Beam());
            hooks.LocalFired(new FireRequestMessage { WeaponId = _rifleId }, Rifle(FireMode.Auto));
        }

        [Test]
        public void TheHudShowsOnlyTheGaugesTheWeaponHas()
        {
            var model = new HudModel();
            var sync = new WeaponSync { WeaponCount = 1, ChargeFraction = 0.4f, Heat = 0.6f, AmmoTypeIndex = 0 };

            ClientWeaponGauges.Show(model, sync, Rifle(FireMode.Auto));

            Assert.That(model.ChargeFraction, Is.Null);
            Assert.That(model.HeatFraction, Is.Null);
            Assert.That(model.AmmoTypeText, Is.Empty);

            ClientWeaponGauges.Show(model, sync, Charged());

            Assert.That(model.ChargeFraction, Is.EqualTo(0.4f));
            Assert.That(model.AmmoTypeText, Is.EqualTo("items.ammo.armor_piercing"));

            ClientWeaponGauges.Show(model, sync, Heated());

            Assert.That(model.ChargeFraction, Is.Null);
            Assert.That(model.HeatFraction, Is.EqualTo(0.6f));
            Assert.That(model.AmmoTypeText, Is.Empty);

            ClientWeaponGauges.Show(model, new WeaponSync(), Heated());

            Assert.That(model.HeatFraction, Is.Null, "no weapon, no gauge");
        }

        [Test]
        public void ShotsKeepEverySegmentTheServerSent()
        {
            var shot = new ShotMessage { WeaponId = _rifleId, Origin = Vector3.Zero, Direction = Vector3.UnitZ };
            ref var pellet = ref shot.AddSegment();
            pellet.Origin = Vector3.Zero;
            pellet.Direction = Vector3.UnitX;
            pellet.HasImpact = true;
            pellet.ImpactPoint = new Vector3(5f, 0f, 0f);
            pellet.ImpactNormal = -Vector3.UnitX;
            pellet.ImpactMaterialId = "concrete";

            Assert.That(shot.SegmentCount, Is.EqualTo(2));
            Assert.That(shot.HasImpact, Is.False, "the first segment stays the one-ray shot");
            Assert.That(shot.Segment(1).ImpactPoint, Is.EqualTo(new Vector3(5f, 0f, 0f)));
        }

        private static WeaponSpec Find(string weaponId)
        {
            switch (weaponId)
            {
                case _beamId:
                    return Beam();
                case _sprayId:
                    return Spray();
                case _rifleId:
                    return Rifle(FireMode.Auto);
                default:
                    return null;
            }
        }

        private static WeaponSpec Rifle(FireMode mode, float fireInterval = 0.1f)
        {
            return new WeaponSpec(_rifleId, "", WeaponCategory.AssaultRifle, new TriggerSpec(mode, fireInterval));
        }

        private static WeaponSpec Beam()
        {
            return new WeaponSpec(_beamId, "", WeaponCategory.AssaultRifle, round: new RoundSpec(RoundDelivery.Beam, beam: new BeamSpec(0.1f)));
        }

        private static WeaponSpec Spray()
        {
            return new WeaponSpec(
                _sprayId, "", WeaponCategory.AssaultRifle, round: new RoundSpec(RoundDelivery.Spray, range: 8f, spray: new SpraySpec(0.1f, 30f)));
        }

        private static WeaponSpec Charged()
        {
            return new WeaponSpec(
                _rifleId,
                "",
                WeaponCategory.AssaultRifle,
                new TriggerSpec(FireMode.Semi, 0.5f, charge: new ChargeSpec(1f, 0f, 2f)),
                ammoTypes: new[] { new AmmoTypeSpec(_ammoTypeId, "items.ammo.armor_piercing") });
        }

        private static WeaponSpec Heated()
        {
            return new WeaponSpec(
                _rifleId, "", WeaponCategory.AssaultRifle, ammo: new AmmoSpec(kind: AmmoKind.Heat, heat: new HeatSpec(0.1f, 0.5f, 0.2f, 1f)));
        }

        private static SnapshotMessage Armed(WeaponSync weapon)
        {
            var snapshot = new SnapshotMessage { Tick = 10, HasOwner = true };
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = _playerId;
            row.Health = 100;
            row.WeaponId = _rifleId;
            snapshot.Owner.EquippedWeaponIds.Add(_rifleId);
            snapshot.Owner.Weapon = weapon;
            return snapshot;
        }
    }
}
