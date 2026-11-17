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
    public sealed class KitTuningLoadTests
    {
        private const string MissingPath = "Tuning/__kit_tuning_load_tests_missing";

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
        }

        [Test]
        public void TheEntryFieldIsReadBeforeResources()
        {
            var content = Content("Game");
            content.PingTuning = Text("GamePing", "{ \"lifetimeSeconds\": 12 }");
            GameContent.Use(content);

            var ping = KitTuning.Load(c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing, PingSpec.Default);

            Assert.That(ping.LifetimeSeconds, Is.EqualTo(12f), "Resources/Tuning/ping.json has 6");
        }

        [Test]
        public void AnEmptyEntryFieldFallsBackToResources()
        {
            var ping = KitTuning.Load(c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing, PingSpec.Default);

            Assert.That(ping, Is.Not.SameAs(PingSpec.Default));
            Assert.That(ping.LifetimeSeconds, Is.EqualTo(6f));
            Assert.That(ping.MaxMarkers, Is.EqualTo(3));
        }

        [Test]
        public void NothingInTheEntryOrResourcesUsesTheDefaults()
        {
            var spec = KitTuning.Load(c => c.SuppressionTuning, MissingPath, TuningJson.ParsePing, PingSpec.Default);

            Assert.That(spec, Is.SameAs(PingSpec.Default));
        }

        [Test]
        public void ABrokenEntryFieldLogsTheItemAndUsesTheDefaults()
        {
            var content = Content("Broken");
            content.PingTuning = Text("BrokenPing", "{ \"maxMarkers\": 0 }");
            GameContent.Use(content);
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"(?s)^\[Tuning\] ping JSON 'GameContent 'Broken' \(BrokenPing\)' item 'maxMarkers' is invalid: .*; using the default values instead$"));

            var spec = KitTuning.Load(c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing, PingSpec.Default);

            Assert.That(spec, Is.SameAs(PingSpec.Default), "a broken entry does not fall through to Resources");
        }

        [Test]
        public void TheResultIsKeptPerPathUntilReload()
        {
            var content = Content("Game");
            content.PingTuning = Text("First", "{ \"lifetimeSeconds\": 12 }");
            GameContent.Use(content);

            var first = KitTuning.Load(c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing, PingSpec.Default);
            content.PingTuning = Text("Second", "{ \"lifetimeSeconds\": 20 }");
            var kept = KitTuning.Load(c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing, PingSpec.Default);

            Assert.That(kept, Is.SameAs(first));
            Assert.That(KitTuning.Ping, Is.SameAs(first), "the property shares the cache of its path");

            KitTuning.Reload();
            var reread = KitTuning.Load(c => c.PingTuning, KitTuning.PingResourcePath, TuningJson.ParsePing, PingSpec.Default);

            Assert.That(reread.LifetimeSeconds, Is.EqualTo(20f));
        }

        [Test]
        public void ReadingOnePathAsAnotherTypeIsRejected()
        {
            KitTuning.Load(c => c.SuppressionTuning, MissingPath, TuningJson.ParsePing, PingSpec.Default);

            Assert.Throws<InvalidOperationException>(
                () => KitTuning.Load(c => c.SuppressionTuning, MissingPath, TuningJson.ParseKnockback, KnockbackSpec.Default));
        }

        [Test]
        public void LoadRejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => KitTuning.Load(null, MissingPath, TuningJson.ParsePing, PingSpec.Default));
            Assert.Throws<ArgumentException>(() => KitTuning.Load(c => c.PingTuning, " ", TuningJson.ParsePing, PingSpec.Default));
            Assert.Throws<ArgumentNullException>(() => KitTuning.Load<PingSpec>(c => c.PingTuning, MissingPath, null, PingSpec.Default));
            Assert.Throws<ArgumentNullException>(() => KitTuning.Load(c => c.PingTuning, MissingPath, TuningJson.ParsePing, null));
        }

        [Test]
        public void BotsDefaultToTheNetBotSpecDefaults()
        {
            Assert.That(KitTuning.BotsResourcePath, Is.EqualTo("Tuning/bots"));
            Assert.That(Resources.Load<TextAsset>(KitTuning.BotsResourcePath), Is.Null, "the kit ships no bots tuning");
            Assert.That(KitTuning.Bots, Is.SameAs(NetBotSpec.Default));
        }

        [Test]
        public void BotsAreReadFromTheEntry()
        {
            var content = Content("Game");
            content.BotsTuning = Text("GameBots", "{ \"sightRange\": 33 }");
            GameContent.Use(content);

            var bots = KitTuning.Bots;

            Assert.That(bots.Brain.SightRange, Is.EqualTo(33f));
            Assert.That(bots.Brain.AttackRange, Is.EqualTo(NetBotSpec.DefaultAttackRange));
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
