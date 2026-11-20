using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SkillLogTests
    {
        [Test]
        public void TheConfiguredLineSummarisesTheSpec()
        {
            Assert.That(SkillLog.Configured(SkillSpec.Default), Is.EqualTo("[Skill] configured initial=1000 k=32 min=0 max=3000"));
            Assert.That(SkillLog.Configured(new SkillSpec(1500, 10f, 100, 2000)), Is.EqualTo("[Skill] configured initial=1500 k=10 min=100 max=2000"));
        }

        [Test]
        public void TheRatedLineShowsTheChangeAndTheOpponents()
        {
            Assert.That(
                SkillLog.Rated(MatchOutcome.Won, 1000, 1016, 1000),
                Is.EqualTo("[Skill] rating 1000->1016 outcome=Won opponents=1000"));
            Assert.That(
                SkillLog.Rated(MatchOutcome.Lost, 1200, 1176, 1000),
                Is.EqualTo("[Skill] rating 1200->1176 outcome=Lost opponents=1000"));
        }

        [Test]
        public void TheSaveLinesShowTheRatingOrTheReason()
        {
            Assert.That(SkillLog.Loaded(new SkillSave(1234)), Is.EqualTo("[Skill] loaded rating=1234"));
            Assert.That(SkillLog.Saved(new SkillSave(1176)), Is.EqualTo("[Skill] saved rating=1176"));
            Assert.That(SkillLog.SaveFailed("disk full"), Is.EqualTo("[Skill] could not save the rating: disk full"));
            Assert.That(SkillLog.Rejected("bad"), Is.EqualTo("[Skill] skill save rejected: bad"));
            Assert.That(SkillLog.Kept("skill-rejected"), Is.EqualTo("[Skill] kept the rejected skill save in slot=skill-rejected"));
        }

        [Test]
        public void RejectsMissingValues()
        {
            Assert.Throws<ArgumentNullException>(() => SkillLog.Configured(null));
            Assert.Throws<ArgumentNullException>(() => SkillLog.Loaded(null));
            Assert.Throws<ArgumentNullException>(() => SkillLog.Saved(null));
        }
    }
}
