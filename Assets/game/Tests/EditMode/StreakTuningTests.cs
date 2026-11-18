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
    public sealed class StreakTuningTests
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
            var asset = Resources.Load<TextAsset>(StreakJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "Resources/Tuning/streaks.json");

            var spec = StreakJson.Parse(asset.text, "Resources/Tuning/streaks.json");

            Assert.That(spec.Rewards.Count, Is.EqualTo(2));
            Assert.That(spec.Rewards[0].Id, Is.EqualTo(StreakSpec.UavRewardId));
            Assert.That(spec.Rewards[0].DisplayNameKey, Is.EqualTo(StreakSpec.DefaultUavNameKey));
            Assert.That(spec.Rewards[0].KillsRequired, Is.EqualTo(3));
            Assert.That(spec.Rewards[1].Id, Is.EqualTo(StreakSpec.AirstrikeRewardId));
            Assert.That(spec.Rewards[1].DisplayNameKey, Is.EqualTo(StreakSpec.DefaultAirstrikeNameKey));
            Assert.That(spec.Rewards[1].KillsRequired, Is.EqualTo(5));
            Assert.That(spec.Uav.DurationSeconds, Is.EqualTo(StreakUavSpec.DefaultDurationSeconds));
            Assert.That(spec.Uav.Range, Is.EqualTo(StreakUavSpec.DefaultRange));
            Assert.That(spec.Airstrike.ShellCount, Is.EqualTo(StreakAirstrikeSpec.DefaultShellCount));
            Assert.That(spec.Airstrike.IntervalSeconds, Is.EqualTo(StreakAirstrikeSpec.DefaultIntervalSeconds));
            Assert.That(spec.Airstrike.Spread, Is.EqualTo(StreakAirstrikeSpec.DefaultSpread));
            Assert.That(spec.Airstrike.Explosion.Damage.MaxDamage, Is.EqualTo(StreakAirstrikeSpec.DefaultMaxDamage));
            Assert.That(spec.Airstrike.Explosion.Damage.Radius, Is.EqualTo(StreakAirstrikeSpec.DefaultRadius));
            Assert.That(spec.Airstrike.Explosion.Damage.FullDamageRadius, Is.EqualTo(StreakAirstrikeSpec.DefaultFullDamageRadius));
            Assert.That(spec.Airstrike.Explosion.StructureDamageAtCenter, Is.EqualTo(StreakAirstrikeSpec.DefaultStructureDamageAtCenter));
            Assert.That(spec.Airstrike.Range, Is.EqualTo(StreakAirstrikeSpec.DefaultRange));
            Assert.That(spec.Airstrike.LineLength, Is.EqualTo(StreakAirstrikeSpec.DefaultLineLength));
            Assert.That(spec.Airstrike.DropHeight, Is.EqualTo(StreakAirstrikeSpec.DefaultDropHeight));
            Assert.That(spec.Targeting.CursorSpeed, Is.EqualTo(StreakTargetingSpec.DefaultCursorSpeed));
            Assert.That(spec.Targeting.TurnDegreesPerSecond, Is.EqualTo(StreakTargetingSpec.DefaultTurnDegreesPerSecond));
            Assert.That(spec.Targeting.ViewHeight, Is.EqualTo(StreakTargetingSpec.DefaultViewHeight));
        }

        [Test]
        public void AnEmptyEntryFieldFallsBackToTheBundledFile()
        {
            var spec = Load();

            Assert.That(spec, Is.Not.SameAs(StreakSpec.Default), "read from Resources");
            Assert.That(spec.Rewards.Count, Is.EqualTo(2));
            Assert.That(spec.Rewards[0].KillsRequired, Is.EqualTo(3));
            Assert.That(spec.Rewards[1].KillsRequired, Is.EqualTo(5));
        }

        [Test]
        public void TheEntryFieldIsReadBeforeTheBundledFile()
        {
            var content = Content("Game");
            content.StreaksTuning = Text("GameStreaks", "{ \"rewards\": [{ \"id\": \"airstrike\", \"killsRequired\": 2 }], \"uav\": { \"range\": 40 } }");
            GameContent.Use(content);

            var spec = Load();

            Assert.That(spec.Rewards.Count, Is.EqualTo(1));
            Assert.That(spec.Rewards[0].Id, Is.EqualTo("airstrike"));
            Assert.That(spec.Rewards[0].KillsRequired, Is.EqualTo(2));
            Assert.That(spec.Uav.Range, Is.EqualTo(40f));
            Assert.That(spec.Uav.DurationSeconds, Is.EqualTo(StreakUavSpec.DefaultDurationSeconds), "left-out items keep the defaults");
        }

        [Test]
        public void ABrokenEntryFieldLogsTheItemAndUsesTheDefaults()
        {
            var content = Content("Broken");
            content.StreaksTuning = Text("BrokenStreaks", "{ \"rewards\": [{ \"id\": \"uav\", \"killsRequired\": 0 }] }");
            GameContent.Use(content);
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    @"(?s)^\[Tuning\] streak JSON 'GameContent 'Broken' \(BrokenStreaks\)' item 'rewards\[0\]\.killsRequired' is invalid: .*; using the default values instead$"));

            Assert.That(Load(), Is.SameAs(StreakSpec.Default));
        }

        private static StreakSpec Load()
        {
            return KitTuning.Load(c => c.StreaksTuning, StreakJson.ResourcePath, StreakJson.Parse, StreakSpec.Default);
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
