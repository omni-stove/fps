using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update が走らないので、敵の判断は Brain.Tick に Perceive を渡して、銃手は Tick で手で進める。
    // 銃座は原点に +Z 向き (左右 -60〜60 度) で、座席は銃の 0.9m 後ろ。敵はその左に立ち、目標は銃の正面 15m に置く。
    public sealed class EnemyMountedGunnerTests
    {
        private const float Step = 0.1f;

        private static readonly Vector3 _gunLocal = new Vector3(0f, 1.1f, 0f);
        private static readonly Vector3 _seatLocal = new Vector3(0f, 0f, -0.9f);
        private static readonly Vector3 _front = new Vector3(0f, 1.1f, 15f);
        private static readonly Vector3 _behind = new Vector3(0f, 1.1f, -15f);

        private readonly List<Object> _created = new List<Object>();
        private EnemyAgent _agent;
        private EnemyMountedGunner _gunner;
        private MountedGun _gun;
        private Damageable _target;

        [SetUp]
        public void SetUp()
        {
            var rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());

            var root = Track(new GameObject("Post"));
            var seat = new GameObject("Seat").AddComponent<VehicleSeat>();
            seat.transform.SetParent(root.transform, false);
            seat.transform.localPosition = _seatLocal;
            var mount = new GameObject("Gun").AddComponent<VehicleWeaponMount>();
            mount.transform.SetParent(root.transform, false);
            mount.transform.localPosition = _gunLocal;
            mount.SetWeapon(TestWeapons.RifleSpec());
            var vehicle = root.AddComponent<Vehicle>();
            vehicle.Configure(new VehicleSpec(
                "post",
                300,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Gunner) },
                new[] { new VehicleWeaponSpec(TestWeapons.RifleId, 0, -60f, 60f, -10f, 25f, 120f) }));
            _gun = root.AddComponent<MountedGun>();
            Assert.That(_gun.TryInitialize(), Is.True);

            _target = CreateBody("Player", _front, 1000);

            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            host.name = "Enemy";
            host.transform.position = new Vector3(-1.5f, 1f, -0.9f);
            _agent = host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = rifle;
            _agent.Id = "enemy-1";
            host.GetComponent<Damageable>().Initialize("enemy-1", 100);
            Assert.That(_agent.TryInitialize(), Is.True);
            _agent.Target = _target;
            _gunner = host.AddComponent<EnemyMountedGunner>();
            _gunner.ApplySpec(new MountedGunUseSpec(6f, 1f, 5f));
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
        public void ItJoinsTheBrainAndPicksAFreeGunThatCanReachTheTarget()
        {
            _gunner.Tick(Step);

            Assert.That(_gunner.IsAttached, Is.True);
            Assert.That(_agent.Brain.Behaviors, Does.Contain(_gunner.Behavior));
            Assert.That(_gunner.Behavior.Spec.SeekRadius, Is.EqualTo(6f));
            Assert.That(_gunner.Candidate, Is.SameAs(_gun));
            Assert.That(_gunner.Behavior.Candidate.HasValue, Is.True);
        }

        [Test]
        public void TickRejectsANegativeOrNonFiniteStep()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _gunner.Tick(-0.1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _gunner.Tick(float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _gunner.Tick(float.PositiveInfinity));
        }

        [Test]
        public void AGunThatCannotTurnToTheTargetIsNotACandidate()
        {
            _target.transform.position = _behind;
            Physics.SyncTransforms();

            _gunner.Tick(Step);

            Assert.That(_gunner.Candidate, Is.Null);
            Assert.That(_gunner.Behavior.Candidate.HasValue, Is.False);
        }

        [Test]
        public void WithoutATargetItLooksForNoGun()
        {
            _agent.Target = null;

            _gunner.Tick(Step);

            Assert.That(_gunner.Candidate, Is.Null);
        }

        [Test]
        public void AnEngagedEnemyBoardsTheGunAndShootsTheTarget()
        {
            Engage();

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape("[MountedGun] 'enemy-1' seek gun=Post distance=")));
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Post' board rider=enemy-1 seat=0 role=Gunner");
            LogAssert.Expect(LogType.Log, "[MountedGun] 'enemy-1' man gun=Post");
            _gunner.Tick(Step);

            Assert.That(_agent.IsRiding, Is.True);
            Assert.That(_gunner.Manned, Is.SameAs(_gun));
            Assert.That(_gun.IsFree, Is.False);

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape("[Vehicle] 'Post' fire rider=enemy-1 seat=0 weapon=" + TestWeapons.RifleId) + ".* hit=Player "));
            _gunner.Tick(Step);
            Physics.SyncTransforms();

            Assert.That(_target.Health.Current, Is.LessThan(1000));
        }

        [Test]
        public void ItLeavesWhenTheTargetMovesOutOfTheGunsRangeAndWaitsBeforeSeekingAgain()
        {
            Engage();
            _gunner.Tick(Step);
            Assert.That(_agent.IsRiding, Is.True);

            _target.transform.position = _behind;
            Physics.SyncTransforms();

            _gunner.Tick(0.6f);
            Assert.That(_agent.IsRiding, Is.True, "still within the give-up time");

            LogAssert.Expect(LogType.Log, "[MountedGun] 'enemy-1' leave gun=Post cause=OutOfArc");
            _gunner.Tick(0.6f);

            Assert.That(_agent.IsRiding, Is.False);
            Assert.That(_gunner.Manned, Is.Null);
            Assert.That(_gunner.Behavior.RemountRemainingSeconds, Is.EqualTo(5f));
            Assert.That(_gun.IsFree, Is.True);
        }

        [Test]
        public void ItLeavesBecauseTheTargetIsLostWhenThereIsNoTargetAnymore()
        {
            Engage();
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape("[MountedGun] 'enemy-1' seek gun=Post distance=")));
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Post' board rider=enemy-1 seat=0 role=Gunner");
            LogAssert.Expect(LogType.Log, "[MountedGun] 'enemy-1' man gun=Post");
            _gunner.Tick(Step);
            Assert.That(_agent.IsRiding, Is.True);

            _agent.Target = null;

            _gunner.Tick(0.6f);
            Assert.That(_agent.IsRiding, Is.True, "still within the give-up time");

            LogAssert.Expect(LogType.Log, "[MountedGun] 'enemy-1' leave gun=Post cause=TargetLost");
            _gunner.Tick(0.6f);

            Assert.That(_agent.IsRiding, Is.False);
            Assert.That(_gunner.Manned, Is.Null);
        }

        [Test]
        public void ItDoesNotFireWhileTheTargetIsHidden()
        {
            Engage();
            _gunner.Tick(Step);
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 1.1f, 7f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
            Physics.SyncTransforms();
            var ammo = _gun.Mount.Weapon.AmmoInMagazine;

            _gunner.Tick(Step);
            _gunner.Tick(Step);

            Assert.That(_gun.Mount.Weapon.AmmoInMagazine, Is.EqualTo(ammo));
            Assert.That(_target.Health.Current, Is.EqualTo(1000));
            Assert.That(_gunner.Behavior.UnusableSeconds, Is.EqualTo(0.2f).Within(1e-4f));
        }

        private void Engage()
        {
            _gunner.Tick(Step);
            _agent.Brain.Tick(Step, _agent.Perceive());
            Assert.That(_agent.Brain.State, Is.EqualTo(EnemyState.Chase).Or.EqualTo(EnemyState.Attack));
            Assert.That(_gunner.Behavior.IsSeeking, Is.True);
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

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
