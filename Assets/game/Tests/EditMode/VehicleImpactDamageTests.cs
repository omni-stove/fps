using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 物理は進めず、ぶつかった Collider と速さと時刻を TryHit に直接渡す。
    public sealed class VehicleImpactDamageTests
    {
        private readonly List<Object> _created = new List<Object>();
        private Vehicle _vehicle;
        private VehicleImpactDamage _impact;
        private Damageable _target;
        private Collider _targetCollider;
        private readonly List<DamageReport> _reports = new List<DamageReport>();
        private ShotAttribution? _attribution;

        [SetUp]
        public void SetUp()
        {
            _vehicle = TestCars.Create(_created);
            _impact = _vehicle.gameObject.AddComponent<VehicleImpactDamage>();
            _impact.Configure(new VehicleImpactSpec(4f, 10f, 200, 1f));

            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "Dummy";
            _created.Add(target);
            target.transform.position = new Vector3(0f, 1f, 4f);
            _target = target.AddComponent<Damageable>();
            _target.Initialize("Dummy", 100);
            _targetCollider = target.GetComponent<Collider>();
            _target.Damaged += OnDamaged;
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
            _reports.Clear();
            _attribution = null;
        }

        [Test]
        public void HittingFastDamagesByTheSpeedAsVehicleDamage()
        {
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' ram target=Dummy dmg=80 speed=12.0 by=- hp=20");

            Assert.That(_impact.TryHit(_targetCollider, 12f, 0f), Is.EqualTo(80));

            Assert.That(_target.Health.Current, Is.EqualTo(20));
            Assert.That(_reports.Count, Is.EqualTo(1));
            Assert.That(_reports[0].Kind, Is.EqualTo(DamageKind.Vehicle));
            Assert.That(_reports[0].SourcePosition, Is.EqualTo(_vehicle.transform.position));
            Assert.That(_attribution, Is.Null, "nobody drives, so the hit belongs to nobody");
        }

        [Test]
        public void HittingSlowlyDoesNoDamage()
        {
            Assert.That(_impact.TryHit(_targetCollider, 4f, 0f), Is.EqualTo(0));
            Assert.That(_target.Health.Current, Is.EqualTo(100));
            Assert.That(_reports, Is.Empty);
        }

        [Test]
        public void TheDriverIsCreditedWithTheHit()
        {
            var driver = TestCars.Board(_vehicle, "Driver", 0, _created);
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' ram target=Dummy dmg=40 speed=8.0 by=Driver hp=60");

            _impact.TryHit(_targetCollider, 8f, 0f);

            Assert.That(_attribution.HasValue, Is.True);
            Assert.That(_attribution.Value.KillerId, Is.EqualTo(CombatEvents.OfflineId(driver.gameObject)));
            Assert.That(_attribution.Value.WeaponId, Is.EqualTo(TestCars.VehicleId));
            Assert.That(CombatEvents.CurrentAttribution, Is.Null, "the attribution ends with the hit");
        }

        [Test]
        public void ARamByTheLocalPlayerShowsTheHitOnTheirScreen()
        {
            var driver = TestCars.Board(_vehicle, "Driver", 0, _created);
            var body = driver.gameObject.AddComponent<Damageable>();
            body.Initialize("Driver", 100);
            body.IsLocalPlayer = true;
            var confirms = new List<HitConfirm>();
            void Record(HitConfirm confirm) => confirms.Add(confirm);
            CombatEvents.HitConfirmed += Record;
            try
            {
                LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' ram target=Dummy dmg=40 speed=8.0 by=Driver hp=60");
                _impact.TryHit(_targetCollider, 8f, 0f);

                Assert.That(confirms.Count, Is.EqualTo(1));
                Assert.That(confirms[0].Damage, Is.EqualTo(40));
                Assert.That(confirms[0].Killed, Is.False);
                Assert.That(confirms[0].Point.HasValue, Is.True, "the damage number is placed on the target");

                LogAssert.Expect(LogType.Log, "[Combat] target died id=Dummy");
                LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' ram target=Dummy dmg=200 speed=30.0 by=Driver hp=0");
                _impact.TryHit(_targetCollider, 30f, 2f);
                Assert.That(confirms.Count, Is.EqualTo(2));
                Assert.That(confirms[1].Killed, Is.True, "running someone over shows the kill");
            }
            finally
            {
                CombatEvents.HitConfirmed -= Record;
            }
        }

        [Test]
        public void ARamWithoutTheLocalPlayerDrivingShowsNothing()
        {
            TestCars.Board(_vehicle, "Driver", 0, _created);
            var confirms = 0;
            void Record(HitConfirm _) => confirms++;
            CombatEvents.HitConfirmed += Record;
            try
            {
                LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' ram target=Dummy dmg=40 speed=8.0 by=Driver hp=60");
                _impact.TryHit(_targetCollider, 8f, 0f);
                LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' ram target=Dummy dmg=40 speed=8.0 by=- hp=20");
                _vehicle.ForceExit(_vehicle.OccupantOf(0), VehicleLeaveReason.Forced);
                _impact.TryHit(_targetCollider, 8f, 2f);

                Assert.That(confirms, Is.Zero);
            }
            finally
            {
                CombatEvents.HitConfirmed -= Record;
            }
        }

        [Test]
        public void TheSameTargetIsHitAgainOnlyAfterTheRepeatTime()
        {
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' ram target=Dummy dmg=20 speed=6.0 by=- hp=80");
            Assert.That(_impact.TryHit(_targetCollider, 6f, 10f), Is.EqualTo(20));
            Assert.That(_impact.TryHit(_targetCollider, 6f, 10.5f), Is.EqualTo(0));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' ram target=Dummy dmg=20 speed=6.0 by=- hp=60");
            Assert.That(_impact.TryHit(_targetCollider, 6f, 11f), Is.EqualTo(20));
        }

        [Test]
        public void TheCarItselfItsRidersAndTheDeadAreNotHit()
        {
            // 乗り物は耐久の Damageable を自分で付けるので、足さずにそれを見る
            var ownDamageable = _vehicle.GetComponent<Damageable>();
            Assert.That(ownDamageable, Is.Not.Null);
            var ownHealth = ownDamageable.Health.Current;
            Assert.That(_impact.TryHit(_vehicle.transform.Find("Body").GetComponent<Collider>(), 20f, 0f), Is.EqualTo(0));
            Assert.That(ownDamageable.Health.Current, Is.EqualTo(ownHealth));

            var passenger = TestCars.Board(_vehicle, "Passenger", 1, _created);
            var passengerBody = passenger.gameObject.AddComponent<CapsuleCollider>();
            var passengerHealth = passenger.gameObject.AddComponent<Damageable>();
            passengerHealth.Initialize("Passenger", 100);
            Assert.That(_impact.TryHit(passengerBody, 20f, 0f), Is.EqualTo(0));
            Assert.That(passengerHealth.Health.Current, Is.EqualTo(100));

            _target.Kill();
            Assert.That(_impact.TryHit(_targetCollider, 20f, 0f), Is.EqualTo(0));
            Assert.That(_impact.TryHit(null, 20f, 0f), Is.EqualTo(0));
        }

        [Test]
        public void TheSpecIsBuiltFromTheDefaultsWhenNotConfigured()
        {
            var plain = TestCars.Create(_created).gameObject.AddComponent<VehicleImpactDamage>();

            Assert.That(plain.Spec.MinSpeed, Is.EqualTo(VehicleImpactSpec.DefaultMinSpeed));
            Assert.That(plain.Spec.DamagePerSpeed, Is.EqualTo(VehicleImpactSpec.DefaultDamagePerSpeed));
            Assert.That(plain.Spec.MaxDamage, Is.EqualTo(VehicleImpactSpec.DefaultMaxDamage));
            Assert.That(plain.Spec.RepeatSeconds, Is.EqualTo(VehicleImpactSpec.DefaultRepeatSeconds));
            Assert.Throws<ArgumentNullException>(() => plain.Configure(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => plain.TryHit(_targetCollider, -1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => plain.TryHit(_targetCollider, 1f, float.NaN));
        }

        private void OnDamaged(Damageable damageable, DamageReport report)
        {
            _reports.Add(report);
            _attribution = CombatEvents.CurrentAttribution;
        }
    }
}
