using System;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using InputGyroscope = UnityEngine.InputSystem.Gyroscope;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class GyroLookTests
    {
        private static readonly ControlSettings _on = ControlSettings.Default.WithGyroEnabled(true);

        private InputGyroscope _device;
        private Vector3 _angularVelocity;
        private int _sourceCalls;
        private GameObject _body;
        private Transform _pivot;
        private FirstPersonLook _look;
        private InputAction _action;

        [SetUp]
        public void SetUp()
        {
            _device = InputSystem.AddDevice<InputGyroscope>();
            InputSystem.EnableDevice(_device);
            _angularVelocity = Vector3.zero;
            _sourceCalls = 0;
            _body = new GameObject("Body");
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_body.transform, false);
            _look = _body.AddComponent<FirstPersonLook>();
            _action = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
        }

        [TearDown]
        public void TearDown()
        {
            _action.Dispose();
            Object.DestroyImmediate(_body);
            if (_device != null && _device.added)
            {
                InputSystem.RemoveDevice(_device);
            }
        }

        [Test]
        public void TurnsByTheAngularVelocityTimesTheSensitivityAndTheFrameTime()
        {
            var settings = _on.WithGyroSensitivity(200);

            // 毎秒 x 1 rad・y 0.5 rad を 0.5 秒。感度 200% なので度にして 2 倍。
            var degrees = GyroLook.ToLookDegrees(new Vector3(1f, 0.5f, 3f), settings, 1f, 0.5f);

            Assert.That(degrees.x, Is.EqualTo(-0.5f * Mathf.Rad2Deg * 2f * 0.5f).Within(1e-3f), "turning the device left turns the view left");
            Assert.That(degrees.y, Is.EqualTo(1f * Mathf.Rad2Deg * 2f * 0.5f).Within(1e-3f));
        }

        [Test]
        public void TheScaleMultipliesTheTurn()
        {
            var full = GyroLook.ToLookDegrees(new Vector3(0.2f, -0.4f, 0f), _on, 1f, 0.1f);
            var half = GyroLook.ToLookDegrees(new Vector3(0.2f, -0.4f, 0f), _on, 0.5f, 0.1f);

            Assert.That(half.x, Is.EqualTo(full.x * 0.5f).Within(1e-4f));
            Assert.That(half.y, Is.EqualTo(full.y * 0.5f).Within(1e-4f));
            Assert.That(full.x, Is.EqualTo(0.4f * Mathf.Rad2Deg * 0.1f).Within(1e-4f));
        }

        [Test]
        public void InvertGyroYFlipsOnlyTheVerticalTurn()
        {
            var plain = GyroLook.ToLookDegrees(new Vector3(1f, 0.5f, 0f), _on, 1f, 0.5f);
            var inverted = GyroLook.ToLookDegrees(new Vector3(1f, 0.5f, 0f), _on.WithInvertGyroY(true), 1f, 0.5f);

            Assert.That(inverted.x, Is.EqualTo(plain.x), "left and right stay as they are");
            Assert.That(inverted.y, Is.EqualTo(-plain.y));
        }

        [Test]
        public void InvertLookYDoesNotFlipTheGyro()
        {
            var plain = GyroLook.ToLookDegrees(new Vector3(1f, 0f, 0f), _on, 1f, 0.5f);
            var stickInverted = GyroLook.ToLookDegrees(new Vector3(1f, 0f, 0f), _on.WithInvertLookY(true), 1f, 0.5f);

            Assert.That(stickInverted, Is.EqualTo(plain));
        }

        [Test]
        public void ReadsTheConnectedGyroscopeWhenOn()
        {
            var gyro = Gyro();
            _angularVelocity = new Vector3(1f, 0.5f, 0f);

            var degrees = gyro.ReadLookDegrees(_on, false, 1f, 0.5f);

            Assert.That(degrees, Is.EqualTo(GyroLook.ToLookDegrees(_angularVelocity, _on, 1f, 0.5f)));
            Assert.That(gyro.Device, Is.SameAs(_device));
        }

        [Test]
        public void DoesNothingAndLeavesTheSensorAloneWhenOff()
        {
            var gyro = Gyro();
            _angularVelocity = new Vector3(1f, 1f, 1f);

            var degrees = gyro.ReadLookDegrees(ControlSettings.Default, true, 1f, 0.5f);

            Assert.That(degrees, Is.EqualTo(Vector2.zero));
            Assert.That(_sourceCalls, Is.Zero, "the gyroscope is not even looked up");
            Assert.That(gyro.Device, Is.Null);
        }

        [Test]
        public void AimOnlyTurnsTheViewOnlyWhileAiming()
        {
            var gyro = Gyro();
            var settings = _on.WithGyroAimOnly(true);
            _angularVelocity = new Vector3(0.3f, 0.6f, 0f);

            var hip = gyro.ReadLookDegrees(settings, false, 1f, 0.5f);
            var aiming = gyro.ReadLookDegrees(settings, true, 1f, 0.5f);
            var hipAgain = gyro.ReadLookDegrees(settings, false, 1f, 0.5f);

            Assert.That(hip, Is.EqualTo(Vector2.zero));
            Assert.That(aiming, Is.EqualTo(GyroLook.ToLookDegrees(_angularVelocity, settings, 1f, 0.5f)));
            Assert.That(aiming, Is.Not.EqualTo(Vector2.zero));
            Assert.That(hipAgain, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void WithoutAGyroscopeItDoesNothingAndSaysSoOnce()
        {
            var gyro = new GyroLook(() => null, _ => throw new AssertionException("nothing to read"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Gyro\] gyro look is on but no gyroscope is connected"));

            var first = gyro.ReadLookDegrees(_on, true, 1f, 0.5f);
            var second = gyro.ReadLookDegrees(_on, true, 1f, 0.5f);

            Assert.That(first, Is.EqualTo(Vector2.zero));
            Assert.That(second, Is.EqualTo(Vector2.zero));
            Assert.That(gyro.Device, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ARemovedGyroscopeCountsAsNone()
        {
            var gyro = Gyro();
            _angularVelocity = new Vector3(1f, 0f, 0f);
            Assert.That(gyro.ReadLookDegrees(_on, false, 1f, 0.5f), Is.Not.EqualTo(Vector2.zero));
            InputSystem.RemoveDevice(_device);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Gyro\] gyro look is on but no gyroscope is connected"));

            var degrees = gyro.ReadLookDegrees(_on, false, 1f, 0.5f);

            Assert.That(degrees, Is.EqualTo(Vector2.zero));
            Assert.That(gyro.Device, Is.Null);
        }

        [Test]
        public void EnablesADisabledSensorAndDisablesItWhenTurnedOff()
        {
            InputSystem.DisableDevice(_device);
            Assert.That(_device.enabled, Is.False);
            var gyro = Gyro();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Gyro\] enabled the gyroscope"));

            gyro.ReadLookDegrees(_on, false, 1f, 0.5f);

            Assert.That(_device.enabled, Is.True);
            Assert.That(gyro.EnabledDevice, Is.True);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Gyro\] disabled the gyroscope"));
            gyro.ReadLookDegrees(ControlSettings.Default, false, 1f, 0.5f);

            Assert.That(_device.enabled, Is.False, "turning the gyro off stops the sensor it started");
            Assert.That(gyro.EnabledDevice, Is.False);
        }

        [Test]
        public void ReleaseLeavesASensorEnabledByAnotherPartOn()
        {
            Assert.That(_device.enabled, Is.True);
            var gyro = Gyro();
            gyro.ReadLookDegrees(_on, false, 1f, 0.5f);

            gyro.Release();

            Assert.That(_device.enabled, Is.True);
            Assert.That(gyro.EnabledDevice, Is.False);
        }

        [Test]
        public void ABrokenReadingDoesNotTurnTheView()
        {
            var gyro = Gyro();
            _angularVelocity = new Vector3(float.NaN, 1f, 0f);

            Assert.That(gyro.ReadLookDegrees(_on, false, 1f, 0.5f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var gyro = Gyro();

            Assert.Throws<ArgumentNullException>(() => new GyroLook(null, _ => Vector3.zero));
            Assert.Throws<ArgumentNullException>(() => new GyroLook(() => null, null));
            Assert.Throws<ArgumentNullException>(() => gyro.ReadLookDegrees(null, false, 1f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => gyro.ReadLookDegrees(_on, false, -1f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => gyro.ReadLookDegrees(_on, false, float.NaN, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => gyro.ReadLookDegrees(_on, false, 1f, -0.5f));
            Assert.Throws<ArgumentNullException>(() => GyroLook.ToLookDegrees(Vector3.one, null, 1f, 0.5f));
        }

        [Test]
        public void TheLookHasACodeDefaultForTheGyroSensitivity()
        {
            Assert.That(_look.GyroSensitivity, Is.EqualTo(FirstPersonLook.DefaultGyroSensitivity));
            Assert.That(_look.Gyro, Is.Not.Null);

            _look.GyroSensitivity = -1f;

            Assert.That(_look.GyroSensitivity, Is.Zero);
        }

        [Test]
        public void TheLookTurnsByTheGyroWhenOn()
        {
            _look.Initialize(_pivot, _action);
            _look.Gyro = Gyro();
            _look.ControlSettings = _on;
            _angularVelocity = new Vector3(0f, -0.5f, 0f);

            _look.Turn(Vector2.zero, false, _look.ReadGyroDegrees(0.5f));

            Assert.That(_look.Yaw, Is.EqualTo(0.5f * Mathf.Rad2Deg * 0.5f).Within(1e-3f), "turning the device right turns the view right");
        }

        [Test]
        public void TheLookTiltsUpByTheGyroAndTheInvertFlipsIt()
        {
            _look.Initialize(_pivot, _action);
            _look.Gyro = Gyro();
            _angularVelocity = new Vector3(0.2f, 0f, 0f);
            var expected = 0.2f * Mathf.Rad2Deg * 0.5f;

            _look.ControlSettings = _on;
            _look.Turn(Vector2.zero, false, _look.ReadGyroDegrees(0.5f));
            Assert.That(_look.Pitch, Is.EqualTo(-expected).Within(1e-3f), "looking up is a negative pitch");

            _look.ControlSettings = _on.WithInvertGyroY(true);
            _look.Turn(Vector2.zero, false, _look.ReadGyroDegrees(0.5f));
            Assert.That(_look.Pitch, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void TheLookScalesTheGyroByItsSensitivityAndTheAdsMultiplierButNotTheAssist()
        {
            _look.Initialize(_pivot, _action);
            _look.Gyro = Gyro();
            _look.ControlSettings = _on.WithGyroSensitivity(150);
            _look.GyroSensitivity = 2f;
            _look.SensitivityMultiplier = 0.5f;
            _look.AssistLookScale = 0.25f;
            _angularVelocity = new Vector3(0f, -1f, 0f);

            _look.Turn(Vector2.zero, false, _look.ReadGyroDegrees(0.1f));

            Assert.That(_look.EffectiveGyroSensitivity, Is.EqualTo(1f));
            Assert.That(_look.Yaw, Is.EqualTo(Mathf.Rad2Deg * 0.1f * 1.5f * 2f * 0.5f).Within(1e-3f));
        }

        [Test]
        public void TheLookAddsTheGyroToTheStick()
        {
            _look.Initialize(_pivot, _action);
            _look.Gyro = Gyro();
            _look.ControlSettings = _on;
            _angularVelocity = new Vector3(0f, -0.1f, 0f);
            var gyroDegrees = _look.ReadGyroDegrees(0.5f);

            _look.Turn(new Vector2(3f, 0f), false, gyroDegrees);

            Assert.That(_look.Yaw, Is.EqualTo(3f + gyroDegrees.x).Within(1e-3f));
        }

        [Test]
        public void TheLookUsesTheGyroOnlyWhileAimingIfAimOnly()
        {
            _look.Initialize(_pivot, _action);
            _look.Gyro = Gyro();
            _look.ControlSettings = _on.WithGyroAimOnly(true);
            _angularVelocity = new Vector3(0f, -1f, 0f);

            _look.Aiming = false;
            _look.Turn(Vector2.zero, false, _look.ReadGyroDegrees(0.1f));
            Assert.That(_look.Yaw, Is.EqualTo(0f).Within(1e-3f));

            _look.Aiming = true;
            _look.Turn(Vector2.zero, false, _look.ReadGyroDegrees(0.1f));
            Assert.That(_look.Yaw, Is.EqualTo(Mathf.Rad2Deg * 0.1f).Within(1e-3f));
        }

        [Test]
        public void TheLookWithoutAGyroscopeTurnsOnlyByTheStick()
        {
            _look.Initialize(_pivot, _action);
            _look.Gyro = new GyroLook(() => null, _ => throw new AssertionException("nothing to read"));
            _look.ControlSettings = _on;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Gyro\] gyro look is on but no gyroscope is connected"));

            var gyroDegrees = _look.ReadGyroDegrees(0.5f);
            _look.Turn(new Vector2(3f, 0f), false, gyroDegrees);

            Assert.That(gyroDegrees, Is.EqualTo(Vector2.zero));
            Assert.That(_look.Yaw, Is.EqualTo(3f).Within(1e-3f));
        }

        [Test]
        public void TheLookRejectsAMissingGyroAndAnInfiniteGyroTurn()
        {
            _look.Initialize(_pivot, _action);

            Assert.Throws<ArgumentNullException>(() => _look.Gyro = null);
            Assert.Throws<ArgumentOutOfRangeException>(() => _look.Turn(Vector2.zero, false, new Vector2(float.PositiveInfinity, 0f)));
            Assert.That(_look.Yaw, Is.EqualTo(0f).Within(1e-3f));
        }

        private GyroLook Gyro()
        {
            return new GyroLook(
                () =>
                {
                    _sourceCalls++;
                    return _device;
                },
                _ => _angularVelocity);
        }
    }
}
