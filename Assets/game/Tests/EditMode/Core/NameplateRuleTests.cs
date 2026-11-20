using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NameplateRuleTests
    {
        private static readonly NameplateSpec _spec = NameplateSpec.Default;

        [TestCase(0f, false, true)]
        [TestCase(NameplateSpec.DefaultFriendlyRange, false, true)]
        [TestCase(NameplateSpec.DefaultFriendlyRange + 0.1f, true, false)]
        public void FriendsAreShownWithinRangeEvenBehindWalls(float distance, bool inSight, bool expected)
        {
            Assert.That(NameplateRule.ShouldShow(_spec, TeamRelation.Friendly, distance, inSight, true), Is.EqualTo(expected));
        }

        // 敵は壁越しに出すと位置が透けるので、見えているときだけ
        [TestCase(10f, true, true)]
        [TestCase(NameplateSpec.DefaultEnemyRange, true, true)]
        [TestCase(10f, false, false)]
        [TestCase(NameplateSpec.DefaultEnemyRange + 0.1f, true, false)]
        public void EnemiesAreShownOnlyInSightAndWithinRange(float distance, bool inSight, bool expected)
        {
            Assert.That(NameplateRule.ShouldShow(_spec, TeamRelation.Enemy, distance, inSight, true), Is.EqualTo(expected));
        }

        [Test]
        public void TheRangesComeFromTheSpec()
        {
            var spec = new NameplateSpec(friendlyRange: 20f, enemyRange: 80f);

            Assert.That(NameplateRule.ShouldShow(spec, TeamRelation.Friendly, 20f, false, true), Is.True);
            Assert.That(NameplateRule.ShouldShow(spec, TeamRelation.Friendly, 20.1f, false, true), Is.False);
            Assert.That(NameplateRule.ShouldShow(spec, TeamRelation.Enemy, 80f, true, true), Is.True);
            Assert.That(NameplateRule.ShouldShow(spec, TeamRelation.Enemy, 80.1f, true, true), Is.False);
        }

        [Test]
        public void AZeroRangeShowsNoneOfThatRelationEvenAtZeroDistance()
        {
            var spec = new NameplateSpec(friendlyRange: 0f, enemyRange: 0f);

            Assert.That(NameplateRule.ShouldShow(spec, TeamRelation.Friendly, 0f, true, true), Is.False);
            Assert.That(NameplateRule.ShouldShow(spec, TeamRelation.Enemy, 0f, true, true), Is.False);
        }

        [Test]
        public void TheDefaultEnemyRangeIsShorterThanTheDefaultFriendlyRange()
        {
            Assert.That(NameplateSpec.DefaultFriendlyRange, Is.EqualTo(60f));
            Assert.That(NameplateSpec.DefaultEnemyRange, Is.EqualTo(35f));
            Assert.That(NameplateSpec.Default.FriendlyRange, Is.EqualTo(NameplateSpec.DefaultFriendlyRange));
            Assert.That(NameplateSpec.Default.EnemyRange, Is.EqualTo(NameplateSpec.DefaultEnemyRange));
            Assert.That(NameplateSpec.DefaultEnemyRange, Is.LessThan(NameplateSpec.DefaultFriendlyRange));
        }

        [TestCase(TeamRelation.Friendly)]
        [TestCase(TeamRelation.Enemy)]
        public void TheDeadAreNotShown(TeamRelation relation)
        {
            Assert.That(NameplateRule.ShouldShow(_spec, relation, 1f, true, false), Is.False);
        }

        [Test]
        public void NeutralIsNotShown()
        {
            Assert.That(NameplateRule.ShouldShow(_spec, TeamRelation.Neutral, 1f, true, true), Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDistancesAreNotShown(float distance)
        {
            Assert.That(NameplateRule.ShouldShow(_spec, TeamRelation.Friendly, distance, true, true), Is.False);
        }

        [Test]
        public void RejectsUnknownRelationsAndAMissingSpec()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NameplateRule.ShouldShow(_spec, (TeamRelation)3, 1f, true, true));
            Assert.Throws<ArgumentNullException>(() => NameplateRule.ShouldShow(null, TeamRelation.Friendly, 1f, true, true));
        }

        [TestCase(-1f, 35f)]
        [TestCase(60f, float.NaN)]
        [TestCase(float.PositiveInfinity, 35f)]
        public void TheSpecRejectsNegativeOrNonFiniteRanges(float friendlyRange, float enemyRange)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NameplateSpec(friendlyRange, enemyRange));
        }

        [Test]
        public void TheLogLinesNameTheRangesAndTheTarget()
        {
            Assert.That(
                NameplateLog.View("NameplateView", new NameplateSpec(60f, 35f)),
                Is.EqualTo("[Nameplate] view=NameplateView friendlyRange=60.0 enemyRange=35.0"));
            Assert.That(NameplateLog.Shown(7, TeamRelation.Enemy, 12.34f), Is.EqualTo("[Nameplate] shown id=7 relation=Enemy distance=12.3"));
            Assert.That(NameplateLog.Hidden(7), Is.EqualTo("[Nameplate] hidden id=7"));
            Assert.That(NameplateLog.Source(" SignalNameplateTargets "), Is.EqualTo("[Nameplate] source=SignalNameplateTargets"));
            Assert.That(NameplateLog.Source(null), Is.EqualTo("[Nameplate] source=none"));
            Assert.Throws<ArgumentNullException>(() => NameplateLog.View("NameplateView", null));
        }
    }
}
