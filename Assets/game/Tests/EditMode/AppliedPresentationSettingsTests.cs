using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AppliedPresentationSettingsTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _logs = new List<string>();
        private ControlSettings _appliedBefore;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            CombatEvents.ClearSubscribers();
            BloodPresenter.ResetStatics();
            AppliedControlSettings.Apply(ControlSettings.Default);
            _logs.Clear();
            Application.logMessageReceived += Capture;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= Capture;
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] == null)
                {
                    continue;
                }

                if (_created[i] is GameObject body && body.TryGetComponent<BloodPresenter>(out var presenter) && presenter.PoolRoot != null)
                {
                    Object.DestroyImmediate(presenter.PoolRoot.gameObject);
                }

                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            CombatEvents.ClearSubscribers();
            AppliedControlSettings.Apply(_appliedBefore);

            // ほかのテストが初期値を前提にするので、保存した設定ではなく初期値に戻す。
            CameraShake.GlobalMultiplier = 1f;
            ViewmodelMotion.GlobalMultiplier = 1f;
            BloodPresenter.ResetStatics();
        }

        [Test]
        public void ApplyingPassesTheIntensitiesAndTheBloodFlagToThePresenters()
        {
            AppliedControlSettings.Apply(ControlSettings.Default
                .With(ControlValue.CameraShakeIntensity, 40)
                .With(ControlValue.ViewmodelMotionIntensity, 75)
                .WithShowBlood(false));

            Assert.That(CameraShake.GlobalMultiplier, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(ViewmodelMotion.GlobalMultiplier, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(BloodPresenter.BloodEnabled, Is.False);
            Assert.That(_logs, Has.Member(CameraShake.FormatMultiplierLog(0.4f)));
            Assert.That(_logs, Has.Member(ViewmodelMotion.FormatMultiplierLog(0.75f)));
            Assert.That(_logs, Has.Member(BloodPresenter.FormatEnabledLog(false)));

            AppliedControlSettings.Apply(ControlSettings.Default);

            Assert.That(CameraShake.GlobalMultiplier, Is.EqualTo(1f));
            Assert.That(ViewmodelMotion.GlobalMultiplier, Is.EqualTo(1f));
            Assert.That(BloodPresenter.BloodEnabled, Is.True);
        }

        [Test]
        public void TheDefaultsShakeAndBleedAsBefore()
        {
            var shake = Shake();
            var blood = Blood();

            CombatEvents.RaiseDamageTaken(null, 50);
            var shown = blood.PlayImpact(CharacterImpact(), Vector3.forward);

            Assert.That(shake.Trauma, Is.GreaterThan(0f));
            Assert.That(shown, Is.True);
            Assert.That(_logs.Exists(log => log.StartsWith(CameraShake.LogTag + " trauma", StringComparison.Ordinal)), Is.True);
            Assert.That(_logs.Exists(log => log.StartsWith(BloodPresenter.LogTag + " spray", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void NoShakeAndNoBloodLeaveNoTraumaAndNoBloodLogs()
        {
            var shake = Shake();
            var blood = Blood();
            AppliedControlSettings.Apply(ControlSettings.Default.With(ControlValue.CameraShakeIntensity, 0).WithShowBlood(false));
            _logs.Clear();

            CombatEvents.RaiseDamageTaken(null, 50);
            var shown = blood.PlayImpact(CharacterImpact(), Vector3.forward);
            shake.Tick(1f / 60f);

            Assert.That(shake.Trauma, Is.Zero);
            Assert.That(shake.LastSample.IsZero, Is.True);
            Assert.That(shown, Is.False);
            Assert.That(blood.PoolRoot, Is.Null, "nothing is instantiated");
            Assert.That(_logs.Exists(log => log.StartsWith(CameraShake.LogTag + " trauma", StringComparison.Ordinal)), Is.False);
            Assert.That(_logs.Exists(log => log.StartsWith(BloodPresenter.LogTag + " spray", StringComparison.Ordinal)), Is.False);
            Assert.That(_logs.Exists(log => log.StartsWith(BloodPresenter.LogTag + " decal", StringComparison.Ordinal)), Is.False);
        }

        private void Capture(string condition, string stackTrace, LogType type)
        {
            _logs.Add(condition);
        }

        private CameraShake Shake()
        {
            var body = Track(new GameObject("Body"));
            var eye = new GameObject("Eye").transform;
            eye.SetParent(body.transform, false);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(eye, false);
            var rig = body.AddComponent<ViewRig>();
            rig.Initialize(eye, camera);
            rig.ApplyBodyVisibility(true);

            var settings = Track(ScriptableObject.CreateInstance<CameraShakeSettings>());
            settings.name = "TestCameraShakeSettings";

            // Edit Mode では Awake と OnEnable が走らないので、実行時にそこでしていることを直接呼ぶ。
            var shake = body.AddComponent<CameraShake>();
            shake.Bind(rig);
            shake.Settings = settings;
            shake.Subscribe();
            return shake;
        }

        private BloodPresenter Blood()
        {
            var spray = Track(new GameObject("Spray"));
            var main = spray.AddComponent<ParticleSystem>().main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var catalog = Track(ScriptableObject.CreateInstance<HitFeedbackCatalog>());
            catalog.SetSprays(new[] { new ZoneBloodSpray(HitZone.Body, spray) });
            catalog.DefaultSpray = spray;

            var presenter = Track(new GameObject("Shooter")).AddComponent<BloodPresenter>();
            presenter.Catalog = catalog;
            return presenter;
        }

        private static ImpactInfo CharacterImpact()
        {
            return new ImpactInfo(new Vector3(0f, 1f, 10f), Vector3.back, null, "flesh", true, HitZone.Body);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
