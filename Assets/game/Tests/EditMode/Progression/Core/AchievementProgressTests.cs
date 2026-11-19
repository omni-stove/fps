using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Progression.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AchievementProgressTests
    {
        [Test]
        public void AchievementProgressFillsUpToTheTargetAndStaysUnlocked()
        {
            var halfway = new AchievementProgress(" marksman ", 25, 50, false);
            var past = new AchievementProgress("marksman", 80, 50, false);
            var raisedAfterUnlock = new AchievementProgress("marksman", 50, 100, true);

            Assert.That(halfway.Id, Is.EqualTo("marksman"));
            Assert.That(halfway.Fraction, Is.EqualTo(0.5f));
            Assert.That(past.Fraction, Is.EqualTo(1f));
            Assert.That(raisedAfterUnlock.Fraction, Is.EqualTo(1f));
            Assert.That(halfway, Is.EqualTo(new AchievementProgress("marksman", 25, 50, false)));
            Assert.That(halfway, Is.Not.EqualTo(new AchievementProgress("marksman", 25, 50, true)));
            Assert.That(halfway.ToString(), Is.EqualTo("marksman 25/50"));
            Assert.That(raisedAfterUnlock.ToString(), Is.EqualTo("marksman 50/100 unlocked"));
            Assert.Throws<ArgumentException>(() => new AchievementProgress(" ", 0, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AchievementProgress("a", -1, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AchievementProgress("a", 0, 0, false));
        }
    }
}
