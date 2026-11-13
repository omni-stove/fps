using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TeamLayoutTests
    {
        [Test]
        public void FreeForAllGivesEachJoinItsOwnSide()
        {
            var layout = new FreeForAll();
            var counts = new Dictionary<Side, int>();

            Assert.That(layout.FixedSides, Is.Empty);
            Assert.That(layout.Capacity, Is.EqualTo(1));
            Assert.That(layout.Contains(new Side(42)), Is.True);
            Assert.That(layout.Assign(0, CountIn(counts)), Is.EqualTo((Side?)new Side(0)));
            Assert.That(layout.Assign(3, CountIn(counts)), Is.EqualTo((Side?)new Side(3)));
        }

        [Test]
        public void FreeForAllSkipsSidesThatAreAlreadyTaken()
        {
            var layout = new FreeForAll();
            var counts = new Dictionary<Side, int> { [new Side(1)] = 1, [new Side(2)] = 1 };

            Assert.That(layout.Assign(1, CountIn(counts)), Is.EqualTo((Side?)new Side(3)));
        }

        [Test]
        public void TwoTeamsAlternateStartingWithAlpha()
        {
            var layout = new TwoTeams();
            var counts = new Dictionary<Side, int>();

            Assert.That(layout.FixedSides, Is.EqualTo(new[] { Side.Alpha, Side.Bravo }));
            Assert.That(layout.Capacity, Is.Null);
            Assert.That(layout.Assign(0, CountIn(counts)), Is.EqualTo((Side?)Side.Alpha));
            Assert.That(layout.Assign(1, CountIn(counts)), Is.EqualTo((Side?)Side.Bravo));
            Assert.That(layout.Assign(2, CountIn(counts)), Is.EqualTo((Side?)Side.Alpha));
            Assert.That(layout.Contains(Side.Bravo), Is.True);
            Assert.That(layout.Contains(new Side(2)), Is.False);
        }

        [Test]
        public void SquadsGoRoundRobinAndSkipFullSquads()
        {
            var layout = new Squads(3, 2);
            var counts = new Dictionary<Side, int>();

            Assert.That(layout.FixedSides, Is.EqualTo(new[] { new Side(0), new Side(1), new Side(2) }));
            Assert.That(layout.Capacity, Is.EqualTo(2));
            Assert.That(layout.Assign(0, CountIn(counts)), Is.EqualTo((Side?)new Side(0)));
            Assert.That(layout.Assign(4, CountIn(counts)), Is.EqualTo((Side?)new Side(1)));

            counts[new Side(1)] = 2;
            Assert.That(layout.Assign(4, CountIn(counts)), Is.EqualTo((Side?)new Side(2)));

            counts[new Side(2)] = 2;
            Assert.That(layout.Assign(4, CountIn(counts)), Is.EqualTo((Side?)new Side(0)));

            counts[new Side(0)] = 2;
            Assert.That(layout.Assign(4, CountIn(counts)), Is.Null);
        }

        [Test]
        public void SquadsOnlyContainTheirOwnSides()
        {
            var layout = new Squads(4, 3);

            Assert.That(layout.Contains(new Side(3)), Is.True);
            Assert.That(layout.Contains(new Side(4)), Is.False);
        }

        [Test]
        public void RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Squads(1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Squads(2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TwoTeams().Assign(-1, _ => 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FreeForAll().Assign(-1, _ => 0));
            Assert.Throws<ArgumentNullException>(() => new FreeForAll().Assign(0, null));
            Assert.Throws<ArgumentNullException>(() => new Squads(2, 2).Assign(0, null));
        }

        private static Func<Side, int> CountIn(Dictionary<Side, int> counts)
        {
            return side => counts.TryGetValue(side, out var count) ? count : 0;
        }
    }
}
