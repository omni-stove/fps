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
    public sealed class InfectedSmokeRunTests
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
        public void ReadsTheRoundLengthArgument()
        {
            Assert.That(InfectedSmokeRun.ParseSeconds(Array.Empty<string>()), Is.EqualTo(40f));
            Assert.That(InfectedSmokeRun.ParseSeconds(new[] { "-batchmode", "-infectedSmokeSeconds", "75.5" }), Is.EqualTo(75.5f));
            Assert.That(InfectedSmokeRun.ParseSeconds(new[] { "-infectedSmokeSeconds", "10" }), Is.EqualTo(10f));
        }

        [TestCase(null)]
        [TestCase("abc")]
        [TestCase("9.9")]
        [TestCase("-30")]
        [TestCase("Infinity")]
        [TestCase("NaN")]
        public void RefusesABadRoundLength(string value)
        {
            var args = value == null
                ? new[] { InfectedSmokeRun.SecondsArgument }
                : new[] { InfectedSmokeRun.SecondsArgument, value };

            Assert.Throws<ArgumentException>(() => InfectedSmokeRun.ParseSeconds(args));
        }

        [Test]
        public void RefusesNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => InfectedSmokeRun.ParseSeconds(null));
        }

        [Test]
        public void ShortenCopiesTheDefinitionAndChangesOnlyTheTimes()
        {
            var source = Track(ScriptableObject.CreateInstance<InfectedDefinition>());
            source.name = "Infected";
            source.CopyFrom(InfectedSpec.Default);
            source.InfectedMaxHealth = 220;
            source.InfectedRespawnSeconds = 4f;

            var shortened = Track(InfectedSmokeRun.Shorten(source, 30f));

            Assert.That(shortened, Is.Not.SameAs(source));
            Assert.That(shortened.name, Is.EqualTo("Infected (smoke)"));
            Assert.That(shortened.hideFlags, Is.EqualTo(HideFlags.DontSave));
            Assert.That(shortened.StartDelaySeconds, Is.EqualTo(2f));
            Assert.That(shortened.RoundSeconds, Is.EqualTo(30f));
            Assert.That(shortened.InfectedMaxHealth, Is.EqualTo(220));
            Assert.That(shortened.InfectedRespawnSeconds, Is.EqualTo(4f));
            Assert.That(source.StartDelaySeconds, Is.EqualTo(InfectedSpec.DefaultStartDelaySeconds));
            Assert.That(source.RoundSeconds, Is.EqualTo(InfectedSpec.DefaultRoundSeconds));
        }

        [Test]
        public void ShortenStartsFromTheDefaultsWithoutADefinition()
        {
            var shortened = Track(InfectedSmokeRun.Shorten(null, 40f));

            Assert.That(shortened.name, Is.EqualTo("InfectedDefinition (smoke)"));
            Assert.That(shortened.StartDelaySeconds, Is.EqualTo(2f));
            Assert.That(shortened.RoundSeconds, Is.EqualTo(40f));
            Assert.That(shortened.InfectedMaxHealth, Is.EqualTo(InfectedSpec.DefaultInfectedMaxHealth));
        }

        [Test]
        public void ShortenRefusesAnInvalidDefinition()
        {
            var source = Track(ScriptableObject.CreateInstance<InfectedDefinition>());
            source.CopyFrom(InfectedSpec.Default);
            source.InfectedMaxHealth = 0;

            Assert.Throws<InvalidOperationException>(() => InfectedSmokeRun.Shorten(source, 40f));
        }

        [Test]
        public void ClassifiesTheModeLinesItWaitsFor()
        {
            Assert.That(InfectedSmokeRun.Classify(InfectedLog.Outbreak(new[] { 3 })), Is.EqualTo(InfectedSmokeRun.LineKind.Outbreak));
            Assert.That(InfectedSmokeRun.Classify("[Mode] infected turn player=3 by=first"), Is.EqualTo(InfectedSmokeRun.LineKind.FirstTurn));
            Assert.That(InfectedSmokeRun.Classify("[Mode] infected turn player=1 by=3"), Is.EqualTo(InfectedSmokeRun.LineKind.Turn));
            Assert.That(InfectedSmokeRun.Classify(InfectedLog.Equip(3, "handgun")), Is.EqualTo(InfectedSmokeRun.LineKind.Equip));
            Assert.That(InfectedSmokeRun.Classify(ModeLog.Ended(Infected.SurvivorSide)), Is.EqualTo(InfectedSmokeRun.LineKind.Ended));
            Assert.That(InfectedSmokeRun.Classify(ModeLog.Ended(null)), Is.EqualTo(InfectedSmokeRun.LineKind.Ended));
        }

        [Test]
        public void IgnoresOtherLines()
        {
            Assert.That(InfectedSmokeRun.Classify(null), Is.EqualTo(InfectedSmokeRun.LineKind.Other));
            Assert.That(InfectedSmokeRun.Classify(ModeLog.Restart()), Is.EqualTo(InfectedSmokeRun.LineKind.Other));
            Assert.That(
                InfectedSmokeRun.Classify("[InfectedSmokeRun] [Mode] infected outbreak first=3"), Is.EqualTo(InfectedSmokeRun.LineKind.Other));
        }

        [Test]
        public void EitherSideWinningDecidesTheRoundButADrawDoesNot()
        {
            Assert.That(InfectedSmokeRun.IsDecided(ModeLog.Ended(Infected.SurvivorSide)), Is.True);
            Assert.That(InfectedSmokeRun.IsDecided(ModeLog.Ended(Infected.InfectedSide)), Is.True);
            Assert.That(InfectedSmokeRun.IsDecided(ModeLog.Ended(null)), Is.False);
            Assert.That(InfectedSmokeRun.IsDecided(ModeLog.Ended(new Side(5))), Is.False);
            Assert.That(InfectedSmokeRun.IsDecided(null), Is.False);
        }
    }
}
