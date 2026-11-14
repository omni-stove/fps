using System;
using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using Fps.Runtime.Traversal;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using ZiplineLine = Fps.Runtime.Traversal.Zipline;
using ZiplineRule = Fps.Core.Zipline;

namespace Fps.Tests.EditMode
{
    public sealed class ZiplineMovementTests
    {
        private const float StepSeconds = 0.02f;

        // 体の中心 (0, 1, 0) の手は (0, 1 + HangOffset, 0)。そのすぐ上を通り、奥 (+Z) へ下る線。
        private static readonly Vector3 _lineStart = new Vector3(0f, 2.5f, -1f);
        private static readonly Vector3 _lineEnd = new Vector3(0f, 1.5f, 12f);

        private GameObject _body;
        private CharacterController _characterController;
        private PlayerController _player;
        private ZiplineMovement _movement;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private GameObject _lineObject;
        private ZiplineLine _line;

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

            _movement = _body.AddComponent<ZiplineMovement>();
            _movement.Initialize(_player, null);

            _lineObject = new GameObject("Line");
            _line = _lineObject.AddComponent<ZiplineLine>();
            _line.SetEnds(_lineStart, _lineEnd);
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_lineObject);
        }

        [Test]
        public void NothingIsGrabbedWhileTheRuleIsOff()
        {
            _movement.Configure(GameRules.Default);

            Assert.That(_movement.enabled, Is.False);
            Assert.That(_movement.TryGrab(_line), Is.False);
            Assert.That(_movement.TryGrab(), Is.False);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.IsMovementOverridden, Is.False);
        }

        [Test]
        public void GrabbingTakesOverTheMovementAndRaisesZiplining()
        {
            TurnOn();

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Zipline\] grab line=Line at="));
            Assert.That(_movement.TryGrab(), Is.True, "the nearest line in reach");
            _player.Tick(Vector2.up, true, false, false, StepSeconds);

            Assert.That(_player.ActiveMovementOverride, Is.SameAs(_movement));
            Assert.That(_player.MovementOverrideConditions, Is.EqualTo(PlayerActionConditions.Ziplining));
            Assert.That(_movement.Line, Is.SameAs(_line));
            var hand = _body.transform.position + (Vector3.up * _movement.HangOffset);
            Assert.That(_line.DistanceTo(hand), Is.LessThan(0.05f), "hangs under the line");
            Assert.That(_player.Motor.Velocity.Z, Is.GreaterThan(0f), "slides towards the lower end");
        }

        [Test]
        public void ALineOutOfReachIsNotGrabbed()
        {
            TurnOn();
            _line.SetEnds(_lineStart + (Vector3.up * 5f), _lineEnd + (Vector3.up * 5f));

            Assert.That(_movement.TryGrab(_line), Is.False);
            Assert.That(_movement.TryGrab(), Is.False);
        }

        [Test]
        public void RidingToTheEndLetsGoThere()
        {
            TurnOn();
            _movement.TryGrab(_line);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Zipline\] release reason=end speed="));
            for (var i = 0; i < 1000 && _movement.IsRiding; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_movement.IsRiding, Is.False);
            Assert.That(_movement.Motion.HasArrived, Is.True);
            var hand = _body.transform.position + (Vector3.up * _movement.HangOffset);
            Assert.That(Vector3.Distance(hand, _lineEnd), Is.LessThan(ZiplineSpec.DefaultEndMargin + 0.05f));

            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.IsMovementOverridden, Is.False, "the next tick hands the body back");
        }

        [Test]
        public void JumpingLetsGoAndTheBodyFalls()
        {
            TurnOn();
            _movement.TryGrab(_line);
            for (var i = 0; i < 10; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Zipline\] release reason=jump speed="));
            _player.Tick(Vector2.zero, false, false, true, StepSeconds);
            Assert.That(_movement.IsRiding, Is.False);
            var releasedAt = _body.transform.position;

            for (var i = 0; i < 20; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(_body.transform.position.y, Is.LessThan(releasedAt.y - 0.1f), "falls");
            Assert.That(_body.transform.position.z, Is.GreaterThan(releasedAt.z), "keeps the momentum along the line");
        }

        [Test]
        public void TheJumpThatGrabbedTheLineDoesNotLetGo()
        {
            TurnOn();
            _movement.TryGrab(_line);

            _player.Tick(Vector2.zero, false, false, true, StepSeconds);

            Assert.That(_movement.IsRiding, Is.True);
        }

        [Test]
        public void GrabbingAgainWaitsForTheCooldown()
        {
            TurnOn();
            _movement.TryGrab(_line);
            _movement.Release(ZiplineMovement.ReasonJump);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            PlaceAt(new Vector3(0f, 1f, 0f));

            Assert.That(_movement.TryGrab(_line), Is.False);
            _movement.TickCooldown(_movement.RegrabSeconds);
            Assert.That(_movement.TryGrab(_line), Is.True);
        }

        [Test]
        public void TurningTheRuleOffLetsGo()
        {
            TurnOn();
            _movement.TryGrab(_line);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Zipline\] release reason=disabled "));
            _movement.Configure(GameRules.Default);

            Assert.That(_movement.IsRiding, Is.False);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.IsMovementOverridden, Is.False);
        }

        [Test]
        public void AnotherOverrideTakingOverEndsTheRide()
        {
            TurnOn();
            _movement.TryGrab(_line);
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Zipline\] release reason=interrupted "));
            _player.UnregisterMovementOverride(_movement);

            Assert.That(_movement.IsRiding, Is.False);
        }

        [Test]
        public void NothingIsGrabbedWhileControlIsHandedOff()
        {
            TurnOn();
            var seat = new object();
            _player.Handoff.Claim(seat);
            try
            {
                Assert.That(_movement.TryGrab(_line), Is.False);
            }
            finally
            {
                _player.Handoff.Release(seat);
            }
        }

        [Test]
        public void TheSpreadWidensOnlyWhileRiding()
        {
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(_body.transform, false);
            var weapon = cameraObject.AddComponent<WeaponController>();
            weapon.MoveFractionSource = () => 0.1f;
            _movement.Initialize(_player, null);
            _movement.Initialize(_player, null);
            TurnOn();

            Assert.That(weapon.MoveFractionSource(), Is.EqualTo(0.1f));
            _movement.TryGrab(_line);
            Assert.That(weapon.MoveFractionSource(), Is.EqualTo(_movement.SpreadMoveFraction), "wrapped once even after initializing again");

            _movement.Release(ZiplineMovement.ReasonJump);
            Assert.That(weapon.MoveFractionSource(), Is.EqualTo(0.1f));
        }

        [Test]
        public void FormatsTaggedLogs()
        {
            TurnOn();
            _movement.TryGrab(_line);

            Assert.That(
                ZiplineMovement.FormatGrabLog(_line, _movement.Motion),
                Does.Match(@"^\[Zipline\] grab line=Line at=\d+\.\d\d remaining=\d+\.\d\d speed=4\.00$"));
            Assert.That(ZiplineMovement.FormatReleaseLog("end", null), Is.EqualTo("[Zipline] release reason=end speed=0.00"));
            Assert.Throws<ArgumentException>(() => ZiplineMovement.FormatReleaseLog("", null));
            Assert.Throws<ArgumentNullException>(() => ZiplineMovement.FormatGrabLog(null, _movement.Motion));
        }

        [Test]
        public void TheInstallerFollowsTheRuleAndSkipsBodiesThatDoNotMove()
        {
            var installer = new ZiplineActionInstaller();
            installer.Install(_body, OnRules());
            Assert.That(_body.GetComponents<ZiplineMovement>(), Has.Length.EqualTo(1), "reuses the existing component");
            Assert.That(_movement.enabled, Is.True);
            Assert.That(_movement.Rule, Is.EqualTo(ZiplineRule.On));

            installer.Install(_body, GameRules.Default);
            Assert.That(_movement.enabled, Is.False);

            var target = new GameObject("Target");
            try
            {
                installer.Install(target, OnRules());
                Assert.That(target.GetComponent<ZiplineMovement>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }

            Assert.That(PlayerActions.Installers.OfType<ZiplineActionInstaller>().Count(), Is.EqualTo(1), "registered on load");
        }

        [Test]
        public void SetEndsRejectsALineTooShortToRide()
        {
            Assert.Throws<ArgumentException>(() => _line.SetEnds(Vector3.zero, new Vector3(0f, 0f, 0.5f)));
            Assert.That(_line.StartPoint, Is.EqualTo(_lineStart), "unchanged");
        }


        private void TurnOn()
        {
            _movement.Configure(OnRules());
        }

        private static GameRules OnRules()
        {
            return GameRules.Default.WithActions(ActionRules.Default.WithZipline(ZiplineRule.On));
        }

        private void PlaceAt(Vector3 position)
        {
            _characterController.enabled = false;
            _body.transform.position = position;
            _characterController.enabled = true;
        }
    }
}
