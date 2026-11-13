using Fps.Core;
using NUnit.Framework;

namespace Fps.Net.Tests
{
    /// <summary>
    /// サーバーが Session の Killed から配るキルフィードの 1 件 (<see cref="NetTeamDeathmatch.KillFeedEntryFor"/>) と、
    /// クライアントがそれをプレイヤー Id とチームの名前にする <see cref="NetKillFeedNames"/> を確かめる。
    /// </summary>
    public sealed class NetKillFeedTests
    {
        [Test]
        public void AKillByTheShotCarriesTheWeaponAndTheHeadshot()
        {
            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 2, false, 1, 2, "rifle", true);

            Assert.That(entry, Is.EqualTo(new KillFeedEntry(1, 2, "rifle", true, false)));
        }

        [Test]
        public void AKillCreditedToSomeoneElseHasNoWeapon()
        {
            // ダウン中の敵に味方がとどめを刺し、キルは先にダウンさせた敵 (3) に付いた
            var entry = NetTeamDeathmatch.KillFeedEntryFor(3, 2, false, 4, 2, "rifle", true);

            Assert.That(entry.WeaponId, Is.Empty);
            Assert.That(entry.Headshot, Is.False);
        }

        [Test]
        public void AReflectedShotIsASuicideWithoutAWeapon()
        {
            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 1, false, 1, 2, "rifle", false);

            Assert.That(entry.IsSuicide, Is.True);
            Assert.That(entry.WeaponId, Is.Empty);
        }

        [Test]
        public void ADeathOutsideAShotHasNoWeapon()
        {
            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 2, false, null, null, null, false);

            Assert.That(entry.WeaponId, Is.Empty);
            Assert.That(entry.Headshot, Is.False);
        }

        [Test]
        public void AFriendlyKillIsMarked()
        {
            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 2, true, 1, 2, "rifle", false);

            Assert.That(entry.Friendly, Is.True);
        }

        [Test]
        public void NamesUseThePlayerIdTheTeamAndTheWeaponName()
        {
            var names = new NetKillFeedNames(
                id => id == 0 ? Team.Alpha : id == 1 ? Team.Bravo : (Team?)null,
                id => id == "rifle" ? "Rifle" : string.Empty,
                1);

            var line = names.Describe(new KillFeedEntry(0, 1, "rifle", false, false));

            Assert.That(line.Killer, Is.EqualTo(new KillFeedName("P0", Team.Alpha, false)));
            Assert.That(line.Victim, Is.EqualTo(new KillFeedName("P1", Team.Bravo, true)));
            Assert.That(line.WeaponName, Is.EqualTo("Rifle"));
            Assert.That(line.Highlighted, Is.True);
        }

        [Test]
        public void ALeftPlayerKeepsTheIdNameWithoutATeam()
        {
            var names = new NetKillFeedNames(_ => null, null, 0);

            var line = names.Describe(new KillFeedEntry(5, 6, "rifle", false, false));

            Assert.That(line.Killer, Is.EqualTo(new KillFeedName("P5", null, false)));
            Assert.That(line.WeaponName, Is.Empty);
            Assert.That(line.Highlighted, Is.False);
        }

        [Test]
        public void WithoutAMatchOnlyTheNamesAndTheHighlightAreKnown()
        {
            var names = NetKillFeedNames.For(null, 2);

            var line = names.Describe(new KillFeedEntry(2, 2, string.Empty, false, false));

            Assert.That(line.Victim, Is.EqualTo(new KillFeedName("P2", null, true)));
            Assert.That(line.ShowsKiller, Is.False);
            Assert.That(line.Highlighted, Is.True);
        }
    }
}
