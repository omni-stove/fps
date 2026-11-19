using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class PayloadSmokeRunTests
    {
        private static readonly float[] _multipliers = { 1f, 1.25f };

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
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
        public void ReadsTheTimeLimitArgument()
        {
            Assert.That(PayloadSmokeRun.ParseSeconds(Array.Empty<string>()), Is.EqualTo(180f));
            Assert.That(PayloadSmokeRun.ParseSeconds(new[] { "-batchmode", "-payloadSmokeSeconds", "240.5" }), Is.EqualTo(240.5f));
            Assert.That(PayloadSmokeRun.ParseSeconds(new[] { "-payloadSmokeSeconds", "30" }), Is.EqualTo(30f));
            Assert.Throws<ArgumentNullException>(() => PayloadSmokeRun.ParseSeconds(null));
        }

        [TestCase(null)]
        [TestCase("abc")]
        [TestCase("29.9")]
        [TestCase("-60")]
        [TestCase("Infinity")]
        [TestCase("NaN")]
        public void RefusesABadTimeLimit(string value)
        {
            var args = value == null
                ? new[] { PayloadSmokeRun.SecondsArgument }
                : new[] { PayloadSmokeRun.SecondsArgument, value };

            Assert.Throws<ArgumentException>(() => PayloadSmokeRun.ParseSeconds(args));
        }

        [Test]
        public void TheTuningJsonReadsBackWithOnlyTheTimeLimitChanged()
        {
            var spec = new PayloadSpec(2.5f, 2, _multipliers, 7f, 0.75f, 45f, 300f);

            var read = PayloadJson.Parse(PayloadSmokeRun.TuningJson(spec, 90f), "test");

            Assert.That(read.CartSpeed, Is.EqualTo(2.5f));
            Assert.That(read.MinPushers, Is.EqualTo(2));
            Assert.That(read.PusherSpeedMultipliers, Is.EqualTo(_multipliers));
            Assert.That(read.RollbackDelaySeconds, Is.EqualTo(7f));
            Assert.That(read.RollbackSpeed, Is.EqualTo(0.75f));
            Assert.That(read.CheckpointBonusSeconds, Is.EqualTo(45f));
            Assert.That(read.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.Throws<ArgumentNullException>(() => PayloadSmokeRun.TuningJson(null, 90f));
        }

        [Test]
        public void ShortenCopiesTheRunnerValuesWithoutADefinition()
        {
            var runner = Track(new GameObject(nameof(PayloadSmokeRunTests))).AddComponent<PayloadRunner>();
            runner.AttackerSideId = 1;
            runner.RespawnSeconds = 3f;
            var fallback = new PayloadSpec(2f, 1, null, 10f, 0.5f, 60f, 300f);

            var shortened = TrackDefinition(PayloadSmokeRun.Shorten(runner, fallback, 60f));

            Assert.That(shortened.name, Is.EqualTo("PayloadDefinition (smoke)"));
            Assert.That(shortened.hideFlags, Is.EqualTo(HideFlags.DontSave));
            Assert.That(shortened.Tuning.hideFlags, Is.EqualTo(HideFlags.DontSave));
            Assert.That(shortened.AttackerSideId, Is.EqualTo(1));
            Assert.That(shortened.RespawnSeconds, Is.EqualTo(3f));

            var spec = shortened.ReadSpec();
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(60f));
            Assert.That(spec.CartSpeed, Is.EqualTo(2f));
            Assert.That(runner.Definition, Is.Null, "the runner is only wired by Run");
        }

        [Test]
        public void ShortenStartsFromTheDefinitionTuningAndLeavesTheSourceAlone()
        {
            var source = Track(ScriptableObject.CreateInstance<PayloadDefinition>());
            source.name = "Payload";
            source.AttackerSideId = 0;
            source.RespawnSeconds = 8f;
            source.Tuning = Track(new TextAsset("{ \"cartSpeed\": 4, \"timeLimitSeconds\": 500 }"));
            var runner = Track(new GameObject(nameof(PayloadSmokeRunTests))).AddComponent<PayloadRunner>();
            runner.Definition = source;

            var shortened = TrackDefinition(PayloadSmokeRun.Shorten(runner, PayloadSpec.Default, 45f));

            Assert.That(shortened, Is.Not.SameAs(source));
            Assert.That(shortened.name, Is.EqualTo("Payload (smoke)"));
            Assert.That(shortened.RespawnSeconds, Is.EqualTo(8f));
            Assert.That(shortened.ReadSpec().CartSpeed, Is.EqualTo(4f));
            Assert.That(shortened.ReadSpec().TimeLimitSeconds, Is.EqualTo(45f));
            Assert.That(source.ReadSpec().TimeLimitSeconds, Is.EqualTo(500f));
        }

        [Test]
        public void ShortenRefusesMissingParts()
        {
            var runner = Track(new GameObject(nameof(PayloadSmokeRunTests))).AddComponent<PayloadRunner>();

            Assert.Throws<ArgumentNullException>(() => PayloadSmokeRun.Shorten(null, PayloadSpec.Default, 60f));
            Assert.Throws<ArgumentNullException>(() => PayloadSmokeRun.Shorten(runner, null, 60f));
        }

        [Test]
        public void ClassifiesThePayloadLinesItWaitsFor()
        {
            var payload = PushedPayload();

            Assert.That(PayloadSmokeRun.Classify(PayloadLog.Motion(payload)), Is.EqualTo(PayloadSmokeRun.LineKind.Pushing));
            Assert.That(PayloadSmokeRun.Classify(PayloadLog.Checkpoint(payload, 0)), Is.EqualTo(PayloadSmokeRun.LineKind.Checkpoint));
            Assert.That(PayloadSmokeRun.Classify(PayloadLog.Ended(payload, Side.Alpha)), Is.EqualTo(PayloadSmokeRun.LineKind.Ended));
            Assert.That(PayloadSmokeRun.Classify(PayloadLog.Ended(payload, null)), Is.EqualTo(PayloadSmokeRun.LineKind.Ended));
        }

        [Test]
        public void IgnoresOtherLines()
        {
            var payload = PushedPayload();
            payload.SetNearCart(1, false);

            Assert.That(PayloadSmokeRun.Classify(null), Is.EqualTo(PayloadSmokeRun.LineKind.Other));
            Assert.That(PayloadSmokeRun.Classify(PayloadLog.Motion(payload)), Is.EqualTo(PayloadSmokeRun.LineKind.Other), "an idle cart is not a push");
            Assert.That(PayloadSmokeRun.Classify(PayloadLog.Started(payload)), Is.EqualTo(PayloadSmokeRun.LineKind.Other));
            Assert.That(PayloadSmokeRun.Classify(ModeLog.Ended(Side.Alpha)), Is.EqualTo(PayloadSmokeRun.LineKind.Other));
            Assert.That(PayloadSmokeRun.Classify("[PayloadSmokeRun] [Mode] payload ended winner=0"), Is.EqualTo(PayloadSmokeRun.LineKind.Other));
        }

        private static Payload PushedPayload()
        {
            var points = new[]
            {
                System.Numerics.Vector3.Zero,
                new System.Numerics.Vector3(0f, 0f, 10f),
                new System.Numerics.Vector3(0f, 0f, 20f),
            };
            var payload = new Payload(new PayloadSpec(2f, 1, null, 10f, 0.5f, 60f, 300f), new PayloadRoute(points, new[] { 1 }), Side.Alpha, 5f);
            payload.Session.AddPlayer(1, Side.Alpha);
            payload.Session.AddPlayer(2, Side.Bravo);
            payload.Session.Start();
            payload.SetNearCart(1, true);
            payload.Session.Tick(1f);
            return payload;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private PayloadDefinition TrackDefinition(PayloadDefinition definition)
        {
            Track(definition.Tuning);
            return Track(definition);
        }
    }
}
