using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update が走らないので、座席の入力は VehicleSeat.SubmitInput で手で流す。動かした後は SyncTransforms する。
    // 乗り物は原点に +Z 向きで置き、座席は運転席 (0) と銃座 (1)。銃は銃座が操作し、左右 -90〜90 度・上下 -10〜30 度・90 度/秒で回る。
    public sealed class VehicleWeaponMountTests
    {
        private const float Step = 0.05f;
        private const float Epsilon = 1e-3f;

        private static readonly Vector3 _gunLocal = new Vector3(0f, 2f, 0f);

        private readonly List<Object> _created = new List<Object>();
        private Vehicle _vehicle;
        private VehicleSeat _driver;
        private VehicleSeat _gunner;
        private VehicleWeaponMount _gun;

        [SetUp]
        public void SetUp()
        {
            var root = Track(new GameObject("Tank"));
            _driver = CreateSeat(root.transform, "Driver", new Vector3(-0.5f, 0.4f, 0f));
            _gunner = CreateSeat(root.transform, "Gunner", new Vector3(0f, 1.2f, -1f));

            _gun = new GameObject("Gun").AddComponent<VehicleWeaponMount>();
            _gun.transform.SetParent(root.transform, false);
            _gun.transform.localPosition = _gunLocal;
            _gun.SetWeapon(TestWeapons.RifleSpec());

            _vehicle = root.AddComponent<Vehicle>();
            _vehicle.Configure(Spec(TestWeapons.RifleId, 1));
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheVehicleCollectsItsWeaponsAndTheSeatThatFiresThem()
        {
            Assert.That(_vehicle.WeaponMounts, Is.EqualTo(new[] { _gun }));
            Assert.That(_gun.Vehicle, Is.SameAs(_vehicle));
            Assert.That(_gun.Index, Is.EqualTo(0));
            Assert.That(_gun.SeatIndex, Is.EqualTo(1));
            Assert.That(_gun.Weapon.Spec.Id, Is.EqualTo(TestWeapons.RifleId));
            Assert.That(_vehicle.WeaponMountsOf(1), Is.EqualTo(new[] { _gun }));
            Assert.That(_vehicle.WeaponMountsOf(0), Is.Empty);
            Assert.That(_vehicle.FiringOf(_gunner), Is.EqualTo(VehicleSeatFiring.VehicleWeapon));
            Assert.That(_vehicle.FiringOf(_driver), Is.EqualTo(VehicleSeatFiring.None), "the driver cannot shoot");
            Assert.That(_vehicle.Spec.Weapons[0].YawMaxDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void TheSpecIsBuiltFromTheMountsWhenNotConfigured()
        {
            var root = Track(new GameObject("Post"));
            CreateSeat(root.transform, "Gun seat", Vector3.zero);
            var gun = new GameObject("Gun").AddComponent<VehicleWeaponMount>();
            gun.transform.SetParent(root.transform, false);
            gun.SetWeapon(TestWeapons.HandgunSpec());
            var post = root.AddComponent<Vehicle>();

            Assert.That(post.Spec.Weapons.Count, Is.EqualTo(1));
            Assert.That(post.Spec.Weapons[0].WeaponId, Is.EqualTo(TestWeapons.HandgunId));
            Assert.That(post.Spec.Weapons[0].SeatIndex, Is.EqualTo(0));
            Assert.That(post.Spec.Weapons[0].TurnSpeedDegreesPerSecond, Is.EqualTo(VehicleWeaponSpec.DefaultTurnSpeedDegreesPerSecond));
        }

        [Test]
        public void TheGunnerShootsATargetInFrontWithThePlayersDamageRules()
        {
            var rider = Board(1);
            var foe = CreateBody("Foe", new Vector3(0f, 2f, 10f), 1000);

            LogAssert.Expect(
                LogType.Log,
                new Regex("^" + Regex.Escape($"[Vehicle] 'Tank' fire rider=Gunner seat=1 weapon={TestWeapons.RifleId} yaw=0.0 pitch=0.0 hit=Foe dmg=25 ammo=29") + "$"));
            Submit(rider, Vector3.forward, true);

            Assert.That(foe.Health.Current, Is.EqualTo(975), "25 damage, no falloff at 10 m");
            Assert.That(_gun.Weapon.AmmoInMagazine, Is.EqualTo(29));
        }

        [Test]
        public void TheGunTurnsAtItsSpeedAndNeverPastItsRange()
        {
            var rider = Board(1);

            Submit(rider, Vector3.right, false, 0.5f);
            Assert.That(_gun.Aim.YawDegrees, Is.EqualTo(45f).Within(Epsilon), "90 degrees per second for half a second");

            for (var i = 0; i < 10; i++)
            {
                Submit(rider, -Vector3.forward + (Vector3.right * 0.1f), false, 0.5f);
            }

            Assert.That(_gun.Aim.YawDegrees, Is.EqualTo(90f).Within(Epsilon), "aiming behind stops at the right edge");
            Assert.That(Vector3.Angle(_gun.transform.forward, Vector3.right), Is.LessThan(0.1f));

            for (var i = 0; i < 10; i++)
            {
                Submit(rider, Vector3.up, false, 0.5f);
            }

            Assert.That(_gun.Aim.PitchDegrees, Is.EqualTo(30f).Within(Epsilon));
        }

        [Test]
        public void ItShootsWhereTheGunPointsNotWhereTheRiderLooks()
        {
            var rider = Board(1);
            var behind = CreateBody("Behind", new Vector3(0f, 2f, -10f), 1000);
            var right = CreateBody("Right", new Vector3(10f, 2f, 0f), 1000);

            for (var i = 0; i < 10; i++)
            {
                Submit(rider, Vector3.back + (Vector3.right * 0.01f), false, 0.5f);
            }

            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape("[Vehicle] 'Tank' fire rider=Gunner seat=1") + ".* yaw=90.0 .*hit=Right "));
            Submit(rider, Vector3.back + (Vector3.right * 0.01f), true);

            Assert.That(behind.Health.Current, Is.EqualTo(1000), "the gun cannot turn to face the rear");
            Assert.That(right.Health.Current, Is.LessThan(1000));
        }

        [Test]
        public void TheGunFollowsTheVehicleWhenItTurns()
        {
            var rider = Board(1);
            _vehicle.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Physics.SyncTransforms();

            Submit(rider, Vector3.right, false);

            Assert.That(_gun.Aim.YawDegrees, Is.Zero.Within(Epsilon), "world right is the turned vehicle's front");
            Assert.That(Vector3.Angle(_gun.AimDirection, Vector3.right), Is.LessThan(0.1f));
        }

        [Test]
        public void OnlyTheOperatingSeatDrivesTheGun()
        {
            var rider = Board(0);
            var foe = CreateBody("Foe", new Vector3(0f, 2f, 10f), 1000);

            Submit(rider, Vector3.right, true, 0.5f);

            Assert.That(_gun.Aim.YawDegrees, Is.Zero);
            Assert.That(foe.Health.Current, Is.EqualTo(1000));
            Assert.That(_gun.Weapon.AmmoInMagazine, Is.EqualTo(30));
        }

        [Test]
        public void SemiAutomaticWeaponsFireOncePerPress()
        {
            _gun.SetWeapon(TestWeapons.HandgunSpec());
            _vehicle.Configure(Spec(TestWeapons.HandgunId, 1));
            var rider = Board(1);
            CreateBody("Foe", new Vector3(0f, 2f, 10f), 1000);

            for (var i = 0; i < 20; i++)
            {
                Submit(rider, Vector3.forward, true);
            }

            Assert.That(_gun.Weapon.AmmoInMagazine, Is.EqualTo(11), "holding the trigger fires one round");

            Submit(rider, Vector3.forward, false);
            Submit(rider, Vector3.forward, true);
            Assert.That(_gun.Weapon.AmmoInMagazine, Is.EqualTo(10));
        }

        [Test]
        public void ReloadingRefillsTheMagazine()
        {
            var rider = Board(1);
            Submit(rider, Vector3.forward, true);
            Submit(rider, Vector3.forward, false);
            Assert.That(_gun.Weapon.AmmoInMagazine, Is.EqualTo(29));

            _gunner.SubmitInput(rider, new VehicleSeatInput(Vector2.zero, 0f, false, false, false, true, Vector3.zero, Vector3.forward), Step);
            Assert.That(_gun.Weapon.IsReloading, Is.True);

            for (var i = 0; i < 50; i++)
            {
                Submit(rider, Vector3.forward, false);
            }

            Assert.That(_gun.Weapon.AmmoInMagazine, Is.EqualTo(30));
        }

        [Test]
        public void ADriverWeaponLetsTheDriverShoot()
        {
            _vehicle.Configure(Spec(TestWeapons.RifleId, 0));
            var rider = Board(0);
            var foe = CreateBody("Foe", new Vector3(0f, 2f, 10f), 1000);

            Assert.That(_vehicle.FiringOf(_driver), Is.EqualTo(VehicleSeatFiring.VehicleWeapon));
            Submit(rider, Vector3.forward, true);

            Assert.That(foe.Health.Current, Is.LessThan(1000));
        }

        [Test]
        public void ConfiguringAnotherWeaponNeedsTheCatalog()
        {
            var handgun = Spec(TestWeapons.HandgunId, 1);

            Assert.Throws<ArgumentException>(() => _vehicle.Configure(handgun));
            Assert.That(_gun.WeaponSpec.Id, Is.EqualTo(TestWeapons.RifleId), "a refused spec changes nothing");
            Assert.Throws<ArgumentException>(
                () => _vehicle.Configure(new VehicleSpec("tank", 100, _vehicle.Spec.Seats)), "the weapon count must match the mounts");

            _vehicle.Configure(handgun, new WeaponCatalog(new[] { TestWeapons.RifleSpec(), TestWeapons.HandgunSpec() }));

            Assert.That(_gun.WeaponSpec.Id, Is.EqualTo(TestWeapons.HandgunId));
            Assert.That(_gun.Weapon.Spec.Id, Is.EqualTo(TestWeapons.HandgunId));
            Assert.Throws<ArgumentException>(() => _vehicle.Configure(Spec("missing", 1), new WeaponCatalog(new[] { TestWeapons.RifleSpec() })));
        }

        [Test]
        public void AMountWithoutAWeaponCannotBuildItsSpec()
        {
            var gun = Track(new GameObject("Bare")).AddComponent<VehicleWeaponMount>();

            Assert.That(gun.HasWeapon, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = gun.Spec);
            Assert.Throws<ArgumentNullException>(() => gun.SetWeapon((WeaponSpec)null));
            Assert.Throws<ArgumentException>(() => _gun.ApplySpec(new VehicleWeaponSpec("other", 1)));
        }

        private static VehicleSpec Spec(string weaponId, int seatIndex)
        {
            return new VehicleSpec(
                "tank",
                500,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver),
                    new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing),
                },
                new[] { new VehicleWeaponSpec(weaponId, seatIndex, -90f, 90f, -10f, 30f, 90f) });
        }

        private VehicleTestRider Board(int seat)
        {
            var rider = Track(new GameObject(seat == 0 ? "Driver" : "Gunner")).AddComponent<VehicleTestRider>();
            Assert.That(_vehicle.TryEnter(rider, seat, requireInRange: false), Is.True);
            return rider;
        }

        private void Submit(VehicleTestRider rider, Vector3 aim, bool fire, float seconds = Step)
        {
            var origin = _vehicle.transform.TransformPoint(_gunLocal);
            rider.Seat.SubmitInput(rider, new VehicleSeatInput(Vector2.zero, 0f, false, fire, false, false, origin, aim), seconds);
            Physics.SyncTransforms();
        }

        private Damageable CreateBody(string name, Vector3 position, int health)
        {
            var body = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            body.name = name;
            body.transform.position = position;
            body.transform.localScale = new Vector3(2f, 2f, 2f);
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, health);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        private VehicleSeat CreateSeat(Transform parent, string name, Vector3 localPosition)
        {
            var seat = new GameObject(name).AddComponent<VehicleSeat>();
            seat.transform.SetParent(parent, false);
            seat.transform.localPosition = localPosition;
            return seat;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
