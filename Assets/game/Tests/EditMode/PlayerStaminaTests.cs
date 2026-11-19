using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class PlayerStaminaTests
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
        private bool _sprintWasHeld;

        // 最大 10、走ると毎秒 10 (1 秒で尽きる)、滑ると 3、回避で 4、毎秒 10 戻る、待ち 0.5 秒、尽きたら 5 まで戻るまで使えない。
        private static StaminaSpec Spec(float regenDelaySeconds = 0.5f, float dodgeCost = 4f) =>
            new StaminaSpec(10f, 10f, 3f, dodgeCost, 10f, regenDelaySeconds, 5f);

        [SetUp]
        public void SetUp()
        {
            GameSignals.Stamina.Clear();
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
            _sprintWasHeld = false;

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
            GameSignals.Stamina.Clear();
        }

        [Test]
        public void TheInstallerIsNotRegisteredWithPlayerActions()
        {
            Assert.That(PlayerActions.Installers.Any(i => i is StaminaActionInstaller), Is.False);
        }

        [Test]
        public void InstallingPublishesAFullMeterAndGatesThePlayer()
        {
            var stamina = Install(Stamina.On, Spec());

            Assert.That(stamina, Is.Not.Null);
            Assert.That(stamina.enabled, Is.True);
            Assert.That(_player.StaminaGate, Is.SameAs(stamina));
            Assert.That(GameSignals.Stamina.Value, Is.EqualTo(new StaminaStatus(10f, 10f, false)));

            Install(Stamina.On, Spec());
            Assert.That(_body.GetComponents<PlayerStamina>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void SprintingRunsOutStopsTheSprintAndComesBackAfterStopping()
        {
            var stamina = Install(Stamina.On, Spec());
            LogAssert.Expect(LogType.Log, "[Stamina] depleted current=0.00 max=10.00 depleted=true");
            LogAssert.Expect(LogType.Log, "[Player] sprint=off mode=hold reason=stamina");

            var sprintingFrames = 0;
            for (var i = 0; i < 65; i++)
            {
                Press(sprint: true);
                if (_player.IsSprinting)
                {
                    sprintingFrames++;
                }
            }

            Assert.That(sprintingFrames, Is.InRange(48, 51), "about a second of sprinting at 10 per second from 10");
            Assert.That(stamina.Meter.IsDepleted, Is.True);
            Assert.That(_player.IsSprinting, Is.False, "holding the button does not sprint while run out");
            Assert.That(_player.Motor.Velocity.Z, Is.EqualTo(_player.WalkSpeed).Within(1e-3f), "walks instead");
            Assert.That(GameSignals.Stamina.Value.HasValue, Is.True);
            Assert.That(GameSignals.Stamina.Value.Value.Depleted, Is.True);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stamina\] recovered current=5\.\d\d max=10\.00 depleted=false$"));
            for (var i = 0; i < 50; i++)
            {
                Press(sprint: false);
            }

            Assert.That(stamina.Meter.IsDepleted, Is.False, "0.5s wait and 0.5s at 10 per second reach the recover amount");
            Assert.That(stamina.Meter.Current, Is.GreaterThan(5f).And.LessThan(10f));
            Assert.That(GameSignals.Stamina.Value.Value.Depleted, Is.False);

            Press(sprint: true);
            Assert.That(_player.IsSprinting, Is.True, "pressing again sprints once recovered");
        }

        [Test]
        public void StaminaComesBackOnlyAfterTheDelay()
        {
            var stamina = Install(Stamina.On, Spec());

            for (var i = 0; i < 10; i++)
            {
                Press(sprint: true);
            }

            Assert.That(stamina.Meter.Current, Is.EqualTo(8f).Within(1e-3f), "0.2s of sprinting");

            for (var i = 0; i < 24; i++)
            {
                Press(sprint: false);
            }

            Assert.That(stamina.Meter.Current, Is.EqualTo(8f).Within(1e-3f), "still within the 0.5s wait");

            for (var i = 0; i < 6; i++)
            {
                Press(sprint: false);
            }

            Assert.That(stamina.Meter.Current, Is.EqualTo(9f).Within(0.05f), "0.1s past the wait at 10 per second");
            Assert.That(GameSignals.Stamina.Value.Value.Current, Is.EqualTo(stamina.Meter.Current));
        }

        [Test]
        public void StandingStillWithTheButtonHeldDoesNotDrain()
        {
            var stamina = Install(Stamina.On, Spec());

            for (var i = 0; i < 20; i++)
            {
                _player.ApplyInput(MoveInput(Vector2.zero, sprint: true), StepSeconds);
                _sprintWasHeld = true;
            }

            Assert.That(stamina.Meter.Current, Is.EqualTo(10f));
        }

        [Test]
        public void DodgeSpendsItsCostAndIsRefusedWhenShort()
        {
            var stamina = Install(Stamina.On, Spec(regenDelaySeconds: 30f));
            _player.DodgeRule = Dodge.On;

            LogAssert.Expect(LogType.Log, "[Stamina] spent use=Dodge cost=4.00 current=6.00 max=10.00 depleted=false");
            Assert.That(_player.TryStartDodge(new Vector2(1f, 0f)), Is.True);
            Assert.That(stamina.Meter.Current, Is.EqualTo(6f).Within(1e-5f));

            stamina.Spend(StaminaUse.Dodge);
            Assert.That(stamina.Meter.Current, Is.EqualTo(2f).Within(1e-5f));
            TickUntilDodgeEnds();
            for (var i = 0; i < 40; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            LogAssert.Expect(LogType.Log, "[Stamina] refused use=Dodge cost=4.00 current=2.00 max=10.00 depleted=false");
            Assert.That(_player.TryStartDodge(new Vector2(-1f, 0f)), Is.False, "the cooldown has passed but 2 is short of 4");
            Assert.That(_player.IsDodging, Is.False);
            Assert.That(stamina.Meter.Current, Is.EqualTo(2f).Within(1e-5f), "a refused dodge spends nothing");
        }

        [Test]
        public void SlideSpendsItsCostAndIsRefusedWhenShort()
        {
            var stamina = Install(Stamina.On, Spec(regenDelaySeconds: 30f, dodgeCost: 5f));
            _slide = _body.AddComponent<SlideMovement>();
            _slide.Configure(Rules(Stamina.On));

            Run(8f);
            Assert.That(_slide.TryStart(), Is.True);
            LogAssert.Expect(LogType.Log, "[Stamina] spent use=Slide cost=3.00 current=7.00 max=10.00 depleted=false");
            _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            Assert.That(_slide.IsSliding, Is.True);
            Assert.That(stamina.Meter.Current, Is.EqualTo(7f).Within(1e-5f));
            _slide.Cancel();

            stamina.Spend(StaminaUse.Dodge);
            Assert.That(stamina.Meter.Current, Is.EqualTo(2f).Within(1e-5f));

            Run(8f);
            LogAssert.Expect(LogType.Log, "[Stamina] refused use=Slide cost=3.00 current=2.00 max=10.00 depleted=false");
            Assert.That(_slide.TryStart(), Is.False);
            Assert.That(stamina.Meter.Current, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void WithTheRuleOffNothingIsLimited()
        {
            var installer = new StaminaActionInstaller(Spec());
            installer.Install(_body, Rules(Stamina.Off));

            Assert.That(_body.GetComponent<PlayerStamina>(), Is.Null, "off does not add the part");
            Assert.That(_player.StaminaGate, Is.Null);
            SprintFor(100);
            Assert.That(GameSignals.Stamina.Value, Is.Null);
        }

        [Test]
        public void TurningTheRuleOffAfterOnLiftsTheLimitAndClearsTheSignal()
        {
            var spec = Spec();
            var stamina = Install(Stamina.On, spec);
            for (var i = 0; i < 10; i++)
            {
                Press(sprint: true);
            }

            Install(Stamina.Off, spec);

            Assert.That(stamina.enabled, Is.False);
            Assert.That(stamina.IsOn, Is.False);
            Assert.That(_player.StaminaGate, Is.Null);
            Assert.That(GameSignals.Stamina.Value, Is.Null);
            Assert.That(stamina.CanSprint, Is.True);
            Assert.That(stamina.CanAfford(StaminaUse.Dodge), Is.True);

            SprintFor(100);

            Install(Stamina.On, spec);
            Assert.That(_player.StaminaGate, Is.SameAs(stamina));
            Assert.That(stamina.Meter.Current, Is.EqualTo(8f).Within(1e-3f), "the same spec keeps the meter");
        }

        [Test]
        public void TheInstallerSkipsObjectsThatDoNotMove()
        {
            var target = new GameObject("Target");
            try
            {
                new StaminaActionInstaller(Spec()).Install(target, Rules(Stamina.On));
                Assert.That(target.GetComponent<PlayerStamina>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }

            Assert.Throws<System.ArgumentNullException>(() => new StaminaActionInstaller(null));
            Assert.That(new StaminaActionInstaller().Spec, Is.SameAs(StaminaSpec.Default));
        }

        private PlayerStamina Install(Stamina rule, StaminaSpec spec)
        {
            new StaminaActionInstaller(spec).Install(_body, Rules(rule));
            return _body.GetComponent<PlayerStamina>();
        }

        private void SprintFor(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                Press(sprint: true);
            }

            Assert.That(_player.IsSprinting, Is.True, $"still sprinting after {frames} frames");
            Assert.That(_player.Motor.Velocity.Z, Is.EqualTo(_player.SprintSpeed).Within(1e-3f));
        }

        private void Settle()
        {
            for (var i = 0; i < 30 && !_player.IsGrounded; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_player.IsGrounded, Is.True, "landed");
        }

        // 走っている速度だけを入れる (ダッシュの入力を介さず滑り出しを確かめる)。
        private void Run(float speed)
        {
            _player.Motor.SetVelocity(new System.Numerics.Vector3(0f, 0f, speed));
        }

        private void Press(bool sprint)
        {
            _player.ApplyInput(MoveInput(Vector2.up, sprint), StepSeconds);
            _sprintWasHeld = sprint;
        }

        private PlayerMoveInput MoveInput(Vector2 move, bool sprint)
        {
            return new PlayerMoveInput(
                move,
                sprint,
                sprint && !_sprintWasHeld,
                InputDeviceKind.KeyboardMouse,
                false,
                false,
                InputDeviceKind.KeyboardMouse,
                false,
                false);
        }

        private void TickUntilDodgeEnds()
        {
            for (var i = 0; i < MaxSteps && _player.IsDodging; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, StepSeconds);
            }

            Assert.That(_player.IsDodging, Is.False, "the roll ended");
        }

        private static GameRules Rules(Stamina stamina)
        {
            return GameRules.Default.WithActions(GameRules.Default.Actions.WithStamina(stamina).WithSlide(Slide.On));
        }
    }
}
