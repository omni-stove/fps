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
    public sealed class CaptureTheFlagSmokeRunTests
    {
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

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        [Test]
        public void ReadsTheTimeLimitArgument()
        {
            Assert.That(CaptureTheFlagSmokeRun.ParseSeconds(Array.Empty<string>()), Is.EqualTo(180f));
            Assert.That(
                CaptureTheFlagSmokeRun.ParseSeconds(new[] { "-batchmode", "-captureTheFlagSmokeSeconds", "240.5" }), Is.EqualTo(240.5f));
            Assert.That(CaptureTheFlagSmokeRun.ParseSeconds(new[] { "-captureTheFlagSmokeSeconds", "30" }), Is.EqualTo(30f));
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
                ? new[] { CaptureTheFlagSmokeRun.SecondsArgument }
                : new[] { CaptureTheFlagSmokeRun.SecondsArgument, value };

            Assert.Throws<ArgumentException>(() => CaptureTheFlagSmokeRun.ParseSeconds(args));
        }

        [Test]
        public void RefusesNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => CaptureTheFlagSmokeRun.ParseSeconds(null));
        }

        [Test]
        public void ShortenCopiesTheDefinitionAndChangesOnlyTheTimeLimit()
        {
            var source = Track(ScriptableObject.CreateInstance<CaptureTheFlagDefinition>());
            source.name = "CaptureTheFlag";
            source.ScoreLimit = 5;
            source.ReturnSeconds = 12f;
            source.BotAttackShare = 0.25f;

            var shortened = Track(CaptureTheFlagSmokeRun.Shorten(source, 90f));

            Assert.That(shortened, Is.Not.SameAs(source));
            Assert.That(shortened.name, Is.EqualTo("CaptureTheFlag (smoke)"));
            Assert.That(shortened.hideFlags, Is.EqualTo(HideFlags.DontSave));
            Assert.That(shortened.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(shortened.ScoreLimit, Is.EqualTo(5));
            Assert.That(shortened.ReturnSeconds, Is.EqualTo(12f));
            Assert.That(shortened.BotAttackShare, Is.EqualTo(0.25f));
            Assert.That(source.TimeLimitSeconds, Is.EqualTo(CaptureTheFlagSpec.DefaultTimeLimitSeconds));
        }

        [Test]
        public void ShortenStartsFromTheDefaultsWithoutADefinition()
        {
            var shortened = Track(CaptureTheFlagSmokeRun.Shorten(null, 60f));

            Assert.That(shortened.TimeLimitSeconds, Is.EqualTo(60f));
            Assert.That(shortened.ScoreLimit, Is.EqualTo(CaptureTheFlagSpec.DefaultScoreLimit));
            Assert.That(shortened.name, Is.EqualTo("CaptureTheFlagDefinition (smoke)"));
        }

        [Test]
        public void ShortenRefusesAnInvalidDefinition()
        {
            var source = Track(ScriptableObject.CreateInstance<CaptureTheFlagDefinition>());
            source.ScoreLimit = 0;

            Assert.Throws<InvalidOperationException>(() => CaptureTheFlagSmokeRun.Shorten(source, 60f));
        }

        [Test]
        public void ClassifiesTheModeLinesItWaitsFor()
        {
            Assert.That(
                CaptureTheFlagSmokeRun.Classify(CaptureTheFlagLog.Taken(Side.Bravo, 2)), Is.EqualTo(CaptureTheFlagSmokeRun.LineKind.Taken));
            Assert.That(
                CaptureTheFlagSmokeRun.Classify(CaptureTheFlagLog.Captured(Side.Bravo, 2, Side.Alpha, 1d)),
                Is.EqualTo(CaptureTheFlagSmokeRun.LineKind.Captured));
            Assert.That(CaptureTheFlagSmokeRun.Classify(ModeLog.Ended(Side.Alpha)), Is.EqualTo(CaptureTheFlagSmokeRun.LineKind.Ended));
            Assert.That(CaptureTheFlagSmokeRun.Classify(ModeLog.Ended(null)), Is.EqualTo(CaptureTheFlagSmokeRun.LineKind.Ended));
        }

        [Test]
        public void IgnoresOtherLines()
        {
            Assert.That(CaptureTheFlagSmokeRun.Classify(null), Is.EqualTo(CaptureTheFlagSmokeRun.LineKind.Other));
            Assert.That(
                CaptureTheFlagSmokeRun.Classify(CaptureTheFlagLog.Dropped(Side.Alpha, 4)), Is.EqualTo(CaptureTheFlagSmokeRun.LineKind.Other));
            Assert.That(
                CaptureTheFlagSmokeRun.Classify(CaptureTheFlagLog.Returned(Side.Alpha, null)), Is.EqualTo(CaptureTheFlagSmokeRun.LineKind.Other));
            Assert.That(CaptureTheFlagSmokeRun.Classify(ModeLog.Restart()), Is.EqualTo(CaptureTheFlagSmokeRun.LineKind.Other));
            Assert.That(
                CaptureTheFlagSmokeRun.Classify("[CaptureTheFlagSmokeRun] [Mode] flag-taken flag=1 player=0"),
                Is.EqualTo(CaptureTheFlagSmokeRun.LineKind.Other));
        }
    }
}
