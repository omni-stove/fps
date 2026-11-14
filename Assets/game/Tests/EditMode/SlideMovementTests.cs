using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// スライディング (<see cref="SlideMovement"/>) が、ダッシュ中のしゃがみで始まり、速さが落ちるかジャンプで終わること、
    /// ルールが Off・カバー中・乗り越え中は始まらないこと、坂では平らな床より長く滑ること。
    /// </summary>
    public sealed class SlideMovementTests
    {
        private const float StepSeconds = 0.02f;
        private const int MaxSteps = 500;

        private GameObject _body;
        private CharacterController _characterController;
        private PlayerController _player;
        private SlideMovement _slide;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private GameObject _floor;
        private GameObject _obstacle;
        private bool _crouchWasHeld;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0f, 1f, 0f);
            _characterController = _body.AddComponent<CharacterController>();
            _characterController.height = 2f;
            _characterController.radius = 0.5f;
            _characterController.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(_characterController, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            _slide = _body.AddComponent<SlideMovement>();
            _slide.Configure(Rules(Slide.On));
            _crouchWasHeld = false;

            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(80f, 1f, 80f);
            Physics.SyncTransforms();
            Settle();
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_floor);
            if (_obstacle != null)
            {
                Object.DestroyImmediate(_obstacle);
            }
        }

        [Test]
        public void CrouchingWhileSprintingSlidesUntilItSlowsDownAndStaysCrouchedWhileHeld()
        {
            Sprint(10);
            var start = _body.transform.position;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Slide\] start speed=11\.00 direction=\(0\.00, 1\.00\)$"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Slide\] end reason=Stopped "));

            Press(sprint: true, crouch: true);

            Assert.That(_slide.IsSliding, Is.True, "the first crouch while sprinting starts the slide");
            Assert.That(_player.ActiveMovementOverride, Is.SameAs(_slide));
            Assert.That(_player.MovementOverrideConditions, Is.EqualTo(PlayerActionConditions.Sliding));
            Assert.That(_player.IsCrouched, Is.True, "slides in the low shape");

            var steps = PressWhileSliding(sprint: true, crouch: true);

            Assert.That(steps, Is.LessThan(MaxSteps));
            Assert.That(_slide.Motion.LastEnd, Is.EqualTo(SlideEnd.Stopped));
            var moved = _body.transform.position - start;
            Assert.That(moved.z, Is.EqualTo(7f).Within(0.5f), "about (11 + 3) / 2 m/s for a second");
            Assert.That(Mathf.Abs(moved.x), Is.LessThan(0.05f));

            Press(sprint: true, crouch: true);
            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(_slide.IsSliding, Is.False, "staying crouched does not start another slide");
            Assert.That(_player.IsCrouched, Is.True, "still holding crouch, so still crouched");
        }

        [Test]
        public void ReleasingCrouchDoesNotStopTheSlideButStandsUpAfterIt()
        {
            Sprint(10);
            Press(sprint: true, crouch: true);
            Assert.That(_slide.IsSliding, Is.True);

            Press(sprint: false, crouch: false);
            Assert.That(_slide.IsSliding, Is.True, "the slide keeps going without the button");

            PressWhileSliding(sprint: false, crouch: false);
            Press(sprint: false, crouch: false);

            Assert.That(_slide.Motion.LastEnd, Is.EqualTo(SlideEnd.Stopped));
            Assert.That(_player.IsCrouched, Is.False, "stands back up");
        }

        [Test]
        public void JumpingEndsTheSlideWithItsMomentum()
        {
            Sprint(10);
            Press(sprint: true, crouch: true);
            Press(sprint: true, crouch: true);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Slide\] end reason=Jumped "));

            Press(sprint: true, crouch: true, jump: true);

            Assert.That(_slide.IsSliding, Is.False);
            Assert.That(_slide.Motion.LastEnd, Is.EqualTo(SlideEnd.Jumped));
            var velocity = _player.Motor.Velocity;
            Assert.That(velocity.Y, Is.GreaterThan(0f), "jumped");
            Assert.That(velocity.Z, Is.GreaterThan(_player.SprintSpeed), "kept the slide's speed");
            Assert.That(_player.IsCrouched, Is.False);
        }

        [Test]
        public void WithTheRuleOffCrouchingWhileSprintingOnlyCrouches()
        {
            _slide.Configure(Rules(Slide.Off));
            Assert.That(_slide.enabled, Is.False);
            Assert.That(_player.MovementOverrides, Does.Not.Contain(_slide));

            Sprint(10);
            Press(sprint: true, crouch: true);
            Press(sprint: true, crouch: true);

            Assert.That(_slide.IsSliding, Is.False);
            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(_player.IsCrouched, Is.True);
            Assert.That(new Vector2(_player.Motor.Velocity.X, _player.Motor.Velocity.Z).magnitude, Is.EqualTo(_player.CrouchSpeed).Within(1e-3f));

            _slide.Configure(Rules(Slide.On));
            Assert.That(_player.MovementOverrides, Does.Contain(_slide), "turning it back on registers it again");
        }

        [Test]
        public void TurningTheRuleOffMidSlideStopsIt()
        {
            Sprint(10);
            Press(sprint: true, crouch: true);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Slide\] end reason=Cancelled "));

            _slide.Configure(Rules(Slide.Off));

            Assert.That(_slide.IsSliding, Is.False);
            Assert.That(_slide.Motion.LastEnd, Is.EqualTo(SlideEnd.Cancelled));
            Assert.That(_player.IsMovementOverridden, Is.False);
        }

        [Test]
        public void NoSlideStartsInCoverOrWhileVaultingOrWalking()
        {
            var inCover = true;
            _slide.Blocker = () => inCover;
            Run(8f);
            Assert.That(_slide.TryStart(), Is.False, "in cover");
            inCover = false;
            Assert.That(_slide.TryStart(), Is.True);
            _slide.Cancel();

            _player.Motor.SetVelocity(new System.Numerics.Vector3(0f, 0f, 4f));
            Assert.That(_slide.TryStart(), Is.False, "walking is not fast enough");

            _player.VaultRule = Vault.On;
            _obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstacle.transform.position = new Vector3(0f, 0.5f, 1f);
            _obstacle.transform.localScale = new Vector3(3f, 1f, 0.4f);
            Physics.SyncTransforms();
            Assert.That(_player.TryStartVault(), Is.True);
            Run(8f);
            Assert.That(_slide.TryStart(), Is.False, "vaulting");
        }

        [Test]
        public void CoverStopsASlideInProgress()
        {
            var inCover = false;
            _slide.Blocker = () => inCover;
            Sprint(10);
            Press(sprint: true, crouch: true);
            Assert.That(_slide.IsSliding, Is.True);

            inCover = true;
            Press(sprint: false, crouch: false);

            Assert.That(_slide.IsSliding, Is.False);
            Assert.That(_slide.Motion.LastEnd, Is.EqualTo(SlideEnd.Cancelled));
        }

        [Test]
        public void AWallStopsTheSlide()
        {
            _obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstacle.transform.position = new Vector3(0f, 1.5f, 2.5f);
            _obstacle.transform.localScale = new Vector3(6f, 3f, 1f);
            Physics.SyncTransforms();

            Run(8f);
            Assert.That(_slide.TryStart(), Is.True);
            var steps = TickWhileSliding();

            Assert.That(_slide.Motion.LastEnd, Is.EqualTo(SlideEnd.Stopped));
            Assert.That(_slide.Motion.Distance, Is.LessThan(4f), "stopped by the wall instead of sliding 7m");
            Assert.That(steps, Is.LessThan(50), "ends soon after hitting the wall");
        }

        [Test]
        public void ASlopeSlidesFurtherThanAFlatLane()
        {
            _obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstacle.transform.SetPositionAndRotation(TestSlope.RampCenter, TestSlope.RampRotation);
            _obstacle.transform.localScale = new Vector3(TestSlope.RampWidth, TestSlope.RampThickness, TestSlope.RampLength);
            Physics.SyncTransforms();

            var flat = SlideFrom(TestSlope.FlatStart);
            var slope = SlideFrom(TestSlope.SlopeStart);

            Assert.That(flat, Is.EqualTo(7f).Within(0.5f));
            Assert.That(slope, Is.GreaterThan(flat * TestSlope.MinSlopeRatio));
        }

        /// <summary>12° の坂と、その足元から続く平らな走路。ゲームの射撃場の坂を変えてもこのテストの前提が動かないよう、テストの側に持つ。</summary>
        private static class TestSlope
        {
            public const float SlopeDegrees = 12f;
            public const float RampLength = 14f;
            public const float RampWidth = 4f;
            public const float RampThickness = 0.5f;
            public const float SlopeStartFromTop = 1.5f;
            public const float MinSlopeRatio = 1.5f;

            public static readonly Vector3 RampFoot = new Vector3(-18f, 0f, -8f);
            public static readonly Vector3 FlatStart = RampFoot + new Vector3(0f, 0f, -4f);

            public static Quaternion RampRotation => Quaternion.Euler(-SlopeDegrees, 0f, 0f);

            public static Vector3 RampCenter => RampFoot - (RampRotation * new Vector3(0f, RampThickness / 2f, -RampLength / 2f));

            public static Vector3 SlopeStart =>
                RampCenter + (RampRotation * new Vector3(0f, RampThickness / 2f, (RampLength / 2f) - SlopeStartFromTop));
        }

        [Test]
        public void TheInstallerAddsTheSlideOnlyToMovingPlayersAndFollowsTheRule()
        {
            Assert.That(PlayerActions.Installers.Count(i => i is SlideActionInstaller), Is.EqualTo(1), "registered on load");

            var installer = new SlideActionInstaller();
            installer.Install(_body, Rules(Slide.On));
            installer.Install(_body, Rules(Slide.On));
            Assert.That(_body.GetComponents<SlideMovement>(), Has.Length.EqualTo(1));
            Assert.That(_slide.enabled, Is.True);
            Assert.That(_player.MovementOverrides.Count(o => ReferenceEquals(o, _slide)), Is.EqualTo(1));

            installer.Install(_body, Rules(Slide.Off));
            Assert.That(_slide.enabled, Is.False);
            Assert.That(_player.MovementOverrides, Does.Not.Contain(_slide));

            var target = new GameObject("Target");
            try
            {
                installer.Install(target, Rules(Slide.On));
                Assert.That(target.GetComponent<SlideMovement>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SetTuningRejectsBadValuesAndKeepsTheOldOnes()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _slide.SetTuning(2f, 8f, 3f, 0.4f, 3f, 20f));
            Assert.That(_slide.Motion.StartSpeed, Is.EqualTo(SlideMotion.DefaultStartSpeed));

            _slide.SetTuning(9f, 4f, 2f, 0.2f, 2f, 10f);
            Assert.That(_slide.Motion.StartSpeed, Is.EqualTo(9f));
            Assert.That(_slide.Motion.MaxSeconds, Is.EqualTo(2f));
        }

        private float SlideFrom(Vector3 feet)
        {
            _characterController.enabled = false;
            _body.transform.position = feet + new Vector3(0f, 1.1f, 0f);
            _characterController.enabled = true;
            Physics.SyncTransforms();
            Settle();

            Run(_player.SprintSpeed, Vector3.back);
            Assert.That(_slide.TryStart(), Is.True, $"can slide from {feet}");
            TickWhileSliding();
            Assert.That(_slide.IsSliding, Is.False);
            var distance = _slide.Motion.Distance;

            // 次の滑りの前に、立ち上がって止まる。
            for (var i = 0; i < 5; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            return distance;
        }

        private void Settle()
        {
            for (var i = 0; i < 30 && !_player.IsGrounded; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_player.IsGrounded, Is.True, "landed");
        }

        // 走っている速度だけを入れる (ダッシュの入力を介さず TryStart を確かめる)。
        private void Run(float speed, Vector3? direction = null)
        {
            var velocity = (direction ?? Vector3.forward) * speed;
            _player.Motor.SetVelocity(new System.Numerics.Vector3(velocity.x, 0f, velocity.z));
        }

        private void Sprint(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                Press(sprint: true, crouch: false);
            }

            Assert.That(_player.IsSprinting, Is.True);
            Assert.That(_player.Motor.Velocity.Z, Is.EqualTo(_player.SprintSpeed).Within(1e-3f));
        }

        private void Press(bool sprint, bool crouch, bool jump = false)
        {
            var input = new PlayerMoveInput(
                Vector2.up,
                sprint,
                false,
                InputDeviceKind.KeyboardMouse,
                crouch,
                crouch && !_crouchWasHeld,
                InputDeviceKind.KeyboardMouse,
                jump,
                false);
            _crouchWasHeld = crouch;
            _player.ApplyInput(input, StepSeconds);
        }

        private int PressWhileSliding(bool sprint, bool crouch)
        {
            var steps = 0;
            while (_slide.IsSliding && steps < MaxSteps)
            {
                Press(sprint, crouch);
                steps++;
            }

            return steps;
        }

        private int TickWhileSliding()
        {
            var steps = 0;
            do
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
                steps++;
            }
            while (_slide.IsSliding && steps < MaxSteps);

            return steps;
        }

        private static GameRules Rules(Slide slide)
        {
            return GameRules.Default.WithActions(GameRules.Default.Actions.WithSlide(slide));
        }
    }
}
