using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class SquadOrderJsonTests
    {
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            // プロジェクトの Resources/FpsKit に置かれた入口の欄に左右されないよう、空の入口に替えておく。
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
            GameSignals.ResetAll();
        }

        [Test]
        public void EveryItemIsRead()
        {
            var spec = SquadOrderJson.Parse(
                "{ \"holdSeconds\": 1.5, \"maxDistance\": 80, \"enemyAimToleranceDegrees\": 5, "
                + "\"nearbyRadius\": 20, \"followDistance\": 4, \"leashDistance\": 25 }",
                "squad.json");

            Assert.That(spec.HoldSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.MaxDistance, Is.EqualTo(80f));
            Assert.That(spec.EnemyAimToleranceDegrees, Is.EqualTo(5f));
            Assert.That(spec.NearbyRadius, Is.EqualTo(20f));
            Assert.That(spec.FollowDistance, Is.EqualTo(4f));
            Assert.That(spec.LeashDistance, Is.EqualTo(25f));
            Assert.That(spec.Aim.MaxDistance, Is.EqualTo(80f));
            Assert.That(spec.Aim.EnemyAimToleranceDegrees, Is.EqualTo(5f));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = SquadOrderJson.Parse("{ \"holdSeconds\": 1.5 }", "squad.json");

            Assert.That(spec.HoldSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.MaxDistance, Is.EqualTo(150f));
            Assert.That(spec.EnemyAimToleranceDegrees, Is.EqualTo(3f));
            Assert.That(spec.NearbyRadius, Is.EqualTo(30f));
            Assert.That(spec.FollowDistance, Is.EqualTo(3f));
            Assert.That(spec.LeashDistance, Is.EqualTo(15f));
        }

        [Test]
        public void AnEmptyObjectIsTheDefaults()
        {
            var spec = SquadOrderJson.Parse("{}", "squad.json");

            Assert.That(spec.HoldSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.MaxDistance, Is.EqualTo(150f));
            Assert.That(spec.EnemyAimToleranceDegrees, Is.EqualTo(3f));
            Assert.That(spec.NearbyRadius, Is.EqualTo(30f));
            Assert.That(spec.FollowDistance, Is.EqualTo(3f));
            Assert.That(spec.LeashDistance, Is.EqualTo(15f));
        }

        [Test]
        public void TheItemNamesAreTheSpecArguments()
        {
            Assert.That(
                SquadOrderJson.ItemNames,
                Is.EqualTo(new[] { "holdSeconds", "maxDistance", "enemyAimToleranceDegrees", "nearbyRadius", "followDistance", "leashDistance" }));
        }

        [Test]
        public void AnOutOfRangeValueNamesItsItem()
        {
            var e = Assert.Throws<TuningJsonException>(() => SquadOrderJson.Parse("{ \"holdSeconds\": 0 }", "squad.json"));

            Assert.That(e.Item, Is.EqualTo("holdSeconds"));
            Assert.That(e.Message, Does.StartWith("squad JSON 'squad.json' item 'holdSeconds' is invalid: "));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ALeashShorterThanTheFollowDistanceNamesTheLeash()
        {
            var e = Assert.Throws<TuningJsonException>(
                () => SquadOrderJson.Parse("{ \"followDistance\": 10, \"leashDistance\": 5 }", "squad.json"));

            Assert.That(e.Item, Is.EqualTo("leashDistance"));
        }

        [Test]
        public void AnAimToleranceOfNinetyDegreesIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(() => SquadOrderJson.Parse("{ \"enemyAimToleranceDegrees\": 90 }", "squad.json"));

            Assert.That(e.Item, Is.EqualTo("enemyAimToleranceDegrees"));
        }

        [Test]
        public void AnUnknownItemIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(() => SquadOrderJson.Parse("{ \"holdSecond\": 1 }", "squad.json"));

            Assert.That(e.Item, Is.EqualTo("holdSecond"));
            Assert.That(e.Message, Does.Contain("unknown item 'holdSecond'"));
        }

        [Test]
        public void AWrongTypeIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(() => SquadOrderJson.Parse("{ \"nearbyRadius\": \"far\" }", "squad.json"));

            Assert.That(e.Item, Is.EqualTo("nearbyRadius"));
        }

        [Test]
        public void BrokenJsonIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(() => SquadOrderJson.Parse("{ \"holdSeconds\": ", "squad.json"));

            Assert.That(e.Message, Does.StartWith("squad JSON 'squad.json' is not valid JSON: "));
        }

        [Test]
        public void ANullTextIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => SquadOrderJson.Parse(null, "squad.json"));
        }

        [Test]
        public void AnUnnamedSourceIsNamedByItsKind()
        {
            var e = Assert.Throws<TuningJsonException>(() => SquadOrderJson.Parse("[]", null));

            Assert.That(e.Message, Does.Contain("(unnamed squad JSON)"));
        }

        [Test]
        public void TheShippedFileHasTheDefaults()
        {
            Assert.That(SquadCommandActionInstaller.TuningResourcePath, Is.EqualTo("Tuning/squad"));
            var asset = Resources.Load<TextAsset>(SquadCommandActionInstaller.TuningResourcePath);
            Assert.That(asset, Is.Not.Null, "Resources/Tuning/squad.json ships with the kit");

            var spec = SquadOrderJson.Parse(asset.text, "Resources/Tuning/squad.json");
            Resources.UnloadAsset(asset);

            Assert.That(spec.HoldSeconds, Is.EqualTo(SquadOrderSpec.Default.HoldSeconds));
            Assert.That(spec.MaxDistance, Is.EqualTo(SquadOrderSpec.Default.MaxDistance));
            Assert.That(spec.EnemyAimToleranceDegrees, Is.EqualTo(SquadOrderSpec.Default.EnemyAimToleranceDegrees));
            Assert.That(spec.NearbyRadius, Is.EqualTo(SquadOrderSpec.Default.NearbyRadius));
            Assert.That(spec.FollowDistance, Is.EqualTo(SquadOrderSpec.Default.FollowDistance));
            Assert.That(spec.LeashDistance, Is.EqualTo(SquadOrderSpec.Default.LeashDistance));
        }

        [Test]
        public void TheInstallerAppliesTheEntryFieldToTheController()
        {
            var content = Content("Game");
            content.SquadTuning = Text("GameSquad", "{ \"holdSeconds\": 1.5, \"nearbyRadius\": 12 }");
            GameContent.Use(content);
            var player = Player();

            new SquadCommandActionInstaller().Install(player, GameRules.Default);

            var controller = player.GetComponent<SquadCommandController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsConfigured, Is.True);
            Assert.That(controller.Spec.HoldSeconds, Is.EqualTo(1.5f));
            Assert.That(controller.Spec.NearbyRadius, Is.EqualTo(12f));
            Assert.That(controller.Spec.LeashDistance, Is.EqualTo(15f));
            Assert.That(controller.CreateSpec().HoldSeconds, Is.EqualTo(1.5f), "the value is written to the serialized fields");
        }

        [Test]
        public void TheInstallerFallsBackToResourcesWhenTheEntryIsEmpty()
        {
            var player = Player();
            var controller = player.AddComponent<SquadCommandController>();
            controller.ApplySpec(new SquadOrderSpec(holdSeconds: 2f, nearbyRadius: 5f));

            new SquadCommandActionInstaller().Install(player, GameRules.Default);

            Assert.That(controller.Spec.HoldSeconds, Is.EqualTo(0.5f), "Resources/Tuning/squad.json has 0.5");
            Assert.That(controller.Spec.NearbyRadius, Is.EqualTo(30f));
        }

        [Test]
        public void ABrokenEntryFieldLogsTheItemAndInstallsTheDefaults()
        {
            var content = Content("Broken");
            content.SquadTuning = Text("BrokenSquad", "{ \"holdSeconds\": -1 }");
            GameContent.Use(content);
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"(?s)^\[Tuning\] squad JSON 'GameContent 'Broken' \(BrokenSquad\)' item 'holdSeconds' is invalid: .*; using the default values instead$"));
            var player = Player();

            new SquadCommandActionInstaller().Install(player, GameRules.Default);

            var controller = player.GetComponent<SquadCommandController>();
            Assert.That(controller.Spec.HoldSeconds, Is.EqualTo(SquadOrderSpec.DefaultHoldSeconds));
            Assert.That(SquadCommandActionInstaller.Tuning, Is.SameAs(SquadOrderSpec.Default));
        }

        private GameObject Player()
        {
            var player = new GameObject("Player");
            _created.Add(player);
            return player;
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
