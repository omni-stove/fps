using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ChatWordFilterTests
    {
        private static ChatWordFilter Filter(
            string words, char mask = ChatWordFilterSpec.DefaultMask, ChatWordFilterAction action = ChatWordFilterAction.Mask, bool wholeWordsOnly = false)
        {
            return new ChatWordFilter(new ChatWordFilterSpec(words.Split(','), mask, action, wholeWordsOnly));
        }

        [TestCase("noob", "NoOb here", "**** here")]
        [TestCase("noob", "noobnoob", "********")]
        [TestCase("noob", "ｎｏｏｂ", "****")]
        [TestCase("noob", "ＮＯＯＢ!", "****!")]
        [TestCase("ＮＯＯＢ", "say noob", "say ****")]
        [TestCase("a b", "a　b", "***")]
        [TestCase("ab,bc", "xabcx", "x***x")]
        [TestCase("ガード", "ｶﾞｰﾄﾞ!", "*****!")]
        [TestCase("ｶﾞｰﾄﾞ", "ガード", "***")]
        [TestCase("パン", "ﾊﾟﾝ", "***")]
        [TestCase("ヴ", "ｳﾞ", "**")]
        [TestCase("😀", "a😀b", "a*b")]
        public void MasksTheListedWordsInTheSendersOwnLetters(string words, string text, string expected)
        {
            Assert.That(Filter(words).Mask(text), Is.EqualTo(expected));
        }

        [Test]
        public void KeepsTheSameTextWhenNoWordIsFound()
        {
            var filter = Filter("noob");
            var text = "nice shot";

            Assert.That(filter.Mask(text), Is.SameAs(text));
            Assert.That(filter.Contains(text), Is.False);
            Assert.That(filter.Contains("ＮＯＯＢ"), Is.True);
            Assert.That(filter.Mask(null), Is.Null);
            Assert.That(filter.Mask(string.Empty), Is.Empty);
        }

        [Test]
        public void HalfWidthKanaWithoutAVoicedFormKeepsTheMarkApart()
        {
            Assert.That(Filter("ア").Mask("ｱﾞ"), Is.EqualTo("*ﾞ"));
            Assert.That(Filter("カ").Mask("ｶﾞ"), Is.EqualTo("ｶﾞ"));
        }

        [Test]
        public void UsesTheMaskOfTheSpec()
        {
            Assert.That(Filter("noob", '#').Mask("noob"), Is.EqualTo("####"));
        }

        [Test]
        public void WholeWordsOnlySkipsWordsInsideLongerWords()
        {
            Assert.That(Filter("ass", wholeWordsOnly: true).Mask("class ass, ASS1"), Is.EqualTo("class ***, ASS1"));
            Assert.That(Filter("ass").Mask("class ass"), Is.EqualTo("cl*** ***"));
        }

        [Test]
        public void ApplyMasksTheLineAndKeepsTheRest()
        {
            var line = new ChatLine(3, ChatChannel.Team, "gg noob", 4.5d);

            Assert.That(Filter("noob").Apply(line, out var filtered), Is.EqualTo(ChatRefusal.None));

            Assert.That(filtered, Is.EqualTo(new ChatLine(3, ChatChannel.Team, "gg ****", 4.5d)));
        }

        [Test]
        public void ApplyDropsTheLineWhenTheSpecSaysDrop()
        {
            var filter = Filter("noob", action: ChatWordFilterAction.Drop);
            var clean = new ChatLine(1, ChatChannel.All, "gg", 1d);

            Assert.That(filter.Apply(new ChatLine(1, ChatChannel.All, "NOOB", 1d), out var dropped), Is.EqualTo(ChatRefusal.Blocked));
            Assert.That(dropped, Is.EqualTo(default(ChatLine)));
            Assert.That(filter.Apply(clean, out var kept), Is.EqualTo(ChatRefusal.None));
            Assert.That(kept, Is.EqualTo(clean));
        }

        [Test]
        public void AnEmptyListLetsEveryLineThrough()
        {
            var filter = new ChatWordFilter(ChatWordFilterSpec.Default);
            var line = new ChatLine(1, ChatChannel.All, "anything", 0d);

            Assert.That(filter.IsEmpty, Is.True);
            Assert.That(filter.Count, Is.EqualTo(0));
            Assert.That(filter.Apply(line, out var filtered), Is.EqualTo(ChatRefusal.None));
            Assert.That(filtered, Is.EqualTo(line));
        }

        [Test]
        public void WordsThatFoldToTheSameWordCountOnce()
        {
            var filter = Filter("noob,ＮＯＯＢ,ﾊﾟﾝ,パン");

            Assert.That(filter.Count, Is.EqualTo(2));
            Assert.That(filter.Spec.Words, Has.Count.EqualTo(4));
        }

        [Test]
        public void FoldsWidthAndCase()
        {
            Assert.That(ChatWordFilter.Fold("ＡＢＣ　ｱｲ"), Is.EqualTo("abc アイ"));
            Assert.That(ChatWordFilter.Fold("ｶﾞﾊﾟｳﾞﾞ"), Is.EqualTo("ガパヴ゛"));
            Assert.That(ChatWordFilter.Fold("ﾞ"), Is.EqualTo("゛"));
            Assert.That(ChatWordFilter.Fold(null), Is.Empty);
        }

        [Test]
        public void RejectsANullSpec()
        {
            Assert.Throws<ArgumentNullException>(() => new ChatWordFilter(null));
        }
    }
}
