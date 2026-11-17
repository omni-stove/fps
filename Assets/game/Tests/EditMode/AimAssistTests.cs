using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AimAssistTests
    {
        private const float _step = 0.1f;

        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);

        private readonly List<Object> _created = new List<Object>();
        private InputAction _lookAction;
        private GameObject _player;
        private FirstPersonLook _look;
        private AimAssist _assist;
        private bool _aiming;

        [SetUp]
        public void SetUp()
        {
            _aiming = false;
            _player = new GameObject("player");
            _created.Add(_player);
            _player.transform.position = _eye;
            var pivot = new GameObject("pivot").transform;
            pivot.SetParent(_player.transform, false);
            _lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            _look = _player.AddComponent<FirstPersonLook>();
            _look.Initialize(pivot, _lookAction);
            _assist = _player.AddComponent<AimAssist>();
            _assist.Look = _look;
            _assist.Spec = new AimAssistSpec(
                slowdownDegrees: 5f,
                slowdownScale: 0.5f,
                magnetismDegrees: 3f,
                magnetismDegreesPerSecond: 30f,
                magnetismSeconds: 0.25f,
                maxDistance: 50f);
            _assist.AimingSource = () => _aiming;
            _assist.AimSource = () => new Ray(_eye, Vector3.forward);
            _assist.Targets = new HostileAimTargets(_player.transform, Physics.DefaultRaycastLayers);
            _player.AddComponent<SideMember>().Side = new Side(0);
        }

        [TearDown]
        public void TearDown()
        {
            _lookAction.Dispose();
            KitTuning.Reload();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void AGamepadNearAnEnemySlowsTheLook()
        {
            CreateBody("enemy", 2f, 1);

            _assist.Tick(_step, InputDeviceKind.Gamepad);

            Assert.That(_assist.LastResult.LookScale, Is.EqualTo(0.5f));
            Assert.That(_look.AssistLookScale, Is.EqualTo(0.5f));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(Vector2.zero), "no pull without aiming down sights");
        }

        [Test]
        public void AnEnemyOutsideTheSlowdownAngleLeavesTheLookAlone()
        {
            CreateBody("enemy", 20f, 1);

            _assist.Tick(_step, InputDeviceKind.Gamepad);

            Assert.That(_look.AssistLookScale, Is.EqualTo(1f));
        }

        [Test]
        public void TheMouseGetsNoAssist()
        {
            CreateBody("enemy", 2f, 1);
            _aiming = true;
            _look.AssistLookScale = 0.25f;

            _assist.Tick(_step, InputDeviceKind.KeyboardMouse);

            Assert.That(_assist.LastResult.LookScale, Is.EqualTo(1f));
            Assert.That(_look.AssistLookScale, Is.EqualTo(1f), "what the assist gave before is taken back");
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TurningTheSettingOffStopsTheAssist()
        {
            CreateBody("enemy", 2f, 1);
            _aiming = true;
            _look.ControlSettings = ControlSettings.Default.WithAimAssist(false);

            _assist.Tick(_step, InputDeviceKind.Gamepad);

            Assert.That(_look.AssistLookScale, Is.EqualTo(1f));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void AnAllyIsNeitherSlowedForNorPulledTo()
        {
            CreateBody("ally", 2f, 0);
            _aiming = true;

            _assist.Tick(_step, InputDeviceKind.Gamepad);

            Assert.That(_look.AssistLookScale, Is.EqualTo(1f));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void AnEnemyInSmokeIsNeitherSlowedForNorPulledTo()
        {
            CreateBody("enemy", 2f, 1);
            _aiming = true;
            var smoke = new Smoke();
            Assert.That(EnemySenses.RegisterVisionBlocker(smoke), Is.True);
            try
            {
                _assist.Tick(_step, InputDeviceKind.Gamepad);
            }
            finally
            {
                EnemySenses.UnregisterVisionBlocker(smoke);
            }

            Assert.That(_look.AssistLookScale, Is.EqualTo(1f));
            Assert.That(_look.PendingAssistDegrees, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void AimingDownSightsPullsTowardTheEnemyAndTurnsTheLook()
        {
            CreateBody("enemy", 2f, 1);
            _aiming = true;

            _assist.Tick(_step, InputDeviceKind.Gamepad);

            // 2 度ずれていて、1 刻みで回せるのは 30 × 0.1 = 3 度なので、2 度回し切る。
            Assert.That(_assist.LastResult.YawDelta, Is.EqualTo(2f).Within(1e-3f));
            Assert.That(_assist.LastResult.PitchUpDelta, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(_look.PendingAssistDegrees.x, Is.EqualTo(2f).Within(1e-3f));

            _look.Turn(Vector2.zero, false);
            Assert.That(_look.Yaw, Is.EqualTo(2f).Within(1e-3f));
        }

        [Test]
        public void ThePullLastsOnlyTheMagnetismSecondsAfterAimingStarts()
        {
            CreateBody("enemy", 2f, 1);
            _aiming = true;

            // 覗き込んでから 0・0.1・0.2 秒は 0.25 秒未満なので吸い付き、0.3 秒では吸い付かない。
            for (var i = 0; i < 3; i++)
            {
                _assist.Tick(_step, InputDeviceKind.Gamepad);
                Assert.That(_assist.LastResult.YawDelta, Is.GreaterThan(0f), $"tick {i}");
            }

            _assist.Tick(_step, InputDeviceKind.Gamepad);
            Assert.That(_assist.AimingSeconds, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(_assist.LastResult.YawDelta, Is.EqualTo(0f));
            Assert.That(_assist.LastResult.LookScale, Is.EqualTo(0.5f), "the slowdown stays while aiming");

            _aiming = false;
            _assist.Tick(_step, InputDeviceKind.Gamepad);
            _aiming = true;
            _assist.Tick(_step, InputDeviceKind.Gamepad);
            Assert.That(_assist.AimingSeconds, Is.EqualTo(0f));
            Assert.That(_assist.LastResult.YawDelta, Is.GreaterThan(0f), "aiming again pulls again");
        }

        [Test]
        public void TheAimingTimeKeepsCountingWhileTheMouseIsUsed()
        {
            CreateBody("enemy", 2f, 1);
            _aiming = true;

            _assist.Tick(_step, InputDeviceKind.KeyboardMouse);
            _assist.Tick(_step, InputDeviceKind.KeyboardMouse);
            _assist.Tick(_step, InputDeviceKind.KeyboardMouse);
            _assist.Tick(_step, InputDeviceKind.Gamepad);

            Assert.That(_assist.AimingSeconds, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(_assist.LastResult.YawDelta, Is.EqualTo(0f), "switching to the gamepad does not restart the pull");
        }

        [Test]
        public void WithoutAnAimThereIsNoAssist()
        {
            CreateBody("enemy", 2f, 1);
            _assist.AimSource = () => null;
            _look.AssistLookScale = 0.25f;

            _assist.Tick(_step, InputDeviceKind.Gamepad);

            Assert.That(_look.AssistLookScale, Is.EqualTo(1f));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TickRejectsABadStep(float deltaSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _assist.Tick(deltaSeconds, InputDeviceKind.Gamepad));
        }

        [Test]
        public void TheSpecDefaultsToTheTuningFile()
        {
            var holder = new GameObject("defaults");
            _created.Add(holder);
            var assist = holder.AddComponent<AimAssist>();

            Assert.That(assist.Spec, Is.SameAs(AimAssist.Tuning));
            Assert.That(assist.Spec.SlowdownDegrees, Is.EqualTo(AimAssistSpec.DefaultSlowdownDegrees));
            Assert.That(assist.Spec.MagnetismSeconds, Is.EqualTo(AimAssistSpec.DefaultMagnetismSeconds));
            Assert.That(assist.Spec.MaxDistance, Is.EqualTo(AimAssistSpec.DefaultMaxDistance));
        }

        [Test]
        public void AttachWiresTheLookTheTargetsAndTheAim()
        {
            var player = new GameObject("attached");
            _created.Add(player);
            var look = player.AddComponent<FirstPersonLook>();
            var weapon = CreateWeapon(player);

            var assist = AimAssist.Attach(player, look, null, weapon);

            Assert.That(assist.gameObject, Is.SameAs(player));
            Assert.That(assist.Look, Is.SameAs(look));
            Assert.That(assist.Targets, Is.Not.Null);
            Assert.That(assist.AimingSource(), Is.False, "without aiming down sights there is no aiming");
            Assert.That(assist.AimSource(), Is.Null, "without a view rig there is no aim yet");
            Assert.That(AimAssist.Attach(player, look, null, weapon), Is.SameAs(assist), "attaching twice keeps one assist");
        }

        [Test]
        public void AttachRejectsMissingParts()
        {
            var player = new GameObject("attached");
            _created.Add(player);
            var look = player.AddComponent<FirstPersonLook>();
            var weapon = CreateWeapon(player);

            Assert.Throws<ArgumentNullException>(() => AimAssist.Attach(null, look, null, weapon));
            Assert.Throws<ArgumentNullException>(() => AimAssist.Attach(player, null, null, weapon));
            Assert.Throws<ArgumentNullException>(() => AimAssist.Attach(player, look, null, null));
            Assert.That(player.GetComponent<AimAssist>(), Is.Null);
        }

        private static WeaponController CreateWeapon(GameObject player)
        {
            var camera = new GameObject("camera");
            camera.transform.SetParent(player.transform, false);
            return camera.AddComponent<WeaponController>();
        }

        private void CreateBody(string id, float degrees, int side)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(body);
            body.name = id;
            var radians = degrees * Mathf.Deg2Rad;
            body.transform.position = _eye + (new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * 10f);
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            body.AddComponent<SideMember>().Side = new Side(side);
            Physics.SyncTransforms();
        }

        private sealed class Smoke : IVisionBlocker
        {
            public bool Blocks(Vector3 eye, Vector3 target)
            {
                return true;
            }
        }
    }
}
