using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // 文は JapaneseLanguageSetUp が効かせる日本語の表 (Resources/Localization/ja/squad.json) から引く。
    public sealed class SquadOrderTextTests
    {
        [Test]
        public void RepliesAndMarkersComeFromTheTable()
        {
            Assert.That(SquadOrderText.Reply("Bot_1", BotErrandKind.Hold), Is.EqualTo("Bot_1: 了解、そこを守る"));
            Assert.That(SquadOrderText.Reply("Bot_1", BotErrandKind.Attack), Is.EqualTo("Bot_1: 了解、あいつを狙う"));
            Assert.That(SquadOrderText.Reply("Bot_1", BotErrandKind.Follow), Is.EqualTo("Bot_1: 了解、付いていく"));
            Assert.That(SquadOrderText.Line(" ", SquadOrderText.TargetDownKey), Is.EqualTo("?: 片付けた、戻る"));
            Assert.That(SquadOrderText.Marker(BotErrandKind.Hold), Is.EqualTo("守れ"));
            Assert.That(SquadOrderText.Marker(BotErrandKind.Attack), Is.EqualTo("狙え"));
        }

        [Test]
        public void ARepliedOrderBecomesItsLine()
        {
            Assert.That(SquadOrderText.Reply(SquadOrderReply.Accepted("Bot_1", BotErrandKind.Follow)), Is.EqualTo("Bot_1: 了解、付いていく"));
            Assert.That(SquadOrderText.Reply(SquadOrderReply.TargetDown("Bot_2")), Is.EqualTo("Bot_2: 片付けた、戻る"));
            Assert.Throws<ArgumentOutOfRangeException>(() => SquadOrderReply.Accepted("Bot_1", BotErrandKind.Collect));
            Assert.Throws<ArgumentNullException>(() => SquadOrderReply.TargetDown(null));
        }

        [Test]
        public void EveryOrderHasItsKeysInBothLanguages()
        {
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var kind in new[] { BotErrandKind.Hold, BotErrandKind.Attack, BotErrandKind.Follow })
                {
                    Assert.That(table.Get(SquadOrderText.ReplyKey(kind)), Is.Not.EqualTo(SquadOrderText.ReplyKey(kind)), language + " " + kind);
                    Assert.That(table.Get(SquadOrderText.MarkerKey(kind)), Is.Not.EqualTo(SquadOrderText.MarkerKey(kind)), language + " " + kind);
                }

                Assert.That(table.Get(SquadOrderText.TargetDownKey), Is.Not.EqualTo(SquadOrderText.TargetDownKey), language.ToString());
                Assert.That(table.Get(SquadOrderText.LineKey), Is.Not.EqualTo(SquadOrderText.LineKey), language.ToString());
            }
        }

        [Test]
        public void OnlyOrdersHaveText()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SquadOrderText.ReplyKey(BotErrandKind.Collect));
            Assert.Throws<ArgumentOutOfRangeException>(() => SquadOrderText.MarkerKey(BotErrandKind.None));
            Assert.Throws<ArgumentNullException>(() => SquadOrderText.Line("Bot", null));
        }
    }
}
