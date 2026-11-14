using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class EnemyWeaponParityTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Enemy";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void GaitRatiosMatchThePlayerDefaults()
        {
            Assert.That(EnemyGait.SprintRatio, Is.EqualTo(PlayerController.DefaultSprintSpeed / PlayerController.DefaultWalkSpeed).Within(1e-5f));
            Assert.That(EnemyGait.CrouchRatio, Is.EqualTo(PlayerController.DefaultCrouchSpeed / PlayerController.DefaultWalkSpeed).Within(1e-5f));
        }

        [Test]
        public void SprintsWhileClosingInOrRunningToCoverButNotWhileFiringOrCrouched()
        {
            Assert.That(EnemyGait.IsSprinting(EnemyState.Chase, EnemyCoverPhase.None, crouching: false, firing: false), Is.True);
            Assert.That(EnemyGait.IsSprinting(EnemyState.TakeCover, EnemyCoverPhase.Moving, crouching: false, firing: false), Is.True);
            Assert.That(EnemyGait.IsSprinting(EnemyState.Chase, EnemyCoverPhase.None, crouching: false, firing: true), Is.False);
            Assert.That(EnemyGait.IsSprinting(EnemyState.TakeCover, EnemyCoverPhase.Moving, crouching: true, firing: false), Is.False);
            Assert.That(EnemyGait.IsSprinting(EnemyState.Patrol, EnemyCoverPhase.None, crouching: false, firing: false), Is.False);
            Assert.That(EnemyGait.IsSprinting(EnemyState.Attack, EnemyCoverPhase.None, crouching: false, firing: false), Is.False);
        }

        [Test]
        public void AimsWhileAttackingOrPeeking()
        {
            Assert.That(EnemyGait.IsAiming(EnemyState.Attack, EnemyCoverPhase.None), Is.True);
            Assert.That(EnemyGait.IsAiming(EnemyState.TakeCover, EnemyCoverPhase.Peeking), Is.True);
            Assert.That(EnemyGait.IsAiming(EnemyState.TakeCover, EnemyCoverPhase.Hiding), Is.False);
            Assert.That(EnemyGait.IsAiming(EnemyState.Chase, EnemyCoverPhase.None), Is.False);
        }

        [Test]
        public void SpeedScalesTheWalkSpeed()
        {
            Assert.That(EnemyGait.Speed(3.5f, sprinting: false, crouching: false), Is.EqualTo(3.5f));
            Assert.That(EnemyGait.Speed(3.5f, sprinting: true, crouching: false), Is.EqualTo(3.5f * EnemyGait.SprintRatio).Within(1e-5f));
            Assert.That(EnemyGait.Speed(3.5f, sprinting: true, crouching: true), Is.EqualTo(3.5f * EnemyGait.CrouchRatio).Within(1e-5f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => EnemyGait.Speed(-1f, false, false));
        }

        [Test]
        public void WalkSpeedIsTheNavMeshAgentSpeedAtInitialize()
        {
            _host.GetComponent<NavMeshAgent>().speed = 3.5f;
            Assert.That(_agent.TryInitialize(), Is.True);
            _host.GetComponent<NavMeshAgent>().speed = 9f;

            Assert.That(_agent.WalkSpeed, Is.EqualTo(3.5f));
        }

        [Test]
        public void RecoilLiftsAndTurnsTheShotLikeThePlayersView()
        {
            var recoil = new RecoilState();
            Assert.That(EnemyAgent.WithRecoil(Vector3.forward, recoil, 0f), Is.EqualTo(Vector3.forward));

            var spec = TestWeapons.RifleSpec();
            for (var i = 0; i < 10; i++)
            {
                recoil.Kick(spec, 7, i);
            }

            var full = EnemyAgent.WithRecoil(Vector3.forward, recoil, 0f);
            var pitch = Mathf.Atan2(full.y, new Vector2(full.x, full.z).magnitude) * Mathf.Rad2Deg;
            Assert.That(pitch, Is.EqualTo(recoil.PitchUpDegrees).Within(1e-3f), "the shot climbs by the pitch offset");
            Assert.That(Mathf.Sign(full.x), Is.EqualTo(Mathf.Sign(recoil.YawDegrees)), "positive yaw turns right");

            var controlled = EnemyAgent.WithRecoil(Vector3.forward, recoil, EnemyAgent.RecoilControl);
            Assert.That(controlled.y, Is.LessThan(full.y).And.GreaterThan(0f));
            Assert.That(EnemyAgent.WithRecoil(Vector3.forward, recoil, 1f), Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void GunIsHeldAndFeedsTheMuzzleFlash()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            var presentation = _host.AddComponent<WeaponPresentation>();
            var view = _host.AddComponent<EnemyWeaponView>();

            view.Refresh(0.016f);

            Assert.That(view.IsBuilt, Is.True);
            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Root.parent, Is.EqualTo(_host.transform));
            Assert.That(view.Root.GetComponentInChildren<Collider>(), Is.Null, "the gun must not block shots or the agent");
            Assert.That(presentation.Muzzle, Is.EqualTo(view.Muzzle));

            var feetY = _agent.GroundPosition.y;
            Assert.That(view.Root.position.y - feetY, Is.EqualTo(PlayerMockPose.BodyHipGunPosition.y).Within(1e-3f), "an idle enemy holds the gun at the hip");
        }

        [Test]
        public void GunIsHiddenWhileUnarmedOrDead()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            var view = _host.AddComponent<EnemyWeaponView>();
            view.Refresh(0.016f);

            _agent.Disarm();
            view.Refresh(0.016f);
            Assert.That(view.IsShown, Is.False);

            _agent.EquipWeapon(_rifle);
            view.Refresh(0.016f);
            Assert.That(view.IsShown, Is.True);

            _host.GetComponent<Damageable>().Kill();
            view.Refresh(0.016f);
            Assert.That(view.IsShown, Is.False);
        }

        [Test]
        public void GunLowersWhileReloading()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            var view = _host.AddComponent<EnemyWeaponView>();
            while (_agent.Weapon.AmmoInMagazine > 0)
            {
                _agent.Weapon.TryFire();
                _agent.Weapon.Tick(1f);
            }

            Assert.That(_agent.Weapon.TryStartReload(), Is.True);
            view.Refresh(0.016f);

            var feetY = _agent.GroundPosition.y;
            Assert.That(view.Root.position.y - feetY, Is.EqualTo(PlayerMockPose.BodyReloadGunPosition.y).Within(1e-3f));
        }
    }
}
