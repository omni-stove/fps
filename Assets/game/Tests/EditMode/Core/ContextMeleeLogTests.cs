using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ContextMeleeLogTests
    {
        [Test]
        public void LinesAreTaggedKeyValuePairs()
        {
            Assert.That(ContextMeleeLog.Started("p1", "e1", "finisher"), Is.EqualTo("[ContextMelee] started id=p1 target=e1 move=finisher"));
            Assert.That(
                ContextMeleeLog.Resolved("p1", "e1", "finisher", ContextMeleeOutcome.Kill, 975, true),
                Is.EqualTo("[ContextMelee] resolved id=p1 target=e1 move=finisher outcome=Kill dmg=975 killed=true"));
            Assert.That(ContextMeleeLog.Reward("p1", "finisher", 25, 30), Is.EqualTo("[ContextMelee] reward id=p1 move=finisher health=25 ammo=30"));
            Assert.That(
                ContextMeleeLog.Interrupted("p1", "e1", "finisher", ContextMeleeInterruption.Shot),
                Is.EqualTo("[ContextMelee] interrupted id=p1 target=e1 move=finisher reason=Shot"));
            Assert.That(
                ContextMeleeLog.NoMove("p1", "e1", ContextMeleeConditions.TargetBehind | ContextMeleeConditions.SelfGrounded),
                Is.EqualTo("[ContextMelee] none id=p1 target=e1 conditions=TargetBehind|SelfGrounded"));
        }

        [Test]
        public void PrefixesStartTheirLines()
        {
            Assert.That(
                ContextMeleeLog.Resolved("p1", "e1", "finisher", ContextMeleeOutcome.Kill, 1, true),
                Does.StartWith(ContextMeleeLog.ResolvedPrefix("p1", "e1", "finisher")));
            Assert.That(ContextMeleeLog.Reward("p1", "finisher", 0, 0), Does.StartWith(ContextMeleeLog.RewardPrefix("p1", "finisher")));
            Assert.That(
                ContextMeleeLog.Resolved("p1", "e10", "finisher", ContextMeleeOutcome.Kill, 1, true),
                Does.Not.StartWith(ContextMeleeLog.ResolvedPrefix("p1", "e1", "finisher")));
        }

        [Test]
        public void ConditionsAreJoinedWithoutSpaces()
        {
            Assert.That(ContextMeleeLog.FormatConditions(ContextMeleeConditions.None), Is.EqualTo("None"));
            Assert.That(ContextMeleeLog.FormatConditions(ContextMeleeConditions.TargetStaggered), Is.EqualTo("TargetStaggered"));
            Assert.That(ContextMeleeLog.FormatConditions((ContextMeleeConditions)(1 << 20) | ContextMeleeConditions.TargetDowned), Is.EqualTo("TargetDowned|1048576"));
        }

        [Test]
        public void AnUndefinedInterruptionIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ContextMeleeLog.Interrupted("p1", "e1", "m", (ContextMeleeInterruption)99));
        }
    }
}
