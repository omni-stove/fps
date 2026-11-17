using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class ScoreboardTextTests
    {
        [Test]
        public void NamesAreKeyed()
        {
            Assert.That(ScoreboardText.PlayerName(3), Is.EqualTo(new LocalizedText("hud.scoreboard.player", 3)));
            Assert.That(ScoreboardText.TargetName(2), Is.EqualTo(new LocalizedText("hud.scoreboard.target", 2)));
            Assert.That(ScoreboardText.LocalPlayerName, Is.EqualTo(new LocalizedText("hud.scoreboard.you")));
            Assert.That(ScoreboardText.ConnectionCaption, Is.EqualTo(new LocalizedText("hud.scoreboard.connectionCaption")));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardText.TargetName(0));
        }

        [Test]
        public void BotNamesCarryTheBotTag()
        {
            Assert.That(ScoreboardText.BotName(1), Is.EqualTo(LocalizedText.Literal("[BOT] Bot 1")));
            Assert.That(GameText.Resolve(ScoreboardText.BotName(12)), Is.EqualTo("[BOT] Bot 12"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardText.BotName(0));
        }

        [Test]
        public void NameTurnsEachKindIntoItsText()
        {
            Assert.That(ScoreboardText.Name(ScoreboardName.LocalPlayer), Is.EqualTo(ScoreboardText.LocalPlayerName));
            Assert.That(ScoreboardText.Name(ScoreboardName.Player(3)), Is.EqualTo(ScoreboardText.PlayerName(3)));
            Assert.That(ScoreboardText.Name(ScoreboardName.Target(2)), Is.EqualTo(ScoreboardText.TargetName(2)));
            Assert.That(ScoreboardText.Name(ScoreboardName.Bot(1)), Is.EqualTo(ScoreboardText.BotName(1)));
            Assert.That(ScoreboardText.Name(BotRoster.ScoreboardName(BotRoster.FirstBotPlayerId)), Is.EqualTo(LocalizedText.Literal("[BOT] Bot 1")));
            Assert.That(GameText.Resolve(ScoreboardText.Name(ScoreboardName.Literal("enemy-1"))), Is.EqualTo("enemy-1"));
        }

        [Test]
        public void AnEmptyNameIsAnEmptyText()
        {
            Assert.That(ScoreboardText.Name(default).IsEmpty, Is.True);
            Assert.That(ScoreboardText.Name(ScoreboardName.Literal(string.Empty)).IsEmpty, Is.True);
            Assert.That(GameText.Resolve(ScoreboardText.Name(default(ScoreboardEntry).Name)), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ConnectionTextIsKeyedAndNoneIsEmpty()
        {
            Assert.That(ScoreboardText.ConnectionText(ScoreboardConnection.None).IsEmpty, Is.True);
            Assert.That(ScoreboardText.ConnectionText(ScoreboardConnection.Connected), Is.EqualTo(new LocalizedText("hud.scoreboard.connected")));
            Assert.That(ScoreboardText.ConnectionText(ScoreboardConnection.Disconnected), Is.EqualTo(new LocalizedText("hud.scoreboard.disconnected")));
            Assert.That(ScoreboardText.ConnectedText, Is.EqualTo(ScoreboardText.ConnectionText(ScoreboardConnection.Connected)));
            Assert.That(ScoreboardText.DisconnectedText, Is.EqualTo(ScoreboardText.ConnectionText(ScoreboardConnection.Disconnected)));
        }

        [Test]
        public void ConnectionTextRejectsUnknownValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardText.ConnectionText((ScoreboardConnection)9));
        }

        [Test]
        public void CountRejectsNegatives()
        {
            Assert.That(ScoreboardText.Count(12), Is.EqualTo("12"));
            Assert.That(ScoreboardText.Count(0), Is.EqualTo("0"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardText.Count(-1));
        }

        [Test]
        public void TheTextsResolveFromTheTables()
        {
            var previous = GameText.Source;
            var table = new StringTable(new[]
            {
                new KeyValuePair<string, string>("hud.scoreboard.you", "あなた"),
                new KeyValuePair<string, string>("hud.scoreboard.player", "Player {0}"),
                new KeyValuePair<string, string>("hud.scoreboard.target", "標的 {0}"),
                new KeyValuePair<string, string>("hud.scoreboard.connected", "接続中"),
            });
            GameText.Use(() => table);
            try
            {
                Assert.That(GameText.Resolve(ScoreboardText.Name(ScoreboardName.LocalPlayer)), Is.EqualTo("あなた"));
                Assert.That(GameText.Resolve(ScoreboardText.Name(ScoreboardName.Player(3))), Is.EqualTo("Player 3"));
                Assert.That(GameText.Resolve(ScoreboardText.Name(ScoreboardName.Target(2))), Is.EqualTo("標的 2"));
                Assert.That(GameText.Resolve(ScoreboardText.ConnectionText(ScoreboardConnection.Connected)), Is.EqualTo("接続中"));
            }
            finally
            {
                GameText.Use(previous);
            }
        }
    }
}
