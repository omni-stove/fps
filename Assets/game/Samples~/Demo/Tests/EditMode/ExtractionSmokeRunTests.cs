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
    public sealed class ExtractionSmokeRunTests
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
        public void ReadsTheRaidLengthArgument()
        {
            Assert.That(ExtractionSmokeRun.ParseSeconds(Array.Empty<string>()), Is.EqualTo(60f));
            Assert.That(ExtractionSmokeRun.ParseSeconds(new[] { "-batchmode", "-extractionSmokeSeconds", "90.5" }), Is.EqualTo(90.5f));
            Assert.That(ExtractionSmokeRun.ParseSeconds(new[] { "-extractionSmokeSeconds", "9" }), Is.EqualTo(9f));
        }

        [TestCase(null)]
        [TestCase("abc")]
        [TestCase("8.9")]
        [TestCase("-30")]
        [TestCase("Infinity")]
        [TestCase("NaN")]
        public void RefusesABadRaidLength(string value)
        {
            var args = value == null
                ? new[] { ExtractionSmokeRun.SecondsArgument }
                : new[] { ExtractionSmokeRun.SecondsArgument, value };

            Assert.Throws<ArgumentException>(() => ExtractionSmokeRun.ParseSeconds(args));
        }

        [Test]
        public void RefusesNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => ExtractionSmokeRun.ParseSeconds(null));
        }

        [Test]
        public void ShortenCopiesTheDefinitionAndChangesOnlyTheTimes()
        {
            var source = Track(ScriptableObject.CreateInstance<ExtractionDefinition>());
            source.name = "Extraction";
            source.CopyFrom(new ExtractionSpec(600f, 5f, 3, 30f, true));

            var shortened = Track(ExtractionSmokeRun.Shorten(source, 45f));

            Assert.That(shortened, Is.Not.SameAs(source));
            Assert.That(shortened.name, Is.EqualTo("Extraction (smoke)"));
            Assert.That(shortened.hideFlags, Is.EqualTo(HideFlags.DontSave));
            Assert.That(shortened.RaidSeconds, Is.EqualTo(45f));
            Assert.That(shortened.OpenAfterSeconds, Is.EqualTo(2f));
            Assert.That(shortened.ExtractHoldSeconds, Is.EqualTo(2f));
            Assert.That(shortened.OpenPoints, Is.EqualTo(3));
            Assert.That(shortened.KeepCarriedOnDeath, Is.True);
            Assert.That(source.RaidSeconds, Is.EqualTo(600f));
            Assert.That(source.OpenAfterSeconds, Is.EqualTo(30f));
            Assert.That(source.ExtractHoldSeconds, Is.EqualTo(5f));
        }

        [Test]
        public void ShortenStartsFromTheDemoSpecWithoutADefinition()
        {
            var shortened = Track(ExtractionSmokeRun.Shorten(null, 60f));

            Assert.That(shortened.name, Is.EqualTo("ExtractionDefinition (smoke)"));
            Assert.That(shortened.RaidSeconds, Is.EqualTo(60f));
            Assert.That(shortened.OpenPoints, Is.EqualTo(ExtractionSceneBuilder.OpenPoints));
            Assert.That(shortened.OpenAfterSeconds, Is.EqualTo(2f));
            Assert.That(shortened.ExtractHoldSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void ShortenRefusesAnInvalidDefinition()
        {
            var source = Track(ScriptableObject.CreateInstance<ExtractionDefinition>());
            source.CopyFrom(ExtractionSceneBuilder.DemoSpec());
            source.OpenPoints = 0;

            Assert.Throws<InvalidOperationException>(() => ExtractionSmokeRun.Shorten(source, 60f));
        }

        [Test]
        public void ClassifiesTheRaidLinesItWaitsFor()
        {
            Assert.That(
                ExtractionSmokeRun.Classify("[Extraction] started points=east,far raid=60.0s open-after=2.0s hold=2.0s brought=-"),
                Is.EqualTo(ExtractionSmokeRun.LineKind.Started));
            Assert.That(
                ExtractionSmokeRun.Classify(LootLog.Opened("Crate-1", "demo-supplies", new[] { new ItemCount("bandage", 2) })),
                Is.EqualTo(ExtractionSmokeRun.LineKind.Opened));
            Assert.That(
                ExtractionSmokeRun.Classify("[Extraction] ended outcome=Extracted at=6.1s stored=bandagex2 lost=-"),
                Is.EqualTo(ExtractionSmokeRun.LineKind.Extracted));
            Assert.That(
                ExtractionSmokeRun.Classify("[Extraction] ended outcome=Died at=4.0s stored=- lost=bandagex2"),
                Is.EqualTo(ExtractionSmokeRun.LineKind.RaidEnded));
            Assert.That(ExtractionSmokeRun.Classify(ModeLog.Ended(Side.Alpha)), Is.EqualTo(ExtractionSmokeRun.LineKind.ModeEnded));
        }

        [Test]
        public void IgnoresOtherLines()
        {
            Assert.That(ExtractionSmokeRun.Classify(null), Is.EqualTo(ExtractionSmokeRun.LineKind.Other));
            Assert.That(
                ExtractionSmokeRun.Classify(LootLog.UnknownTable("Crate-1", "missing")), Is.EqualTo(ExtractionSmokeRun.LineKind.Other));
            Assert.That(ExtractionSmokeRun.Classify(ModeLog.Restart()), Is.EqualTo(ExtractionSmokeRun.LineKind.Other));
            Assert.That(
                ExtractionSmokeRun.Classify("[ExtractionSmokeRun] [Extraction] ended outcome=Extracted at=6.1s stored=- lost=-"),
                Is.EqualTo(ExtractionSmokeRun.LineKind.Other));
        }
    }
}
