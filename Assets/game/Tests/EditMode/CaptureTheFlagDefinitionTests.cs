using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// <see cref="CaptureTheFlagDefinition"/> の既定値が Runner と Spec の既定値と揃い、<see cref="CaptureTheFlagDefinition.ToSpec"/> が値をそのまま渡して
    /// 不正な値ではアセット名を添えて止まり、<see cref="CaptureTheFlagDefinition.CopyFrom"/> が Spec の値を写すことを確かめる。
    /// </summary>
    public sealed class CaptureTheFlagDefinitionTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in _objects)
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void ANewDefinitionHasTheDefaults()
        {
            var definition = Create("CaptureTheFlag");

            Assert.That(definition.ScoreLimit, Is.EqualTo(3));
            Assert.That(definition.TimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(definition.RespawnSeconds, Is.EqualTo(5f));
            Assert.That(definition.PickupRadius, Is.EqualTo(1.5f));
            Assert.That(definition.CaptureRadius, Is.EqualTo(2f));
            Assert.That(definition.ReturnSeconds, Is.EqualTo(20f));
            Assert.That(definition.PointsPerCapture, Is.EqualTo(1));
            Assert.That(definition.BotPlanIntervalSeconds, Is.EqualTo(0.5f));
            Assert.That(definition.BotAttackShare, Is.EqualTo(0.5f));
        }

        [Test]
        public void ANewRunnerHasTheSameDefaultsAsANewDefinition()
        {
            var definition = Create("CaptureTheFlag");
            var go = new GameObject("Runner");
            _objects.Add(go);
            var runner = go.AddComponent<CaptureTheFlagRunner>();

            Assert.That(runner.ScoreLimit, Is.EqualTo(definition.ScoreLimit));
            Assert.That(runner.TimeLimitSeconds, Is.EqualTo(definition.TimeLimitSeconds));
            Assert.That(runner.RespawnSeconds, Is.EqualTo(definition.RespawnSeconds));
            Assert.That(runner.PickupRadius, Is.EqualTo(definition.PickupRadius));
            Assert.That(runner.CaptureRadius, Is.EqualTo(definition.CaptureRadius));
            Assert.That(runner.ReturnSeconds, Is.EqualTo(definition.ReturnSeconds));
            Assert.That(runner.PointsPerCapture, Is.EqualTo(definition.PointsPerCapture));
            Assert.That(runner.BotPlanIntervalSeconds, Is.EqualTo(definition.BotPlanIntervalSeconds));
            Assert.That(runner.BotAttackShare, Is.EqualTo(definition.BotAttackShare));
        }

        [Test]
        public void ToSpecPassesTheValuesThrough()
        {
            var definition = Create("QuickFlags");
            definition.ScoreLimit = 5;
            definition.TimeLimitSeconds = 240f;
            definition.RespawnSeconds = 0f;
            definition.PickupRadius = 2.5f;
            definition.CaptureRadius = 3.5f;
            definition.ReturnSeconds = 12f;
            definition.PointsPerCapture = 2;

            var spec = definition.ToSpec();

            Assert.That(spec.ScoreLimit, Is.EqualTo(5));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(240f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(0f));
            Assert.That(spec.PickupRadius, Is.EqualTo(2.5f));
            Assert.That(spec.CaptureRadius, Is.EqualTo(3.5f));
            Assert.That(spec.ReturnSeconds, Is.EqualTo(12f));
            Assert.That(spec.PointsPerCapture, Is.EqualTo(2));
            Assert.That(definition.ToSpec(), Is.Not.SameAs(spec), "each call builds a new spec");
        }

        [TestCase(0, 600f, 5f, 1.5f, 2f, 20f, 1)]
        [TestCase(3, 0f, 5f, 1.5f, 2f, 20f, 1)]
        [TestCase(3, 600f, -1f, 1.5f, 2f, 20f, 1)]
        [TestCase(3, 600f, 5f, 0f, 2f, 20f, 1)]
        [TestCase(3, 600f, 5f, 1.5f, 0f, 20f, 1)]
        [TestCase(3, 600f, 5f, 1.5f, 2f, 0f, 1)]
        [TestCase(3, 600f, 5f, 1.5f, 2f, 20f, 0)]
        public void AnInvalidValueStopsToSpecAndNamesTheAsset(
            int scoreLimit, float timeLimit, float respawn, float pickup, float capture, float returnSeconds, int points)
        {
            var definition = Create("BrokenFlags");
            definition.ScoreLimit = scoreLimit;
            definition.TimeLimitSeconds = timeLimit;
            definition.RespawnSeconds = respawn;
            definition.PickupRadius = pickup;
            definition.CaptureRadius = capture;
            definition.ReturnSeconds = returnSeconds;
            definition.PointsPerCapture = points;

            var error = Assert.Throws<InvalidOperationException>(() => definition.ToSpec());
            Assert.That(error.Message, Does.Contain("BrokenFlags"));
        }

        [TestCase(0f, 0.5f)]
        [TestCase(float.PositiveInfinity, 0.5f)]
        [TestCase(0.5f, -0.1f)]
        [TestCase(0.5f, 1.1f)]
        [TestCase(0.5f, float.NaN)]
        public void AnInvalidBotValueStopsToSpecAndNamesTheAsset(float interval, float share)
        {
            var definition = Create("BrokenBots");
            definition.BotPlanIntervalSeconds = interval;
            definition.BotAttackShare = share;

            var error = Assert.Throws<InvalidOperationException>(() => definition.ToSpec());
            Assert.That(error.Message, Does.Contain("BrokenBots"));
        }

        [Test]
        public void CopyFromTakesTheSpecValuesAndKeepsTheBotValues()
        {
            var definition = Create("Copied");
            definition.BotPlanIntervalSeconds = 2f;
            definition.BotAttackShare = 0.75f;
            var spec = new CaptureTheFlagSpec(7, 420f, 1f, 2.25f, 4f, 9f, 3);

            definition.CopyFrom(spec);

            Assert.That(definition.ScoreLimit, Is.EqualTo(7));
            Assert.That(definition.TimeLimitSeconds, Is.EqualTo(420f));
            Assert.That(definition.RespawnSeconds, Is.EqualTo(1f));
            Assert.That(definition.PickupRadius, Is.EqualTo(2.25f));
            Assert.That(definition.CaptureRadius, Is.EqualTo(4f));
            Assert.That(definition.ReturnSeconds, Is.EqualTo(9f));
            Assert.That(definition.PointsPerCapture, Is.EqualTo(3));
            Assert.That(definition.BotPlanIntervalSeconds, Is.EqualTo(2f));
            Assert.That(definition.BotAttackShare, Is.EqualTo(0.75f));

            var roundTrip = definition.ToSpec();
            Assert.That(roundTrip.ScoreLimit, Is.EqualTo(7));
            Assert.That(roundTrip.ReturnSeconds, Is.EqualTo(9f));
        }

        [Test]
        public void CopyFromRejectsNull()
        {
            var definition = Create("Copied");

            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        private CaptureTheFlagDefinition Create(string name)
        {
            var definition = ScriptableObject.CreateInstance<CaptureTheFlagDefinition>();
            definition.name = name;
            _objects.Add(definition);
            return definition;
        }
    }
}
