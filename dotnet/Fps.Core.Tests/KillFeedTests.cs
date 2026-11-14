using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class KillFeedTests
    {
        private static readonly LocalizedText _rifle = new LocalizedText("weapon.rifle");

        [Test]
        public void StartsEmptyWithTheDefaultLimits()
        {
            var feed = new KillFeed();

            Assert.That(feed.Capacity, Is.EqualTo(5));
            Assert.That(feed.LineSeconds, Is.EqualTo(5f));
            Assert.That(feed.Count, Is.Zero);
            Assert.That(feed.Lines, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsACapacityBelowOne(int capacity)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new KillFeed(capacity, 5f));
            Assert.That(ex.ParamName, Is.EqualTo("capacity"));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsLineSecondsThatAreNotPositiveAndFinite(float seconds)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new KillFeed(5, seconds));
            Assert.That(ex.ParamName, Is.EqualTo("lineSeconds"));
        }

        [Test]
        public void AddsLinesOldestFirst()
        {
            var feed = new KillFeed();

            feed.Add(Line(1, 2));
            feed.Add(Line(3, 4));

            Assert.That(feed.Lines, Is.EqualTo(new[] { Line(1, 2), Line(3, 4) }));
            Assert.That(feed.RemainingSeconds(0), Is.EqualTo(5f));
            Assert.That(feed.RemainingSeconds(1), Is.EqualTo(5f));
        }

        [Test]
        public void TheSixthLineDropsTheOldest()
        {
            var feed = new KillFeed();
            for (var i = 1; i <= 6; i++)
            {
                feed.Add(Line(i, 100 + i));
            }

            Assert.That(feed.Count, Is.EqualTo(5));
            Assert.That(feed.Lines[0], Is.EqualTo(Line(2, 102)));
            Assert.That(feed.Lines[4], Is.EqualTo(Line(6, 106)));
        }

        [Test]
        public void ALineDisappearsWhenItsSecondsRunOut()
        {
            var feed = new KillFeed();
            feed.Add(Line(1, 2));

            feed.Tick(4.5f);
            Assert.That(feed.Count, Is.EqualTo(1));
            Assert.That(feed.RemainingSeconds(0), Is.EqualTo(0.5f));

            feed.Tick(0.5f);
            Assert.That(feed.Count, Is.Zero);
        }

        [Test]
        public void EachLineCountsFromWhenItWasAdded()
        {
            var feed = new KillFeed();
            feed.Add(Line(1, 2));
            feed.Tick(3f);
            feed.Add(Line(3, 4));

            feed.Tick(2f);

            Assert.That(feed.Lines, Is.EqualTo(new[] { Line(3, 4) }));
            Assert.That(feed.RemainingSeconds(0), Is.EqualTo(3f));
        }

        [Test]
        public void ALongTickRemovesEveryExpiredLineAndRaisesChangedOnce()
        {
            var feed = new KillFeed();
            feed.Add(Line(1, 2));
            feed.Add(Line(3, 4));
            var changes = 0;
            feed.Changed += () => changes++;

            feed.Tick(10f);

            Assert.That(feed.Count, Is.Zero);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void RaisesChangedOnlyWhenTheLinesChange()
        {
            var feed = new KillFeed();
            var changes = 0;
            feed.Changed += () => changes++;

            feed.Tick(1f);
            feed.Clear();
            Assert.That(changes, Is.Zero);

            feed.Add(Line(1, 2));
            Assert.That(changes, Is.EqualTo(1));

            feed.Tick(1f);
            feed.Tick(0f);
            Assert.That(changes, Is.EqualTo(1));

            feed.Clear();
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(feed.Count, Is.Zero);
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsATickThatIsNotNonNegativeAndFinite(float delta)
        {
            var feed = new KillFeed();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => feed.Tick(delta));
            Assert.That(ex.ParamName, Is.EqualTo("deltaSeconds"));
        }

        [Test]
        public void RemainingSecondsRejectsAnIndexOutsideTheLines()
        {
            var feed = new KillFeed();
            feed.Add(Line(1, 2));

            Assert.Throws<ArgumentOutOfRangeException>(() => feed.RemainingSeconds(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => feed.RemainingSeconds(1));
        }

        [Test]
        public void ASmallerFeedKeepsItsOwnLimits()
        {
            var feed = new KillFeed(2, 1f);
            feed.Add(Line(1, 2));
            feed.Add(Line(3, 4));
            feed.Add(Line(5, 6));

            Assert.That(feed.Lines, Is.EqualTo(new[] { Line(3, 4), Line(5, 6) }));

            feed.Tick(1f);
            Assert.That(feed.Count, Is.Zero);
        }

        [Test]
        public void ALineIsHighlightedWhenTheLocalPlayerKilledOrDied()
        {
            var entry = new KillFeedEntry(1, 2, "rifle", false, false);
            var me = new KillFeedName("P1", Team.Alpha, true);
            var other = new KillFeedName("P2", Team.Bravo, false);

            Assert.That(new KillFeedLine(entry, me, other, _rifle).Highlighted, Is.True);
            Assert.That(new KillFeedLine(entry, other, me, _rifle).Highlighted, Is.True);
            Assert.That(new KillFeedLine(entry, other, other, _rifle).Highlighted, Is.False);
        }

        [Test]
        public void ASuicideLineHidesTheKiller()
        {
            var name = new KillFeedName("P1", Team.Alpha, false);
            var suicide = new KillFeedLine(new KillFeedEntry(1, 1, null, false, false), name, name, default);
            var kill = new KillFeedLine(new KillFeedEntry(1, 2, null, false, false), name, name, default);

            Assert.That(suicide.ShowsKiller, Is.False);
            Assert.That(kill.ShowsKiller, Is.True);
        }

        [Test]
        public void DefaultLinesAndNamesReadAsEmptyText()
        {
            Assert.That(default(KillFeedLine).WeaponName.IsEmpty, Is.True);
            Assert.That(GameText.Resolve(default(KillFeedLine).WeaponName), Is.Empty);
            Assert.That(default(KillFeedName).Text, Is.Empty);
            Assert.That(default(KillFeedName).Team, Is.Null);
        }

        [Test]
        public void NamesWithTheSameValuesAreEqual()
        {
            var a = new KillFeedName("P1", Team.Bravo, true);

            Assert.That(a, Is.EqualTo(new KillFeedName("P1", Team.Bravo, true)));
            Assert.That(a.GetHashCode(), Is.EqualTo(new KillFeedName("P1", Team.Bravo, true).GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(new KillFeedName("P2", Team.Bravo, true)));
            Assert.That(a, Is.Not.EqualTo(new KillFeedName("P1", Team.Alpha, true)));
            Assert.That(a, Is.Not.EqualTo(new KillFeedName("P1", null, true)));
            Assert.That(a, Is.Not.EqualTo(new KillFeedName("P1", Team.Bravo, false)));
        }

        [TestCase("weapon.rifle", false, false, ExpectedResult = "[Rifle]")]
        [TestCase("weapon.rifle", true, false, ExpectedResult = "[Rifle] 頭")]
        [TestCase(null, false, true, ExpectedResult = "[自滅]")]
        [TestCase(null, false, false, ExpectedResult = "[-]")]
        [TestCase("weapon.rifle", false, true, ExpectedResult = "[Rifle]")]
        public string FormatsTheWeapon(string weaponNameKey, bool headshot, bool suicide)
        {
            var name = weaponNameKey == null ? default : new LocalizedText(weaponNameKey);
            return GameText.Resolve(KillFeedFormat.Weapon(name, headshot, suicide));
        }

        [Test]
        public void TheWeaponColumnIsKeyedWithTheNameAndTheMarksNested()
        {
            Assert.That(KillFeedFormat.Weapon(_rifle, false, false), Is.EqualTo(new LocalizedText("hud.killFeed.weapon", _rifle)));
            Assert.That(
                KillFeedFormat.Weapon(_rifle, true, false),
                Is.EqualTo(new LocalizedText("hud.killFeed.weaponHeadshot", _rifle, KillFeedFormat.HeadshotMark)));
            Assert.That(KillFeedFormat.Weapon(default, false, true), Is.EqualTo(new LocalizedText("hud.killFeed.weapon", KillFeedFormat.Suicide)));
            Assert.That(GameText.Resolve(KillFeedFormat.FriendlyMark), Is.EqualTo("味方撃ち"));
        }

        [Test]
        public void AWeaponWithoutANameKeyShowsItsIdAsIs()
        {
            var id = "00000000-0000-4000-8000-000000000001";
            var spec = new WeaponSpec(id, null, WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None);

            Assert.That(GameText.Resolve(KillFeedFormat.Weapon(spec.Name, false, false)), Is.EqualTo("[" + id + "]"));
        }

        [Test]
        public void LinesWithTheSameWeaponNameAreEqual()
        {
            var entry = new KillFeedEntry(1, 2, "rifle", false, false);
            var killer = new KillFeedName("P1", Team.Alpha, false);
            var victim = new KillFeedName("P2", Team.Bravo, false);
            var line = new KillFeedLine(entry, killer, victim, _rifle);

            Assert.That(line, Is.EqualTo(new KillFeedLine(entry, killer, victim, new LocalizedText("weapon.rifle"))));
            Assert.That(line.GetHashCode(), Is.EqualTo(new KillFeedLine(entry, killer, victim, new LocalizedText("weapon.rifle")).GetHashCode()));
            Assert.That(line, Is.Not.EqualTo(new KillFeedLine(entry, killer, victim, new LocalizedText("weapon.handgun"))));
            Assert.That(line, Is.Not.EqualTo(new KillFeedLine(entry, killer, victim, LocalizedText.Literal("weapon.rifle"))));
        }

        [Test]
        public void FormatsAWholeLine()
        {
            var killer = new KillFeedName("P1", Team.Alpha, false);
            var victim = new KillFeedName("P2", Team.Alpha, false);
            var friendly = new KillFeedLine(new KillFeedEntry(1, 2, "id", true, true), killer, victim, _rifle);
            var suicide = new KillFeedLine(new KillFeedEntry(2, 2, string.Empty, false, false), victim, victim, default);

            Assert.That(KillFeedFormat.Line(friendly), Is.EqualTo("味方撃ち P1 [Rifle] 頭 P2"));
            Assert.That(KillFeedFormat.Line(suicide), Is.EqualTo("[自滅] P2"));
            Assert.That(friendly.ToString(), Is.EqualTo(KillFeedFormat.Line(friendly)));
        }

        [Test]
        public void FormatsPlayerNames()
        {
            Assert.That(KillFeedFormat.Player(0), Is.EqualTo("P0"));
            Assert.That(KillFeedFormat.Player(12), Is.EqualTo("P12"));
            Assert.That(KillFeedFormat.Unknown(3), Is.EqualTo("?3"));
        }

        private static KillFeedLine Line(int killerId, int victimId)
        {
            return new KillFeedLine(
                new KillFeedEntry(killerId, victimId, "w", false, false),
                new KillFeedName(KillFeedFormat.Player(killerId), Team.Alpha, false),
                new KillFeedName(KillFeedFormat.Player(victimId), Team.Bravo, false),
                _rifle);
        }
    }
}
