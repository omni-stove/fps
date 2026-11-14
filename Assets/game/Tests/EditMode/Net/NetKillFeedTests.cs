using Fps.Core;
using NUnit.Framework;

namespace Fps.Net.Tests
{
    /// <summary>
    /// クライアントがキルフィードの 1 件をプレイヤー Id とチームの名前にする <see cref="NetKillFeedNames"/> を確かめる。
    /// </summary>
    public sealed class NetKillFeedTests
    {
        [Test]
        public void NamesUseThePlayerIdTheTeamAndTheWeaponName()
        {
            var names = new NetKillFeedNames(
                id => id == 0 ? Team.Alpha : id == 1 ? Team.Bravo : (Team?)null,
                id => id == "rifle" ? new LocalizedText("weapon.rifle") : default,
                1);

            var line = names.Describe(new KillFeedEntry(0, 1, "rifle", false, false));

            Assert.That(line.Killer, Is.EqualTo(new KillFeedName("P0", Team.Alpha, false)));
            Assert.That(line.Victim, Is.EqualTo(new KillFeedName("P1", Team.Bravo, true)));
            Assert.That(line.WeaponName, Is.EqualTo(new LocalizedText("weapon.rifle")));
            Assert.That(GameText.Resolve(line.WeaponText), Is.EqualTo("[Rifle]"), "the name is read from the table");
            Assert.That(line.Highlighted, Is.True);
        }

        [Test]
        public void ALeftPlayerKeepsTheIdNameWithoutATeam()
        {
            var names = new NetKillFeedNames(_ => null, null, 0);

            var line = names.Describe(new KillFeedEntry(5, 6, "rifle", false, false));

            Assert.That(line.Killer, Is.EqualTo(new KillFeedName("P5", null, false)));
            Assert.That(line.WeaponName.IsEmpty, Is.True);
            Assert.That(line.Highlighted, Is.False);
        }

        [Test]
        public void BotsAreNamedWithTheBotTag()
        {
            var bot = BotRoster.FirstBotPlayerId;
            var names = new NetKillFeedNames(id => id == bot ? Team.Bravo : Team.Alpha, null, 1);

            var line = names.Describe(new KillFeedEntry(bot, 1, "rifle", false, false));

            Assert.That(line.Killer, Is.EqualTo(new KillFeedName("[BOT] B1", Team.Bravo, false)));
            Assert.That(line.Victim, Is.EqualTo(new KillFeedName("P1", Team.Alpha, true)));
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
