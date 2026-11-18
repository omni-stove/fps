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
    // Edit Mode では Awake も Update も走らないので、MountedGun は TryInitialize で、座席の入力は VehicleSeat.SubmitInput で手で進める。
    // 銃座は原点に +Z 向きで置き、銃の回る点は高さ 1.1m、座席はその 0.9m 後ろ。熱は 1 発 0.25 で、上限で 1 秒止まり、毎秒 0.5 冷める。
    public sealed class MountedGunTests
    {
        private const string HeatGunId = "0c4e9a51-7d2b-4e83-9f16-a5b8c2d3e4f7";
        private const float FireInterval = 0.1f;

        private static readonly Vector3 _gunLocal = new Vector3(0f, 1.1f, 0f);
        private static readonly Vector3 _seatLocal = new Vector3(0f, 0f, -0.9f);

        private readonly List<Object> _created = new List<Object>();
        private Vehicle _vehicle;
        private VehicleSeat _seat;
        private VehicleWeaponMount _gun;
        private MountedGun _mounted;

        [SetUp]
        public void SetUp()
        {
            var root = Track(new GameObject("Post"));
            _seat = new GameObject("Seat").AddComponent<VehicleSeat>();
            _seat.transform.SetParent(root.transform, false);
            _seat.transform.localPosition = _seatLocal;

            _gun = new GameObject("Gun").AddComponent<VehicleWeaponMount>();
            _gun.transform.SetParent(root.transform, false);
            _gun.transform.localPosition = _gunLocal;
            _gun.SetWeapon(HeatGun());

            _vehicle = root.AddComponent<Vehicle>();
            _vehicle.Configure(Spec());
            _mounted = root.AddComponent<MountedGun>();
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
        public void ASingleGunnerSeatWithAWeaponIsAMountedGunWithAShieldOnTheGun()
        {
            Assert.That(_mounted.TryInitialize(), Is.True);

            Assert.That(_mounted.IsReady, Is.True);
            Assert.That(_mounted.Problem, Is.Null);
            Assert.That(_mounted.Seat, Is.SameAs(_seat));
            Assert.That(_mounted.Mount, Is.SameAs(_gun));
            Assert.That(_vehicle.Spec.DriverSeatIndex, Is.EqualTo(-1));
            Assert.That(_vehicle.FiringOf(_seat), Is.EqualTo(VehicleSeatFiring.VehicleWeapon));

            var shield = _mounted.Shield;
            Assert.That(shield, Is.Not.Null);
            Assert.That(shield.transform.parent, Is.SameAs(_gun.transform), "the shield turns with the gun");
            Assert.That(shield.transform.localPosition, Is.EqualTo(MountedGun.PlaceholderShieldLocalPosition));
            Assert.That(shield.transform.localScale, Is.EqualTo(MountedGun.PlaceholderShieldLocalScale));
            Assert.That(shield.Collider, Is.Not.Null);
            Assert.That(_mounted.TryInitialize(), Is.True, "initializing twice makes no second shield");
            Assert.That(_gun.GetComponentsInChildren<BulletShield>().Length, Is.EqualTo(1));
        }

        [Test]
        public void AShieldPrefabReplacesThePlaceholder()
        {
            var prefab = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            prefab.name = "SteelPlate";
            _mounted.ShieldPrefab = prefab;

            Assert.That(_mounted.TryInitialize(), Is.True);

            Assert.That(_mounted.Shield.name, Is.EqualTo("SteelPlate"));
            Assert.That(_gun.transform.Find(BulletShield.PlaceholderName), Is.Null);
        }

        [Test]
        public void AShieldPrefabWithoutARootColliderFallsBackToThePlaceholder()
        {
            var prefab = Track(new GameObject("NoCollider"));
            _mounted.ShieldPrefab = prefab;

            LogAssert.Expect(LogType.Error, new Regex("(?s)\\[MountedGun\\].*shield prefab 'NoCollider' has no collider on its root.*using the placeholder shield"));
            Assert.That(_mounted.TryInitialize(), Is.True);

            Assert.That(_mounted.Shield.name, Is.EqualTo(BulletShield.PlaceholderName));
            Assert.That(_mounted.Shield.Collider, Is.Not.Null);
        }

        [Test]
        public void AVehicleThatIsNotAMountedGunIsRefused()
        {
            var root = Track(new GameObject("Jeep"));
            foreach (var name in new[] { "Driver", "Gunner" })
            {
                new GameObject(name).AddComponent<VehicleSeat>().transform.SetParent(root.transform, false);
            }

            var jeep = root.AddComponent<Vehicle>();
            jeep.Configure(new VehicleSpec(
                "jeep", 100, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver), new VehicleSeatSpec(VehicleSeatRole.Gunner) }));
            var mounted = root.AddComponent<MountedGun>();

            LogAssert.Expect(LogType.Error, "[MountedGun] 'Jeep' is not a mounted gun reason=seat_count");
            Assert.That(mounted.TryInitialize(), Is.False);

            Assert.That(mounted.Problem, Is.EqualTo(MountedGunLog.ReasonSeatCount));
            Assert.That(mounted.IsReady, Is.False);
            Assert.That(mounted.Shield, Is.Null);
            Assert.That(MountedGun.Active, Has.No.Member(mounted));
        }

        [Test]
        public void AFreeGunIsListedUntilSomeoneSitsInIt()
        {
            _mounted.TryInitialize();

            Assert.That(MountedGun.Active, Does.Contain(_mounted));
            Assert.That(_mounted.IsFree, Is.True);
            Assert.That(_mounted.BoardPoint, Is.EqualTo(_seat.SitPosition), "with no enter point the seat itself is where to board");

            var rider = Board();
            Assert.That(_mounted.IsFree, Is.False);

            _vehicle.TryExit(rider);
            Assert.That(_mounted.IsFree, Is.True);

            _vehicle.Wreck();
            Assert.That(_mounted.IsFree, Is.False, "a wrecked gun cannot be used");
        }

        [Test]
        public void ItCanAimOnlyInsideTheGunsRange()
        {
            _mounted.TryInitialize();
            var pivot = _vehicle.transform.TransformPoint(_gunLocal);

            Assert.That(_mounted.CanAimAt(pivot + new Vector3(0f, 0f, 10f)), Is.True);
            Assert.That(_mounted.CanAimAt(pivot + new Vector3(10f, 0f, 10f)), Is.True, "45 degrees right is inside 60");
            Assert.That(_mounted.CanAimAt(pivot + new Vector3(10f, 0f, 1f)), Is.False, "84 degrees right is outside 60");
            Assert.That(_mounted.CanAimAt(pivot + new Vector3(0f, 0f, -10f)), Is.False, "behind");
            Assert.That(_mounted.AimOffsetDegrees(pivot + new Vector3(10f, 0f, 10f)), Is.EqualTo(45f).Within(1e-2f));

            _vehicle.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Assert.That(_mounted.CanAimAt(pivot + new Vector3(10f, 0f, 0f)), Is.True, "the range turns with the gun's base");
            Assert.That(_mounted.CanAimAt(pivot + new Vector3(0f, 0f, 10f)), Is.False);
        }

        [Test]
        public void TheShieldStopsBulletsFromTheFrontButNotFromBehind()
        {
            _mounted.TryInitialize();
            var gunner = CreateBody("Gunner", _vehicle.transform.TransformPoint(_seatLocal + new Vector3(0f, 1.1f, 0f)), 100);

            LogAssert.Expect(LogType.Log, "[AI] id=Post shield blocked dmg=10 by=unknown");
            var front = Hitscan.Fire(new Vector3(0f, 1.1f, 10f), Vector3.back, 50f, 10, DamageFalloff.None, null);

            Assert.That(front.Damage, Is.EqualTo(0));
            Assert.That(gunner.Health.Current, Is.EqualTo(100));

            var back = Hitscan.Fire(new Vector3(0f, 1.1f, -10f), Vector3.forward, 50f, 10, DamageFalloff.None, null);

            Assert.That(back.Damage, Is.EqualTo(10));
            Assert.That(gunner.Health.Current, Is.EqualTo(90));
        }

        [Test]
        public void TheGunnersOwnShotsPassTheShield()
        {
            _mounted.TryInitialize();
            var foe = CreateBody("Foe", _vehicle.transform.TransformPoint(_gunLocal) + new Vector3(0f, 0f, 10f), 1000);
            var rider = Board();

            Submit(rider, true, FireInterval);

            Assert.That(foe.Health.Current, Is.EqualTo(980));
        }

        [Test]
        public void FiringRaisesHeatUntilItLocksThenCoolingLetsItFireAgain()
        {
            _mounted.TryInitialize();
            CreateBody("Foe", _vehicle.transform.TransformPoint(_gunLocal) + new Vector3(0f, 0f, 10f), 1000);
            var rider = Board();

            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape($"[Vehicle] 'Post' fire rider=Gunner seat=0 weapon={HeatGunId}") + ".* heat=0\\.25$"));
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape($"[Vehicle] 'Post' fire rider=Gunner seat=0 weapon={HeatGunId}") + ".* heat=0\\.50$"));
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape($"[Vehicle] 'Post' fire rider=Gunner seat=0 weapon={HeatGunId}") + ".* heat=0\\.75$"));
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape($"[Vehicle] 'Post' fire rider=Gunner seat=0 weapon={HeatGunId}") + ".* heat=1\\.00$"));
            LogAssert.Expect(LogType.Log, $"[Vehicle] 'Post' overheated seat=0 weapon={HeatGunId} lock=1.00");
            var shots = 0;
            _gun.Fired += _ => shots++;
            for (var i = 0; i < 6; i++)
            {
                Submit(rider, true, FireInterval);
            }

            Assert.That(shots, Is.EqualTo(4), "the fourth shot fills the heat and the gun stops");
            Assert.That(_gun.Weapon.Ammo.CanFire, Is.False);

            // 止まる 1 秒のうち 0.2 秒は撃てなかった 2 回で過ぎ、残り 0.8 秒と、毎秒 0.5 で 1 から冷める 2 秒で 2.8 秒。0.5 秒ずつなら 6 回目で冷めきる。
            for (var i = 0; i < 5; i++)
            {
                Submit(rider, false, 0.5f);
            }

            Assert.That(_gun.Weapon.Ammo.CanFire, Is.False, "partly cool is not enough");

            LogAssert.Expect(LogType.Log, $"[Vehicle] 'Post' cooled seat=0 weapon={HeatGunId}");
            Submit(rider, false, 0.5f);
            Assert.That(_gun.Weapon.Ammo.CanFire, Is.True);

            Submit(rider, true, FireInterval);
            Assert.That(shots, Is.EqualTo(5), "it fires again once cool");
        }

        private static WeaponSpec HeatGun()
        {
            return new WeaponSpec(
                HeatGunId,
                "weapon.mountedGun",
                WeaponCategory.AssaultRifle,
                trigger: new TriggerSpec(FireMode.Auto, FireInterval),
                ammo: new AmmoSpec(1, 0, 0f, kind: AmmoKind.Heat, heat: new HeatSpec(0.25f, 0.5f, 0.5f, 1f)),
                round: new RoundSpec(RoundDelivery.Hitscan, 100f),
                impact: new ImpactSpec(20));
        }

        private static VehicleSpec Spec()
        {
            return new VehicleSpec(
                "post",
                300,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Gunner) },
                new[] { new VehicleWeaponSpec(HeatGunId, 0, -60f, 60f, -10f, 25f, 120f) });
        }

        private VehicleTestRider Board()
        {
            var rider = Track(new GameObject("Gunner")).AddComponent<VehicleTestRider>();
            Assert.That(_vehicle.TryEnter(rider, 0, requireInRange: false), Is.True);
            return rider;
        }

        private void Submit(VehicleTestRider rider, bool fire, float seconds)
        {
            var origin = _vehicle.transform.TransformPoint(_gunLocal);
            rider.Seat.SubmitInput(rider, new VehicleSeatInput(Vector2.zero, 0f, false, fire, false, false, origin, _vehicle.transform.forward), seconds);
            Physics.SyncTransforms();
        }

        private Damageable CreateBody(string name, Vector3 position, int health)
        {
            var body = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            body.name = name;
            body.transform.position = position;
            body.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, health);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
