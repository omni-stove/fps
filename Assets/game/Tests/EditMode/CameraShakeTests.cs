using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class CameraShakeTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameObject _body;
        private Camera _camera;
        private ViewRig _rig;
        private CameraShake _shake;
        private CameraShakeSettings _settings;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            CameraShake.GlobalMultiplier = 1f;

            _body = Track(new GameObject("Body"));
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(eye, false);
            _rig = _body.AddComponent<ViewRig>();
            _rig.Initialize(eye, _camera);
            _rig.ApplyBodyVisibility(true);

            _settings = Track(ScriptableObject.CreateInstance<CameraShakeSettings>());
            _settings.name = "TestCameraShakeSettings";

            // Edit Mode では Awake と OnEnable が走らないので、実行時にそこでしていることを直接呼ぶ。
            _shake = _body.AddComponent<CameraShake>();
            _shake.Bind(_rig);
            _shake.Settings = _settings;
            _shake.Subscribe();
        }

        [TearDown]
        public void TearDown()
        {
            if (_shake != null)
            {
                _shake.Unsubscribe();
            }

            CombatEvents.ClearSubscribers();
            CameraShake.GlobalMultiplier = 1f;
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
        public void OwnShotAddsTheDefaultTraumaAndLogsIt()
        {
            LogAssert.Expect(LogType.Log, new Regex(
                @"^\[CameraShake\] trauma \+0\.080 total=0\.080 source=fire weapon=unknown-gun category=unknown$"));

            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, "unknown-gun");

            Assert.That(_shake.Trauma, Is.EqualTo(CameraShakeSettings.DefaultFireTrauma).Within(1e-6f));
        }

        [Test]
        public void OwnShotUsesTheTraumaOfTheWeaponCategory()
        {
            var rifle = TestWeapons.RifleSpec();
            var launcher = TestWeapons.LauncherSpec();
            _shake.WeaponSource = () => rifle;

            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, rifle.Id);
            var afterRifle = _shake.Trauma;
            _shake.ResetShake();
            _shake.WeaponSource = () => launcher;
            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, launcher.Id);

            Assert.That(afterRifle, Is.EqualTo(_settings.FireTrauma(rifle.Category)).Within(1e-6f));
            Assert.That(_shake.Trauma, Is.EqualTo(_settings.FireTrauma(WeaponCategory.Launcher)).Within(1e-6f));
            Assert.That(_shake.Trauma, Is.GreaterThan(afterRifle), "a launcher kicks harder than a rifle");
        }

        [Test]
        public void ShotFromAnotherWeaponThanTheHeldOneUsesTheDefault()
        {
            var rifle = TestWeapons.RifleSpec();
            _shake.WeaponSource = () => rifle;

            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, "previous-weapon");

            Assert.That(_shake.Trauma, Is.EqualTo(CameraShakeSettings.DefaultFireTrauma).Within(1e-6f));
        }

        [Test]
        public void ShotFromTheCameraUnderTheBodyCountsAsOwn()
        {
            CombatEvents.RaiseShotFired(_camera.gameObject, Vector3.zero, Vector3.forward, "w");

            Assert.That(_shake.Trauma, Is.GreaterThan(0f));
        }

        [Test]
        public void SomeoneElsesShotDoesNotShake()
        {
            var other = Track(new GameObject("Other"));

            CombatEvents.RaiseShotFired(other, Vector3.zero, Vector3.forward, "w");
            CombatEvents.RaiseShotFired(null, Vector3.zero, Vector3.forward, "w");

            Assert.That(_shake.Trauma, Is.Zero);
        }

        [Test]
        public void NearExplosionShakesMoreThanAFarOne()
        {
            var view = _camera.transform.position;

            CombatEvents.RaiseExploded(view + new Vector3(0f, 0f, 5f), "grenade");
            var near = _shake.Trauma;
            _shake.ResetShake();
            CombatEvents.RaiseExploded(view + new Vector3(0f, 0f, 20f), "grenade");
            var far = _shake.Trauma;
            _shake.ResetShake();
            CombatEvents.RaiseExploded(view + new Vector3(0f, 0f, CameraShakeSettings.DefaultExplosionRadius + 1f), "grenade");

            Assert.That(near, Is.EqualTo(_settings.ExplosionTraumaAt(5f)).Within(1e-4f));
            Assert.That(far, Is.GreaterThan(0f).And.LessThan(near));
            Assert.That(_shake.Trauma, Is.Zero, "out of range");
        }

        [Test]
        public void DamageShakesByTheAmount()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[CameraShake\] trauma \+0\.240 total=0\.240 source=damage amount=20$"));

            CombatEvents.RaiseDamageTaken(null, 20);

            Assert.That(_shake.Trauma, Is.EqualTo(0.24f).Within(1e-5f));
        }

        [Test]
        public void HardLandingShakesButAJumpDoesNot()
        {
            CombatEvents.RaiseLanded(_body, 5f);
            Assert.That(_shake.Trauma, Is.Zero);

            CombatEvents.RaiseLanded(_body, 18f);
            Assert.That(_shake.Trauma, Is.EqualTo(_settings.LandingTrauma(18f)).Within(1e-6f));
            Assert.That(_shake.Trauma, Is.GreaterThan(0f));
        }

        [Test]
        public void SomeoneElsesLandingDoesNotShake()
        {
            CombatEvents.RaiseLanded(Track(new GameObject("Other")), 18f);

            Assert.That(_shake.Trauma, Is.Zero);
        }

        [Test]
        public void NothingShakesWhenThisIsNotTheLocalView()
        {
            _rig.ApplyBodyVisibility(false);

            CombatEvents.RaiseShotFired(_body, Vector3.zero, Vector3.forward, "w");
            CombatEvents.RaiseExploded(_camera.transform.position, "grenade");
            CombatEvents.RaiseDamageTaken(null, 50);
            CombatEvents.RaiseLanded(_body, 18f);

            Assert.That(_shake.Trauma, Is.Zero);
        }

        [Test]
        public void NothingShakesWithoutSettings()
        {
            _shake.Settings = null;

            CombatEvents.RaiseDamageTaken(null, 50);
            _shake.Tick(1f / 60f);

            Assert.That(_shake.Trauma, Is.Zero);
            Assert.That(_rig.ShakeRoot.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void InvalidSettingsAreRejectedWithAnError()
        {
            var broken = Track(ScriptableObject.CreateInstance<CameraShakeSettings>());
            broken.name = "Broken";
            broken.ExplosionRadius = 0f;
            LogAssert.Expect(LogType.Error, new Regex(@"^\[CameraShake\] camera shake settings 'Broken': explosion radius 0 must be a positive finite number"));

            _shake.Settings = broken;

            Assert.That(_shake.Settings, Is.Null);
        }

        [Test]
        public void UnsubscribedShakeIgnoresEvents()
        {
            _shake.Unsubscribe();

            CombatEvents.RaiseDamageTaken(null, 50);

            Assert.That(_shake.Trauma, Is.Zero);
        }

        [Test]
        public void TickMovesOnlyTheShakeRootAndSettlesBack()
        {
            var cameraPosition = _camera.transform.localPosition;
            var cameraRotation = _camera.transform.localRotation;
            _shake.AddTrauma(1f, "test", null);

            _shake.Tick(1f / 60f);

            Assert.That(_shake.LastSample.IsZero, Is.False);
            Assert.That(
                _rig.ShakeRoot.localPosition != Vector3.zero || _rig.ShakeRoot.localRotation != Quaternion.identity,
                Is.True,
                "the shake root moves");
            Assert.That(_rig.ViewmodelRoot.localPosition, Is.EqualTo(Vector3.zero), "the viewmodel root is left alone");
            Assert.That(_rig.ViewmodelRoot.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(_camera.transform.localPosition, Is.EqualTo(cameraPosition), "the camera is left alone");
            Assert.That(_camera.transform.localRotation, Is.EqualTo(cameraRotation));

            for (var i = 0; i < 600 && _shake.Trauma > 0f; i++)
            {
                _shake.Tick(1f / 60f);
            }

            Assert.That(_shake.Trauma, Is.Zero);
            Assert.That(_rig.ShakeRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_rig.ShakeRoot.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void ZeroGlobalMultiplierKeepsTheShakeRootStill()
        {
            CameraShake.GlobalMultiplier = 0f;

            for (var i = 0; i < 120; i++)
            {
                CombatEvents.RaiseDamageTaken(null, 50);
                _shake.Tick(1f / 60f);

                Assert.That(_shake.LastSample.IsZero, Is.True, $"frame {i}");
                Assert.That(_rig.ShakeRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(_rig.ShakeRoot.localRotation, Is.EqualTo(Quaternion.identity));
            }

            Assert.That(_shake.Trauma, Is.GreaterThan(0f), "trauma still builds up; only the output is scaled");
        }

        [Test]
        public void TurningTheMultiplierDownStopsAShakeInProgress()
        {
            _shake.AddTrauma(1f, "test", null);
            _shake.Tick(1f / 60f);

            CameraShake.GlobalMultiplier = 0f;
            _shake.Tick(1f / 60f);

            Assert.That(_rig.ShakeRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_rig.ShakeRoot.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void GlobalMultiplierRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShake.GlobalMultiplier = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShake.GlobalMultiplier = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraShake.GlobalMultiplier = float.PositiveInfinity);
            Assert.That(CameraShake.GlobalMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void BindRejectsARigOnAnotherObject()
        {
            var other = Track(new GameObject("Other"));
            var otherRig = other.AddComponent<ViewRig>();

            Assert.Throws<ArgumentException>(() => _shake.Bind(otherRig));
            Assert.Throws<ArgumentNullException>(() => _shake.Bind(null));
            Assert.That(_shake.Rig, Is.SameAs(_rig));
        }

        [Test]
        public void CharacterComponentsAddTheShakeOnlyToBodiesWithAView()
        {
            var player = Track(new GameObject("Player"));
            player.AddComponent<ViewRig>();
            var enemy = Track(new GameObject("Enemy"));

            CharacterComponents.Attach(player, throwsGrenades: false);
            CharacterComponents.Attach(player, throwsGrenades: false);
            CharacterComponents.Attach(enemy, throwsGrenades: false);

            Assert.That(player.GetComponents<CameraShake>(), Has.Length.EqualTo(1));
            Assert.That(enemy.GetComponent<CameraShake>(), Is.Null);
        }

        [Test]
        public void DefaultSettingsAreValidAndCoverEveryCategory()
        {
            _settings.Validate();

            foreach (WeaponCategory category in Enum.GetValues(typeof(WeaponCategory)))
            {
                Assert.That(_settings.FireTrauma(category), Is.InRange(0.01f, 1f), category.ToString());
            }

            Assert.That(_settings.FireTrauma(null), Is.EqualTo(CameraShakeSettings.DefaultFireTrauma));
        }

        [Test]
        public void SettingsValidationListsEveryProblem()
        {
            _settings.DecayPerSecond = -1f;
            _settings.LandingFullSpeed = _settings.LandingMinSpeed;
            _settings.SetFireByCategory(new[]
            {
                new CameraShakeFireTrauma(WeaponCategory.Handgun, 0.1f),
                new CameraShakeFireTrauma(WeaponCategory.Handgun, 2f),
                null,
            });

            var e = Assert.Throws<CameraShakeSettingsException>(() => _settings.Validate());

            Assert.That(e.Message, Does.Contain("decay per second"));
            Assert.That(e.Message, Does.Contain("landing full speed"));
            Assert.That(e.Message, Does.Contain("repeats the weapon category Handgun"));
            Assert.That(e.Message, Does.Contain("fire trauma #1 (Handgun) 2 must be between 0 and 1"));
            Assert.That(e.Message, Does.Contain("fire trauma #2 is empty"));
        }

        [Test]
        public void ResetToDefaultsRestoresEveryValue()
        {
            _settings.DecayPerSecond = 9f;
            _settings.MaxAngles = Vector3.one * 20f;
            _settings.SetFireByCategory(null);

            _settings.ResetToDefaults();

            Assert.That(_settings.DecayPerSecond, Is.EqualTo(CameraShakeSettings.DefaultDecayPerSecond));
            Assert.That(_settings.MaxAngles, Is.EqualTo(CameraShakeSettings.DefaultMaxAngles));
            Assert.That(_settings.FireByCategory, Has.Count.EqualTo(Enum.GetValues(typeof(WeaponCategory)).Length));
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
