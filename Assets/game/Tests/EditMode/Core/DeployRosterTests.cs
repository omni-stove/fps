using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DeployRosterTests
    {
        [Test]
        public void RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeployRoster<string>(0, DeployOverflow.ReplaceOldest));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeployRoster<string>(1, (DeployOverflow)99));

            var roster = new DeployRoster<string>(1, DeployOverflow.ReplaceOldest);
            Assert.Throws<ArgumentOutOfRangeException>(() => roster.Configure(0, DeployOverflow.ReplaceOldest));
            Assert.Throws<ArgumentNullException>(() => roster.Add(null));
        }

        [Test]
        public void KeepsTheOrderOfPlacing()
        {
            var roster = new DeployRoster<string>(3, DeployOverflow.ReplaceOldest);

            Assert.That(roster.Add("a"), Is.Empty);
            Assert.That(roster.Add("b"), Is.Empty);

            Assert.That(roster.Items, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(roster.Count, Is.EqualTo(2));
        }

        [Test]
        public void ReplaceOldestEvictsTheOldestWhenFull()
        {
            var roster = new DeployRoster<string>(2, DeployOverflow.ReplaceOldest);
            roster.Add("a");
            roster.Add("b");

            Assert.That(roster.CanPlace, Is.True);
            Assert.That(roster.Add("c"), Is.EqualTo(new[] { "a" }));
            Assert.That(roster.Items, Is.EqualTo(new[] { "b", "c" }));
            Assert.That(roster.Add("d"), Is.EqualTo(new[] { "b" }));
            Assert.That(roster.Items, Is.EqualTo(new[] { "c", "d" }));
        }

        [Test]
        public void RefuseDoesNotPlaceWhenFull()
        {
            var roster = new DeployRoster<string>(1, DeployOverflow.Refuse);
            roster.Add("a");

            Assert.That(roster.CanPlace, Is.False);
            Assert.Throws<InvalidOperationException>(() => roster.Add("b"));
            Assert.That(roster.Items, Is.EqualTo(new[] { "a" }));

            Assert.That(roster.Remove("a"), Is.True);
            Assert.That(roster.CanPlace, Is.True);
        }

        [Test]
        public void RemovedItemsFreeTheirPlace()
        {
            var roster = new DeployRoster<string>(2, DeployOverflow.ReplaceOldest);
            roster.Add("a");
            roster.Add("b");

            Assert.That(roster.Remove("a"), Is.True);
            Assert.That(roster.Remove("a"), Is.False, "already removed");
            Assert.That(roster.Remove(null), Is.False);
            Assert.That(roster.Add("c"), Is.Empty);
            Assert.That(roster.Items, Is.EqualTo(new[] { "b", "c" }));
        }

        [Test]
        public void TheSameItemCannotBeAddedTwice()
        {
            var roster = new DeployRoster<string>(2, DeployOverflow.ReplaceOldest);
            roster.Add("a");

            Assert.Throws<ArgumentException>(() => roster.Add("a"));
        }

        [Test]
        public void LoweringTheLimitEvictsTheOldest()
        {
            var roster = new DeployRoster<string>(3, DeployOverflow.ReplaceOldest);
            roster.Add("a");
            roster.Add("b");
            roster.Add("c");

            Assert.That(roster.Configure(1, DeployOverflow.Refuse), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(roster.Items, Is.EqualTo(new[] { "c" }));
            Assert.That(roster.MaxActive, Is.EqualTo(1));
            Assert.That(roster.Overflow, Is.EqualTo(DeployOverflow.Refuse));
            Assert.That(roster.Configure(2, DeployOverflow.Refuse), Is.Empty);
        }

        [Test]
        public void ClearReturnsEverythingOldestFirst()
        {
            var roster = new DeployRoster<string>(3, DeployOverflow.ReplaceOldest);
            roster.Add("a");
            roster.Add("b");

            Assert.That(roster.Clear(), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(roster.Count, Is.Zero);
        }
    }
}
