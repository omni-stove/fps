using System;
using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// パラシュート (<see cref="ParachuteMovement"/>) が空中の十分な高さでだけ開き、開いている間は移動を横取りしてゆっくり降り、
    /// 着地で閉じて落下ダメージを受けないこと。ルールが Off・低すぎる・ほかの動きの最中には開かないこと。
    /// </summary>
    public sealed class ParachuteMovementTests
    {
        private const float StepSeconds = 0.02f;
        private const int MaxTicks = 2000;
        private const int MaxHealth = 100;

        /// <summary>落下ダメージを受ける高さ。既定の重力 20 m/s² で開かずに落ちると約 20 m/s で、安全な速さ 12 m/s を超える。</summary>
        private const float HighDrop = 10f;

        private GameObject _body;
        private CharacterController _characterController;
        private PlayerController _player;
        private Damageable _damageable;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private GameObject _floor;

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(200f, 1f, 200f);

            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0f, 1f, 0f);
            _characterController = _body.AddComponent<CharacterController>();
            _characterController.height = 2f;
            _characterController.radius = 0.5f;
            _characterController.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _damageable = _body.AddComponent<Damageable>();
            _damageable.Initialize("Player", MaxHealth);
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(_characterController, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            Physics.SyncTransforms();
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
        }

        [Test]
        public void IsOffByDefaultAndRegistersAsAMovementOverride()
        {
            var parachute = AddParachute();

            Assert.That(parachute.IsInitialized, Is.True);
            Assert.That(parachute.Rule, Is.EqualTo(Parachute.Off));
            Assert.That(parachute.IsOpen, Is.False);
            Assert.That(parachute.IsActive, Is.False);
            Assert.That(parachute.Conditions, Is.EqualTo(PlayerActionConditions.Parachuting));
            Assert.That(_player.MovementOverrides, Does.Contain(parachute));
            Assert.That(parachute.Motion.MinOpenHeight, Is.EqualTo(ParachuteMotion.DefaultMinOpenHeight));
        }

        [Test]
        public void OpensInTheAirHighEnoughAndDescendsSlowly()
        {
            var parachute = AddParachute(Parachute.On);
            var opened = 0f;
            parachute.Opened += height => opened = height;
            FallFrom(20f);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Parachute\] opened height=\d+\.\d\d$"));
            Assert.That(parachute.TryOpen(), Is.EqualTo(ParachuteOpenVerdict.Open));
            Assert.That(parachute.IsOpen, Is.True);
            Assert.That(parachute.LastVerdict, Is.EqualTo(ParachuteOpenVerdict.Open));
            Assert.That(opened, Is.GreaterThan(parachute.Motion.MinOpenHeight));

            var before = _body.transform.position.y;
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            Assert.That(_player.ActiveMovementOverride, Is.SameAs(parachute));
            Assert.That(_player.MovementOverrideConditions, Is.EqualTo(PlayerActionConditions.Parachuting));
            Assert.That(_player.Motor.Velocity.Y, Is.EqualTo(-parachute.Motion.DescentSpeed).Within(1e-4f));
            Assert.That(before - _body.transform.position.y, Is.EqualTo(parachute.Motion.DescentSpeed * StepSeconds).Within(1e-3f));
        }

        [Test]
        public void LandingClosesItWithoutFallDamage()
        {
            _player.FallDamage = FallDamage.On;
            var parachute = AddParachute(Parachute.On);
            ParachuteCloseReason? closed = null;
            parachute.Closed += reason => closed = reason;
            FallFrom(HighDrop);
            Assert.That(parachute.TryOpen(), Is.EqualTo(ParachuteOpenVerdict.Open));

            LogAssert.Expect(LogType.Log, "[Parachute] closed reason=Landed");
            TickUntil(() => !parachute.IsOpen);

            Assert.That(closed, Is.EqualTo(ParachuteCloseReason.Landed));
            Assert.That(parachute.LastCloseReason, Is.EqualTo(ParachuteCloseReason.Landed));
            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_player.IsMovementOverridden, Is.False);
            Assert.That(_damageable.Health.Current, Is.EqualTo(MaxHealth));
        }

        [Test]
        public void TheSameDropWithoutOpeningHurts()
        {
            _player.FallDamage = FallDamage.On;
            AddParachute(Parachute.On);
            FallFrom(HighDrop);

            TickUntil(() => _player.IsGrounded);

            Assert.That(_damageable.Health.Current, Is.LessThan(MaxHealth));
        }

        [Test]
        public void ADescentFasterThanTheSafeSpeedStillTakesNoDamage()
        {
            _player.FallDamage = FallDamage.On;
            var parachute = _body.AddComponent<ParachuteMovement>();
            var serialized = new SerializedObject(parachute);
            serialized.FindProperty("_descentSpeed").floatValue = _player.Motor.Spec.SafeFallSpeed * 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            parachute.Initialize(_player);
            parachute.Rule = Parachute.On;
            FallFrom(HighDrop);
            Assert.That(parachute.TryOpen(), Is.EqualTo(ParachuteOpenVerdict.Open));

            TickUntil(() => !parachute.IsOpen);

            Assert.That(parachute.LastCloseReason, Is.EqualTo(ParachuteCloseReason.Landed));
            Assert.That(_damageable.Health.Current, Is.EqualTo(MaxHealth));
        }

        [Test]
        public void DoesNotOpenTooLow()
        {
            var parachute = AddParachute(Parachute.On);
            LandOnFloor();

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Parachute\] open=TooLow height=0\.\d\d$"));
            Assert.That(parachute.TryOpen(), Is.EqualTo(ParachuteOpenVerdict.TooLow), "standing on the floor");

            FallFrom(parachute.Motion.MinOpenHeight / 2f);
            Assert.That(parachute.TryOpen(), Is.EqualTo(ParachuteOpenVerdict.TooLow), "in the air but too low");
            Assert.That(parachute.IsOpen, Is.False);

            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.IsMovementOverridden, Is.False);
        }

        [Test]
        public void DoesNotOpenWithTheRuleOff()
        {
            var parachute = AddParachute(Parachute.Off);
            FallFrom(20f);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Parachute\] open=RuleOff height="));
            Assert.That(parachute.TryOpen(), Is.EqualTo(ParachuteOpenVerdict.RuleOff));
            Assert.That(parachute.IsOpen, Is.False);
        }

        [Test]
        public void ADisabledComponentDoesNotOpen()
        {
            var parachute = AddParachute(Parachute.On);
            parachute.enabled = false;
            FallFrom(20f);

            Assert.That(parachute.TryOpen(), Is.EqualTo(ParachuteOpenVerdict.RuleOff));
            Assert.That(parachute.IsOpen, Is.False);
        }

        [Test]
        public void DoesNotOpenTwice()
        {
            var parachute = AddParachute(Parachute.On);
            FallFrom(20f);
            parachute.TryOpen();

            Assert.That(parachute.TryOpen(), Is.EqualTo(ParachuteOpenVerdict.AlreadyOpen));
        }

        [Test]
        public void TurningTheRuleOffClosesIt()
        {
            var parachute = AddParachute(Parachute.On);
            FallFrom(20f);
            parachute.TryOpen();
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);

            LogAssert.Expect(LogType.Log, "[Parachute] closed reason=RuleOff");
            parachute.Rule = Parachute.Off;

            Assert.That(parachute.IsOpen, Is.False);
            Assert.That(parachute.LastCloseReason, Is.EqualTo(ParachuteCloseReason.RuleOff));
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.IsMovementOverridden, Is.False);
        }

        [Test]
        public void ConfigureTakesTheRule()
        {
            var parachute = AddParachute();

            parachute.Configure(GameRules.Default.WithActions(ActionRules.Default.WithParachute(Parachute.On)));
            Assert.That(parachute.Rule, Is.EqualTo(Parachute.On));

            parachute.Configure(GameRules.Default);
            Assert.That(parachute.Rule, Is.EqualTo(Parachute.Off));

            Assert.Throws<ArgumentNullException>(() => parachute.Configure(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => parachute.Rule = (Parachute)99);
        }

        [Test]
        public void DoesNotOpenWhileAnotherMovementIsTakenOver()
        {
            var parachute = AddParachute(Parachute.On);
            var other = new HoldingOverride();
            _player.RegisterMovementOverride(other);
            FallFrom(20f);
            Assert.That(_player.ActiveMovementOverride, Is.SameAs(other));

            Assert.That(parachute.TryOpen(), Is.EqualTo(ParachuteOpenVerdict.Busy));
            Assert.That(parachute.IsOpen, Is.False);
        }

        [Test]
        public void SteersWithTheMoveInput()
        {
            var parachute = AddParachute(Parachute.On);
            FallFrom(30f);
            parachute.TryOpen();
            var start = _body.transform.position;

            for (var i = 0; i < 50; i++)
            {
                _player.Tick(Vector2.right, false, false, false, StepSeconds);
            }

            var moved = _body.transform.position - start;
            Assert.That(parachute.IsOpen, Is.True);
            Assert.That(moved.x, Is.GreaterThan(0.5f), "drifts to the right");
            Assert.That(Mathf.Abs(moved.z), Is.LessThan(0.05f));
            Assert.That(_player.Motor.Velocity.X, Is.GreaterThan(0f));
            Assert.That(_player.Motor.Velocity.X, Is.LessThanOrEqualTo(parachute.Motion.HorizontalSpeed + 1e-4f));
        }

        [Test]
        public void TryOpenAtLiftsTheBodyAndOpensOrPutsItBack()
        {
            var parachute = AddParachute(Parachute.Off);
            LandOnFloor();
            var ground = _body.transform.position;

            Assert.That(parachute.TryOpenAt(ground + (Vector3.up * 30f)), Is.EqualTo(ParachuteOpenVerdict.RuleOff));
            Assert.That(_body.transform.position, Is.EqualTo(ground), "put back when it cannot open");

            parachute.Rule = Parachute.On;
            Assert.That(parachute.TryOpenAt(ground + (Vector3.up * 30f)), Is.EqualTo(ParachuteOpenVerdict.Open));
            Assert.That(_body.transform.position.y, Is.EqualTo(ground.y + 30f).Within(1e-4f));

            // 移した直後は接地が床の上のままだが、一度動かすまでは着地と見なさない。
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.ActiveMovementOverride, Is.SameAs(parachute));
            Assert.That(parachute.IsOpen, Is.True);

            Assert.Throws<ArgumentOutOfRangeException>(() => parachute.TryOpenAt(new Vector3(float.NaN, 0f, 0f)));
        }

        [Test]
        public void MeasuresTheHeightAboveTheGroundIgnoringItsOwnBody()
        {
            var parachute = AddParachute();
            _body.transform.position = new Vector3(0f, 1f + 7f, 0f);
            Physics.SyncTransforms();

            Assert.That(parachute.MeasureHeightAboveGround(), Is.EqualTo(7f).Within(1e-3f));

            Object.DestroyImmediate(_floor);
            _floor = new GameObject("NoFloor");
            Physics.SyncTransforms();
            Assert.That(parachute.MeasureHeightAboveGround(), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void InitializeRejectsAnotherOrAnUninitializedMover()
        {
            var parachute = _body.AddComponent<ParachuteMovement>();
            var other = new GameObject("Other");
            try
            {
                other.AddComponent<CharacterController>();
                var otherMover = other.AddComponent<PlayerController>();

                Assert.Throws<ArgumentNullException>(() => parachute.Initialize(null));
                Assert.Throws<ArgumentException>(() => parachute.Initialize(otherMover));
                Assert.Throws<InvalidOperationException>(() => parachute.TryOpen(), "not initialized");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void TheInstallerAddsItWithTheRule()
        {
            Assert.That(PlayerActions.Installers.Any(i => i is ParachuteActionInstaller), Is.True, "registered on load");
            var installer = new ParachuteActionInstaller();

            installer.Install(_body, GameRules.Default.WithActions(ActionRules.Default.WithParachute(Parachute.On)));
            var parachute = _body.GetComponent<ParachuteMovement>();
            Assert.That(parachute, Is.Not.Null);
            Assert.That(parachute.IsInitialized, Is.True);
            Assert.That(parachute.Rule, Is.EqualTo(Parachute.On));

            installer.Install(_body, GameRules.Default);
            Assert.That(_body.GetComponents<ParachuteMovement>(), Has.Length.EqualTo(1));
            Assert.That(parachute.Rule, Is.EqualTo(Parachute.Off));
            Assert.That(_player.MovementOverrides.Count(o => ReferenceEquals(o, parachute)), Is.EqualTo(1));
        }

        [Test]
        public void TheInstallerSkipsObjectsThatDoNotMove()
        {
            var target = new GameObject("Target");
            try
            {
                new ParachuteActionInstaller().Install(target, GameRules.Default);
                Assert.That(target.GetComponent<ParachuteMovement>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void LogsHaveTheTag()
        {
            Assert.That(ParachuteMovement.FormatOpenLog(ParachuteOpenVerdict.Open, 12f), Is.EqualTo("[Parachute] opened height=12.00"));
            Assert.That(ParachuteMovement.FormatOpenLog(ParachuteOpenVerdict.TooLow, 1.2f), Is.EqualTo("[Parachute] open=TooLow height=1.20"));
            Assert.That(
                ParachuteMovement.FormatOpenLog(ParachuteOpenVerdict.Open, float.PositiveInfinity),
                Is.EqualTo("[Parachute] opened height=inf"));
            Assert.That(ParachuteMovement.FormatClosedLog(ParachuteCloseReason.Landed), Is.EqualTo("[Parachute] closed reason=Landed"));
        }

        private ParachuteMovement AddParachute(Parachute rule = Parachute.Off)
        {
            var parachute = _body.AddComponent<ParachuteMovement>();
            parachute.Initialize(_player);
            parachute.Rule = rule;
            return parachute;
        }

        private void LandOnFloor()
        {
            TickUntil(() => _player.IsGrounded);
        }

        // 足元を床から feetHeight 上に置き、1 度動かして空中にいることを CharacterController に知らせる。
        private void FallFrom(float feetHeight)
        {
            _body.transform.position = new Vector3(0f, 1f + feetHeight, 0f);
            Physics.SyncTransforms();
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_player.IsGrounded, Is.False, "in the air");
        }

        private void TickUntil(Func<bool> done)
        {
            for (var i = 0; i < MaxTicks; i++)
            {
                if (done())
                {
                    return;
                }

                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.Fail("the condition was not met");
        }

        /// <summary>いつも有効で、その場に留める横取り。</summary>
        private sealed class HoldingOverride : IMovementOverride
        {
            public int Priority => 1;

            public bool IsActive => true;

            public PlayerActionConditions Conditions => PlayerActionConditions.None;

            public void Begin(PlayerController mover)
            {
            }

            public MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
            {
                return MovementOverrideStep.Move(Vector3.zero, Vector3.zero);
            }

            public void End(PlayerController mover)
            {
            }
        }
    }
}
