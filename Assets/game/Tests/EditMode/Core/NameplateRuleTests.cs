using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NameplateRuleTests
    {
        // 味方は壁越しでも出す
        [TestCase(0f, false, true)]
        [TestCase(NameplateRule.FriendlyRange, false, true)]
        [TestCase(NameplateRule.FriendlyRange + 0.1f, true, false)]
        public void FriendsAreShownWithinRangeEvenBehindWalls(float distance, bool inSight, bool expected)
        {
            Assert.That(NameplateRule.ShouldShow(TeamRelation.Friendly, distance, inSight, true), Is.EqualTo(expected));
        }

        // 敵は壁越しに出すと位置が透けるので、見えているときだけ
        [TestCase(10f, true, true)]
        [TestCase(NameplateRule.EnemyRange, true, true)]
        [TestCase(10f, false, false)]
        [TestCase(NameplateRule.EnemyRange + 0.1f, true, false)]
        public void EnemiesAreShownOnlyInSightAndWithinRange(float distance, bool inSight, bool expected)
        {
            Assert.That(NameplateRule.ShouldShow(TeamRelation.Enemy, distance, inSight, true), Is.EqualTo(expected));
        }

        [Test]
        public void TheEnemyRangeIsShorterThanTheFriendlyRange()
        {
            Assert.That(NameplateRule.EnemyRange, Is.LessThan(NameplateRule.FriendlyRange));
        }

        [TestCase(TeamRelation.Friendly)]
        [TestCase(TeamRelation.Enemy)]
        public void TheDeadAreNotShown(TeamRelation relation)
        {
            Assert.That(NameplateRule.ShouldShow(relation, 1f, true, false), Is.False);
        }

        [Test]
        public void NeutralIsNotShown()
        {
            Assert.That(NameplateRule.ShouldShow(TeamRelation.Neutral, 1f, true, true), Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDistancesAreNotShown(float distance)
        {
            Assert.That(NameplateRule.ShouldShow(TeamRelation.Friendly, distance, true, true), Is.False);
        }

        [Test]
        public void RejectsUnknownRelations()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NameplateRule.ShouldShow((TeamRelation)3, 1f, true, true));
        }
    }
}
