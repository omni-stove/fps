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
    public sealed class SuppressionTuningTests
    {
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            // プロジェクトの Resources/ShooterKit に置かれた入口の欄に左右されないよう、空の入口に替えておく。
            GameContent.Use(Content("Empty"));
        }

        [TearDown]
        public void TearDown()
        {
            EnemySuppression.ConfigureAll(Suppression.Off, SuppressionSpec.Default);
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
        public void TheBundledFileHoldsTheDefaults()
        {
            Assert.That(KitTuning.SuppressionResourcePath, Is.EqualTo("Tuning/suppression"));
            Assert.That(Resources.Load<TextAsset>(KitTuning.SuppressionResourcePath), Is.Not.Null, "Resources/Tuning/suppression.json");

            // 壊れていれば KitTuning がエラーを出し、テストの側で失敗になる。
            var spec = KitTuning.Suppression;

            Assert.That(spec, Is.Not.SameAs(SuppressionSpec.Default), "read from the bundled file");
            Assert.That(spec.NearMissRadius, Is.EqualTo(1.5f));
            Assert.That(spec.BuildPerNearMiss, Is.EqualTo(0.2f));
            Assert.That(spec.DecayDelaySeconds, Is.EqualTo(0.75f));
            Assert.That(spec.DecayPerSecond, Is.EqualTo(0.4f));
            Assert.That(spec.SuppressedLevel, Is.EqualTo(0.5f));
            Assert.That(spec.ReleasedLevel, Is.EqualTo(0.2f));
            Assert.That(spec.MaxSpreadDegrees, Is.EqualTo(4f));
            Assert.That(spec.MaxCrosshairSpread, Is.EqualTo(0.6f));
            Assert.That(spec.MaxVignetteAlpha, Is.EqualTo(0.65f));
            Assert.That(KitTuning.Suppression, Is.SameAs(spec), "read once and kept");
        }

        [Test]
        public void TheEntryFieldReachesThePlayerThroughTheBuiltInInstaller()
        {
            var content = Content("Game");
            content.SuppressionTuning = Text("GameSuppression", "{ \"nearMissRadius\": 4, \"buildPerNearMiss\": 0.9 }");
            GameContent.Use(content);
            var player = Track(new GameObject("Player"));
            var installer = new SuppressionActionInstaller();

            installer.Install(player, GameRules.Default.WithActions(ActionRules.Default.WithSuppression(Suppression.On)));

            var receiver = player.GetComponent<PlayerSuppression>().Receiver;
            Assert.That(installer.Spec, Is.SameAs(KitTuning.Suppression));
            Assert.That(receiver.Spec, Is.SameAs(installer.Spec));
            Assert.That(receiver.Spec.NearMissRadius, Is.EqualTo(4f));
            Assert.That(receiver.Spec.BuildPerNearMiss, Is.EqualTo(0.9f));
            Assert.That(receiver.Spec.DecayPerSecond, Is.EqualTo(0.4f), "left-out items keep the defaults");
            Assert.That(receiver.CreateSpec().NearMissRadius, Is.EqualTo(4f), "the Inspector shows the value in effect");
        }

        [Test]
        public void AGivenSpecWinsOverTheFile()
        {
            var given = new SuppressionSpec(nearMissRadius: 2f);
            var content = Content("Game");
            content.SuppressionTuning = Text("GameSuppression", "{ \"nearMissRadius\": 4 }");
            GameContent.Use(content);

            Assert.That(new SuppressionActionInstaller(given).Spec, Is.SameAs(given));
        }

        [Test]
        public void ABrokenEntryFieldLogsTheItemAndUsesTheDefaults()
        {
            var content = Content("Broken");
            content.SuppressionTuning = Text("BrokenSuppression", "{ \"suppressedLevel\": 0.1, \"releasedLevel\": 0.3 }");
            GameContent.Use(content);
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"(?s)^\[Tuning\] suppression JSON 'GameContent 'Broken' \(BrokenSuppression\)' item 'releasedLevel' is invalid: .*; using the default values instead$"));

            Assert.That(new SuppressionActionInstaller().Spec, Is.SameAs(SuppressionSpec.Default));
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
