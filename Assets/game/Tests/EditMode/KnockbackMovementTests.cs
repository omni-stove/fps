using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class KnockbackMovementTests
    {
        private const float StepSeconds = 0.02f;
        private const string PlayerId = "player-1";

        private GameObject _body;
        private PlayerController _player;
        private Damageable _damageable;
        private KnockbackMovement _movement;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private GameObject _floor;
        private GameObject _other;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0f, 1f, 0f);
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));

            _damageable = _body.AddComponent<Damageable>();
            _damageable.Initialize(PlayerId, 100);
            _movement = KnockbackMovement.Attach(_player, _damageable);
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
            foreach (var extra in new[] { _floor, _other })
            {
                if (extra != null)
                {
                    Object.DestroyImmediate(extra);
                }
            }
        }

        [Test]
        public void AttachRegistersOneOverrideOnTheBody()
        {
            Assert.That(KnockbackMovement.Attach(_player, _damageable), Is.SameAs(_movement));
            Assert.That(_movement.gameObject, Is.SameAs(_damageable.gameObject));
            Assert.That(_player.MovementOverrides, Has.Count.EqualTo(1));
            Assert.That(_player.MovementOverrides[0], Is.SameAs(_movement));
            Assert.That(_movement.IsKnockedBack, Is.False);
        }

        [Test]
        public void ALaunchFliesThePlayerAndLandsItBackOnTheFloor()
        {
            LandOnFloor();
            var start = _body.transform.position;

            LogAssert.Expect(LogType.Log, "[Knockback] id=player-1 launched kind=Explosion velocity=(4.0, 3.0, 0.0)");
            LogAssert.Expect(LogType.Log, "[Player] override=KnockbackMovement active=true");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Knockback\] id=player-1 landed after=0\.\d\ds at=\("));
            Assert.That(_movement.Launch(new Vector3(4f, 3f, 0f), DamageKind.Explosion), Is.True);

            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.ActiveMovementOverride, Is.SameAs(_movement));
            Assert.That(_player.IsGrounded, Is.False, "left the floor");

            for (var i = 0; i < 200 && _movement.IsKnockedBack; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_movement.IsKnockedBack, Is.False, "landed");
            Assert.That(_body.transform.position.x - start.x, Is.GreaterThan(0.8f), "flew along the push");

            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.IsMovementOverridden, Is.False, "gives the movement back after landing");
            Assert.That(_player.IsGrounded, Is.True);
        }

        [Test]
        public void TheInputSteersOnlyALittleInTheAir()
        {
            LandOnFloor();
            _movement.Launch(new Vector3(0f, 4f, 0f), DamageKind.Explosion);

            for (var i = 0; i < 10; i++)
            {
                _player.Tick(Vector2.right, false, false, false, StepSeconds);
            }

            Assert.That(_movement.Velocity.x, Is.GreaterThan(0f));
            Assert.That(_movement.Velocity.x, Is.LessThan(_player.Motor.Spec.WalkSpeed * 0.5f), "air control is weak");
        }

        [Test]
        public void AMeleeHitLaunchesAlongTheHit()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Knockback\] id=player-1 launched kind=Melee velocity="));

            _damageable.TakeDamage(5, Vector3.zero, new Vector3(0f, 0f, 2f), DamageKind.Melee);

            Assert.That(_movement.IsKnockedBack, Is.True);
            Assert.That(_movement.Velocity.z, Is.GreaterThan(0f));
            Assert.That(_movement.Velocity.y, Is.GreaterThan(0f));
        }

        [Test]
        public void BulletsAndExplosionReportsDoNotLaunchByThemselves()
        {
            _damageable.TakeDamage(5, Vector3.zero, Vector3.forward, DamageKind.Bullet);
            _damageable.TakeDamage(5, Vector3.zero, Vector3.forward, DamageKind.Explosion);
            _damageable.TakeDamage(5, Vector3.zero, null, DamageKind.Melee);

            Assert.That(_movement.IsKnockedBack, Is.False);
        }

        [Test]
        public void AHeavierBodyFliesSlower()
        {
            _movement.KnockbackWeight = 2f;

            Assert.That(Knockback.Apply(_damageable, DamageKind.Explosion, Vector3.right, 1f), Is.True);

            Assert.That(_movement.Velocity.magnitude, Is.EqualTo(KnockbackSpec.DefaultExplosionSpeed / 2f).Within(1e-4f));
        }

        [Test]
        public void TheTunedForceAndPlayerWeightSetHowFarThePlayerFlies()
        {
            var tuning = new KnockbackSpec(
                explosion: new KnockbackForce(12f, 0f, 1f),
                playerWeight: 3f,
                enemyWeight: 0.5f);

            _movement.ApplyTuning(tuning);

            Assert.That(_movement.KnockbackSpec, Is.SameAs(tuning));
            Assert.That(_movement.KnockbackWeight, Is.EqualTo(3f), "the player reads the player weight, not the enemy one");
            Assert.That(Knockback.Apply(_damageable, DamageKind.Explosion, Vector3.right, 1f), Is.True);
            Assert.That(_movement.Velocity.magnitude, Is.EqualTo(12f / 3f).Within(1e-4f));
        }

        [Test]
        public void TuningDoesNotOverwriteValuesSetByCode()
        {
            var set = new KnockbackSpec(gravity: 30f);
            _movement.KnockbackSpec = set;
            _movement.KnockbackWeight = 2f;

            _movement.ApplyTuning(new KnockbackSpec(gravity: 10f, playerWeight: 5f));

            Assert.That(_movement.KnockbackSpec, Is.SameAs(set));
            Assert.That(_movement.KnockbackWeight, Is.EqualTo(2f));
            Assert.Throws<ArgumentNullException>(() => _movement.ApplyTuning(null));
        }

        [Test]
        public void TheDeadAreNotLaunchedAndDyingStopsTheFlight()
        {
            _movement.Launch(Vector3.up * 3f, DamageKind.Explosion);

            _damageable.Kill();

            Assert.That(_movement.IsKnockedBack, Is.False);
            Assert.That(_movement.Launch(Vector3.up * 3f, DamageKind.Explosion), Is.False);
        }

        [Test]
        public void UnbindingCancelsTheFlight()
        {
            LandOnFloor();
            _movement.Launch(new Vector3(0f, 4f, 0f), DamageKind.Explosion);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Knockback\] id=player-1 cancelled after="));
            _movement.Unbind();

            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(_player.MovementOverrides, Is.Empty);
            Assert.That(_movement.IsKnockedBack, Is.False);
        }

        [Test]
        public void RejectsInvalidValues()
        {
            Assert.Throws<ArgumentException>(() => _movement.Launch(new Vector3(float.NaN, 0f, 0f), DamageKind.Explosion));
            Assert.Throws<ArgumentOutOfRangeException>(() => _movement.KnockbackWeight = 0f);
            Assert.Throws<ArgumentNullException>(() => _movement.KnockbackSpec = null);
            Assert.Throws<ArgumentNullException>(() => KnockbackMovement.Attach(null, _damageable));

            _other = new GameObject("Unbound");
            var unbound = _other.AddComponent<KnockbackMovement>();
            Assert.Throws<InvalidOperationException>(() => unbound.Launch(Vector3.up, DamageKind.Explosion));
        }

        private void LandOnFloor()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(40f, 1f, 40f);
            Physics.SyncTransforms();

            for (var i = 0; i < 10 && !_player.IsGrounded; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_player.IsGrounded, Is.True, "landed on the floor");
        }
    }
}
