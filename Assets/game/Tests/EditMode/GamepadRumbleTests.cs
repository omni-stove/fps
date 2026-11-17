using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class GamepadRumbleTests
    {
        private const float Frame = 0.016f;

        private readonly List<Object> _created = new List<Object>();
        private GameObject _body;
        private Camera _camera;
        private GamepadRumble _rumble;
        private ControlSettings _settings;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            _settings = ControlSettings.Default;

            _body = Track(new GameObject("Body"));
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(eye, false);
            var rig = _body.AddComponent<ViewRig>();
            rig.Initialize(eye, _camera);
            rig.ApplyBodyVisibility(true);

            // Edit Mode では Awake と OnEnable が走らないので、実行時にそこでしていることを直接呼ぶ。
            _rumble = _body.AddComponent<GamepadRumble>();
            _rumble.Bind(rig);
            _rumble.Spec = RumbleSpec.Default;
            _rumble.SettingsSource = () => _settings;
            _rumble.ActiveDevice = InputDeviceKind.Gamepad;
            _rumble.Subscribe();
        }

        [TearDown]
        public void TearDown()
        {
            if (_rumble != null)
            {
                _rumble.Unsubscribe();
            }

            CombatEvents.ClearSubscribers();
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
        public void OwnShotRumblesWithTheFireValuesAndLogsIt()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Rumble\] pulse source=fire low=0\.150 high=0\.350 seconds=0\.08 weapon=rifle$"));

            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, "rifle");
            _rumble.Tick(Frame, false);

            Assert.That(_rumble.LastOutput, Is.EqualTo(new RumbleMotors(0.15f, 0.35f)));
        }

        [Test]
        public void ShotFromTheCameraUnderTheBodyCountsAsOwn()
        {
            CombatEvents.RaiseShotFired(_camera.gameObject, Vector3.zero, Vector3.forward, "rifle");

            Assert.That(_rumble.ActivePulses, Is.EqualTo(1));
        }

        [Test]
        public void SomeoneElsesShotDoesNotRumble()
        {
            var other = Track(new GameObject("Other"));

            CombatEvents.RaiseShotFired(other, Vector3.zero, Vector3.forward, "rifle");
            CombatEvents.RaiseShotFired(null, Vector3.zero, Vector3.forward, "rifle");
            _rumble.Tick(Frame, false);

            Assert.That(_rumble.ActivePulses, Is.Zero);
            Assert.That(_rumble.LastOutput, Is.EqualTo(RumbleMotors.Zero));
        }

        [Test]
        public void ShootingWithKeyboardAndMouseDoesNotRumble()
        {
            _rumble.ActiveDevice = InputDeviceKind.KeyboardMouse;

            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, "rifle");
            _rumble.ActiveDevice = InputDeviceKind.Gamepad;
            _rumble.Tick(Frame, false);

            Assert.That(_rumble.ActivePulses, Is.Zero, "a shot taken with the mouse is not kept for when the gamepad comes back");
            Assert.That(_rumble.LastOutput, Is.EqualTo(RumbleMotors.Zero));
        }

        [Test]
        public void TurnedOffInTheSettingsDoesNotRumble()
        {
            _settings = ControlSettings.Default.WithRumbleEnabled(false);

            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, "rifle");
            CombatEvents.RaiseDamageTaken(null, 30);
            _rumble.Tick(Frame, false);

            Assert.That(_rumble.ActivePulses, Is.Zero);
            Assert.That(_rumble.LastOutput, Is.EqualTo(RumbleMotors.Zero));
        }

        [Test]
        public void TheSettingsStrengthScalesTheOutput()
        {
            _settings = ControlSettings.Default.WithRumbleStrength(50);

            CombatEvents.RaiseDamageTaken(null, 30);
            _rumble.Tick(Frame, false);

            Assert.That(_rumble.LastOutput.Low, Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(_rumble.LastOutput.High, Is.EqualTo(0.15f).Within(1e-6f));
        }

        [Test]
        public void ZeroDamageDoesNotRumble()
        {
            CombatEvents.RaiseDamageTaken(null, 0);

            Assert.That(_rumble.ActivePulses, Is.Zero);
        }

        [Test]
        public void ExplosionFadesWithTheDistanceFromTheCamera()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Rumble\] pulse source=explosion low=0\.500 high=0\.300 seconds=0\.45 weapon=frag distance=12\.5$"));

            CombatEvents.RaiseExploded(new Vector3(0f, 0.6f, 12.5f), "frag");
            CombatEvents.RaiseExploded(new Vector3(0f, 0.6f, 30f), "frag");
            _rumble.Tick(Frame, false);

            Assert.That(_rumble.ActivePulses, Is.EqualTo(1), "an explosion beyond the radius does not rumble");
            Assert.That(_rumble.LastOutput.Low, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(_rumble.LastOutput.High, Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void OverlappingEventsTakeTheStrongerMotorValues()
        {
            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, "rifle");
            CombatEvents.RaiseDamageTaken(null, 30);
            _rumble.Tick(Frame, false);

            Assert.That(_rumble.LastOutput, Is.EqualTo(new RumbleMotors(0.6f, 0.35f)));
        }

        [Test]
        public void TheMotorsStopWhenThePulseRunsOut()
        {
            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, "rifle");
            _rumble.Tick(0.1f, false);
            LogAssert.Expect(LogType.Log, "[Rumble] stopped reason=expired");

            _rumble.Tick(Frame, false);

            Assert.That(_rumble.LastOutput, Is.EqualTo(RumbleMotors.Zero));
        }

        [Test]
        public void PausingStopsTheMotorsAndDropsThePulses()
        {
            CombatEvents.RaiseDamageTaken(null, 30);
            _rumble.Tick(Frame, false);
            LogAssert.Expect(LogType.Log, "[Rumble] stopped reason=paused");

            _rumble.Tick(Frame, true);
            var afterPause = _rumble.LastOutput;
            _rumble.Tick(Frame, false);

            Assert.That(afterPause, Is.EqualTo(RumbleMotors.Zero));
            Assert.That(_rumble.ActivePulses, Is.Zero);
            Assert.That(_rumble.LastOutput, Is.EqualTo(RumbleMotors.Zero), "the pulse does not come back after the pause");
        }

        [Test]
        public void SwitchingToKeyboardAndMouseStopsTheMotors()
        {
            CombatEvents.RaiseDamageTaken(null, 30);
            _rumble.Tick(Frame, false);
            _rumble.ActiveDevice = InputDeviceKind.KeyboardMouse;
            LogAssert.Expect(LogType.Log, "[Rumble] stopped reason=keyboardMouse");

            _rumble.Tick(Frame, false);

            Assert.That(_rumble.LastOutput, Is.EqualTo(RumbleMotors.Zero));
        }

        [Test]
        public void TurningTheSettingOffStopsTheMotors()
        {
            CombatEvents.RaiseDamageTaken(null, 30);
            _rumble.Tick(Frame, false);
            _settings = _settings.WithRumbleEnabled(false);
            LogAssert.Expect(LogType.Log, "[Rumble] stopped reason=settingsOff");

            _rumble.Tick(Frame, false);

            Assert.That(_rumble.LastOutput, Is.EqualTo(RumbleMotors.Zero));
        }

        [Test]
        public void StopDropsThePulsesAndStopsTheMotors()
        {
            CombatEvents.RaiseDamageTaken(null, 30);
            _rumble.Tick(Frame, false);
            LogAssert.Expect(LogType.Log, "[Rumble] stopped reason=disabled");

            _rumble.Stop(GamepadRumble.StopDisabled);

            Assert.That(_rumble.ActivePulses, Is.Zero);
            Assert.That(_rumble.LastOutput, Is.EqualTo(RumbleMotors.Zero));
        }

        [Test]
        public void AfterUnsubscribingEventsAreIgnored()
        {
            _rumble.Unsubscribe();

            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, "rifle");
            CombatEvents.RaiseDamageTaken(null, 30);
            CombatEvents.RaiseExploded(Vector3.zero, "frag");

            Assert.That(_rumble.ActivePulses, Is.Zero);
        }

        [Test]
        public void AHiddenViewDoesNotRumble()
        {
            _rumble.Rig.enabled = false;

            CombatEvents.RaiseDamageTaken(null, 30);

            Assert.That(_rumble.ActivePulses, Is.Zero);
        }

        [Test]
        public void TheLastUsedDeviceDecidesTheKind()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                _rumble.ActiveDevice = InputDeviceKind.KeyboardMouse;

                _rumble.NoteDevice(pad);
                var afterPad = _rumble.ActiveDevice;
                _rumble.NoteDevice(null);
                var afterNothing = _rumble.ActiveDevice;
                _rumble.NoteDevice(keyboard);

                Assert.That(afterPad, Is.EqualTo(InputDeviceKind.Gamepad));
                Assert.That(afterNothing, Is.EqualTo(InputDeviceKind.Gamepad), "no device keeps the last one");
                Assert.That(_rumble.ActiveDevice, Is.EqualTo(InputDeviceKind.KeyboardMouse));
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void BindRejectsARigOnAnotherObject()
        {
            var other = Track(new GameObject("Other")).AddComponent<ViewRig>();

            Assert.Throws<System.ArgumentNullException>(() => _rumble.Bind(null));
            Assert.Throws<System.ArgumentException>(() => _rumble.Bind(other));
        }

        [Test]
        public void TickRejectsAnInvalidTime()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _rumble.Tick(-1f, false));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _rumble.Tick(float.NaN, false));
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
