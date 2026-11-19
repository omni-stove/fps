using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class StealthNoiseTuningTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            // プロジェクトの Resources/ShooterKit に置かれた入口の欄に左右されないよう、空の入口に替えておく。
            GameContent.Use(Content("Empty"));
            _player = Track(new GameObject("Player"));
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
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
        public void TheKitShipsTheDefaultsInResources()
        {
            Assert.That(StealthActionInstaller.TuningResourcePath, Is.EqualTo("Tuning/stealth"));
            Assert.That(KitTuning.StealthResourcePath, Is.EqualTo(StealthActionInstaller.TuningResourcePath));
            var asset = Resources.Load<TextAsset>(StealthActionInstaller.TuningResourcePath);
            Assert.That(asset, Is.Not.Null);

            var shipped = StealthNoiseJson.Parse(asset.text, "Resources/Tuning/stealth.json");
            var defaults = StealthNoiseSpec.Default;
            Assert.That(shipped.FootstepNoiseRadius, Is.EqualTo(defaults.FootstepNoiseRadius));
            Assert.That(shipped.WalkStride, Is.EqualTo(defaults.WalkStride));
            Assert.That(shipped.SprintStride, Is.EqualTo(defaults.SprintStride));
            Assert.That(shipped.CrouchStride, Is.EqualTo(defaults.CrouchStride));
            Assert.That(shipped.WalkVolume, Is.EqualTo(defaults.WalkVolume));
            Assert.That(shipped.SprintVolume, Is.EqualTo(defaults.SprintVolume));
            Assert.That(shipped.CrouchVolume, Is.EqualTo(defaults.CrouchVolume));
            Assert.That(shipped.MinLandingFallSpeed, Is.EqualTo(defaults.MinLandingFallSpeed));
            Assert.That(shipped.HardLandingFallSpeed, Is.EqualTo(defaults.HardLandingFallSpeed));
            Assert.That(shipped.MinLandingVolume, Is.EqualTo(defaults.MinLandingVolume));

            Assert.That(StealthActionInstaller.Noise, Is.Not.SameAs(StealthNoiseSpec.Default), "read from Resources");
            Assert.That(StealthActionInstaller.Noise.FootstepNoiseRadius, Is.EqualTo(12f));
        }

        [Test]
        public void TheInstallerGivesThePlayerTheEntryValues()
        {
            var content = Content("Game");
            content.StealthTuning = Text("GameStealth", "{ \"footstepNoiseRadius\": 30, \"walkStride\": 1, \"crouchVolume\": 0.1 }");
            GameContent.Use(content);
            _player.AddComponent<FakeCharacterMotionSource>();

            new StealthActionInstaller().Install(_player, GameRules.Default.WithActions(ActionRules.Default.WithStealth(Stealth.On)));

            var target = _player.GetComponent<StealthTarget>();
            Assert.That(target.FootstepNoiseRadius, Is.EqualTo(30f));
            Assert.That(target.Cadence.WalkStride, Is.EqualTo(1f));
            Assert.That(target.Cadence.CrouchVolume, Is.EqualTo(0.1f));
            Assert.That(target.Cadence.SprintStride, Is.EqualTo(FootstepCadence.DefaultSprintStride));

            // 3 m を歩く。歩幅 1 m なら半歩ぶん進んだところから 3 歩 (既定の 2.2 m なら 1 歩)。
            Assert.That(Walk(target, 30), Is.EqualTo(3));
        }

        [Test]
        public void ABrokenEntryLogsTheItemAndTheInstallerUsesTheDefaults()
        {
            var content = Content("Broken");
            content.StealthTuning = Text("BrokenStealth", "{ \"walkStride\": 0 }");
            GameContent.Use(content);
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"(?s)^\[Tuning\] stealth JSON 'GameContent 'Broken' \(BrokenStealth\)' item 'walkStride' is invalid: .*; using the default values instead$"));

            new StealthActionInstaller().Install(_player, GameRules.Default);

            var target = _player.GetComponent<StealthTarget>();
            Assert.That(target.FootstepNoiseRadius, Is.EqualTo(StealthNoiseSpec.DefaultFootstepNoiseRadius));
            Assert.That(target.Cadence.WalkStride, Is.EqualTo(FootstepCadence.DefaultWalkStride));
        }

        [Test]
        public void ApplyingTheNoiseRestartsTheCadenceAndOverridesTheInspectorRadius()
        {
            _player.AddComponent<FakeCharacterMotionSource>();
            var target = _player.AddComponent<StealthTarget>();
            target.Rule = Stealth.On;
            target.FootstepNoiseRadius = 5f;
            Walk(target, 3);
            var before = target.Cadence;

            target.ApplyNoise(new StealthNoiseSpec(footstepNoiseRadius: 20f));

            Assert.That(target.FootstepNoiseRadius, Is.EqualTo(20f));
            Assert.That(target.Cadence, Is.Not.SameAs(before));
            Assert.That(target.Cadence.Progress, Is.EqualTo(FootstepCadence.FirstStepProgress));
            Assert.Throws<System.ArgumentNullException>(() => target.ApplyNoise(null));
        }

        // 1 フレーム 0.02 秒で 0.1 m (5 m/s、歩きの速さ) 進める。
        private static int Walk(StealthTarget target, int frames)
        {
            var steps = 0;
            for (var i = 0; i < frames; i++)
            {
                if (target.AdvanceFootsteps(new Vector3(0f, 0f, 0.1f), 0.02f))
                {
                    steps++;
                }
            }

            return steps;
        }

        private GameContent Content(string name)
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = name;
            return content;
        }

        private TextAsset Text(string name, string json)
        {
            var asset = Track(new TextAsset(json));
            asset.name = name;
            return asset;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
