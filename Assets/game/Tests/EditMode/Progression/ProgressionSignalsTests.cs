using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    public sealed class ProgressionSignalsTests
    {
        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
        }

        [Test]
        public void TheAchievementSignalStartsEmpty()
        {
            Assert.That(ProgressionSignals.Achievements.Value, Is.Null);
            Assert.That(ProgressionSignals.Achievements.HasValue, Is.False);
        }

        [Test]
        public void TheAchievementSignalHoldsWhatWasPublished()
        {
            var achievements = new[] { new AchievementProgress("marksman", 10, 50, false) };

            ProgressionSignals.Achievements.Publish(achievements);

            Assert.That(ProgressionSignals.Achievements.Value, Is.SameAs(achievements));
        }

        [Test]
        public void ResetAllEmptiesTheAchievementSignalAndDropsItsSubscribers()
        {
            var heard = 0;
            ProgressionSignals.Achievements.Changed += _ => heard++;
            ProgressionSignals.Achievements.Publish(new[] { new AchievementProgress("first-blood", 0, 1, false) });
            heard = 0;

            GameSignals.ResetAll();

            Assert.That(ProgressionSignals.Achievements.Value, Is.Null);
            ProgressionSignals.Achievements.Publish(new[] { new AchievementProgress("first-blood", 1, 1, true) });
            Assert.That(heard, Is.Zero, "subscribers from before the reset are gone");
        }
    }
}
