using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ScoreboardNameTests
    {
        [Test]
        public void EachKindKeepsItsNumber()
        {
            Assert.That(ScoreboardName.LocalPlayer.Kind, Is.EqualTo(ScoreboardNameKind.LocalPlayer));
            Assert.That(ScoreboardName.LocalPlayer.Number, Is.Zero);
            Assert.That(ScoreboardName.Player(3).Kind, Is.EqualTo(ScoreboardNameKind.Player));
            Assert.That(ScoreboardName.Player(3).Number, Is.EqualTo(3));
            Assert.That(ScoreboardName.Target(2).Kind, Is.EqualTo(ScoreboardNameKind.Target));
            Assert.That(ScoreboardName.Target(2).Number, Is.EqualTo(2));
            Assert.That(ScoreboardName.Bot(5).Kind, Is.EqualTo(ScoreboardNameKind.Bot));
            Assert.That(ScoreboardName.Bot(5).Number, Is.EqualTo(5));
            Assert.That(ScoreboardName.Player(3).Text, Is.Empty);
        }

        [Test]
        public void ALiteralKeepsItsText()
        {
            var name = ScoreboardName.Literal("enemy-1");

            Assert.That(name.Kind, Is.EqualTo(ScoreboardNameKind.Literal));
            Assert.That(name.Text, Is.EqualTo("enemy-1"));
            Assert.That(name.Number, Is.Zero);
            Assert.That(name.IsEmpty, Is.False);
        }

        [Test]
        public void DefaultAndNullLiteralsAreEmpty()
        {
            Assert.That(default(ScoreboardName).IsEmpty, Is.True);
            Assert.That(default(ScoreboardName).Text, Is.Empty);
            Assert.That(ScoreboardName.Literal(null), Is.EqualTo(default(ScoreboardName)));
            Assert.That(ScoreboardName.Literal(null).GetHashCode(), Is.EqualTo(default(ScoreboardName).GetHashCode()));
            Assert.That(ScoreboardName.LocalPlayer.IsEmpty, Is.False);
        }

        [Test]
        public void NamesAreEqualByKindNumberAndText()
        {
            var three = ScoreboardName.Player(3);

            Assert.That(three, Is.EqualTo(ScoreboardName.Player(3)));
            Assert.That(three == ScoreboardName.Player(3), Is.True);
            Assert.That(three.GetHashCode(), Is.EqualTo(ScoreboardName.Player(3).GetHashCode()));
            Assert.That(ScoreboardName.Player(3), Is.Not.EqualTo(ScoreboardName.Player(4)));
            Assert.That(ScoreboardName.Player(1), Is.Not.EqualTo(ScoreboardName.Bot(1)));
            Assert.That(ScoreboardName.Target(1) != ScoreboardName.Bot(1), Is.True);
            Assert.That(ScoreboardName.Literal("a"), Is.Not.EqualTo(ScoreboardName.Literal("b")));
        }

        [Test]
        public void TargetsAndBotsCountFromOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardName.Target(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardName.Bot(0));
        }

        [Test]
        public void ToStringShowsTheKind()
        {
            Assert.That(ScoreboardName.LocalPlayer.ToString(), Is.EqualTo("LocalPlayer"));
            Assert.That(ScoreboardName.Target(2).ToString(), Is.EqualTo("Target 2"));
            Assert.That(ScoreboardName.Literal("enemy-1").ToString(), Is.EqualTo("'enemy-1'"));
        }
    }
}
