using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class TextTableTests
    {
        private static readonly string[] _menuStartKeys = { "Menu.Start" };

        private static TextTable Table(params string[] keysAndFormats)
        {
            var entries = new List<KeyValuePair<string, string>>();
            for (var i = 0; i < keysAndFormats.Length; i += 2)
            {
                entries.Add(new KeyValuePair<string, string>(keysAndFormats[i], keysAndFormats[i + 1]));
            }

            return new TextTable(entries);
        }

        [Test]
        public void FillsTheArgumentsIntoTheFormat()
        {
            var table = Table("match.win", "{0} の勝ち ({1} 点)");

            Assert.That(table.Resolve(new LocalizedText("match.win", "Alpha", 30)), Is.EqualTo("Alpha の勝ち (30 点)"));
        }

        [Test]
        public void ResolvesAKeyWithoutArguments()
        {
            var table = Table("menu.start", "はじめる");

            Assert.That(table.Resolve(new LocalizedText("menu.start")), Is.EqualTo("はじめる"));
        }

        [Test]
        public void ResolvesNestedTextsWithTheSameTable()
        {
            var table = Table(
                "match.win", "{0} の勝ち",
                "team.named", "チーム {0}",
                "team.alpha", "アルファ");
            var text = new LocalizedText("match.win", new LocalizedText("team.named", new LocalizedText("team.alpha")));

            Assert.That(table.Resolve(text), Is.EqualTo("チーム アルファ の勝ち"));
        }

        [Test]
        public void ReturnsTheKeyItselfWhenTheTableDoesNotHaveIt()
        {
            var table = Table("menu.start", "はじめる");

            Assert.That(table.Resolve(new LocalizedText("menu.quit", 1)), Is.EqualTo("menu.quit"));
            Assert.That(TextTable.Empty.Resolve(new LocalizedText("menu.start")), Is.EqualTo("menu.start"));
        }

        [Test]
        public void ANestedMissingKeyBecomesTheKeyInsideTheSentence()
        {
            var table = Table("match.win", "{0} の勝ち");

            Assert.That(table.Resolve(new LocalizedText("match.win", new LocalizedText("team.bravo"))), Is.EqualTo("team.bravo の勝ち"));
        }

        [Test]
        public void LiteralIsNotLookedUpOrFormatted()
        {
            var table = Table("GG {0}", "引かれてはいけない");

            Assert.That(table.Resolve(LocalizedText.Literal("GG {0}")), Is.EqualTo("GG {0}"));
        }

        [Test]
        public void ALiteralArgumentIsInsertedAsIs()
        {
            var table = Table("chat.line", "{0}: {1}");

            Assert.That(
                table.Resolve(new LocalizedText("chat.line", LocalizedText.Literal("Bob"), LocalizedText.Literal("{0}"))),
                Is.EqualTo("Bob: {0}"));
        }

        [Test]
        public void DefaultTextResolvesToAnEmptyString()
        {
            Assert.That(Table("a", "b").Resolve(default), Is.Empty);
        }

        [Test]
        public void WritesNumbersWithTheInvariantCultureWhateverTheThreadCultureIs()
        {
            var table = Table("hud.time", "{0} 秒 / {1:0.0} m / {2:N0}");
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                // ICU の無い環境 (InvariantGlobalization) でも作れるよう、de-DE を引かずに小数点と桁区切りを入れ替えた文化を作る
                var commaDecimal = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                commaDecimal.NumberFormat.NumberDecimalSeparator = ",";
                commaDecimal.NumberFormat.NumberGroupSeparator = ".";
                Thread.CurrentThread.CurrentCulture = commaDecimal;

                Assert.That(table.Resolve(new LocalizedText("hud.time", 1.5f, 12.34f, 12345)), Is.EqualTo("1.5 秒 / 12.3 m / 12,345"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void EscapedBracesInTheFormatBecomeSingleBraces()
        {
            Assert.That(Table("k", "{{{0}}}").Resolve(new LocalizedText("k", 7)), Is.EqualTo("{7}"));
        }

        [Test]
        public void AFormatThatUsesMoreArgumentsThanGivenThrowsNamingTheKey()
        {
            var table = Table("match.win", "{0} の勝ち {1}");

            var ex = Assert.Throws<FormatException>(() => table.Resolve(new LocalizedText("match.win", "A")));
            Assert.That(ex.Message, Does.Contain("match.win"));
        }

        [Test]
        public void ABrokenFormatThrowsNamingTheKey()
        {
            var table = Table("broken", "{0");

            var ex = Assert.Throws<FormatException>(() => table.Resolve(new LocalizedText("broken", 1)));
            Assert.That(ex.Message, Does.Contain("broken"));
        }

        [Test]
        public void MergeLetsTheLaterTableWin()
        {
            var first = Table("a", "A1", "b", "B1");
            var second = Table("b", "B2", "c", "C2");

            var merged = first.Merge(second);

            Assert.That(merged.Count, Is.EqualTo(3));
            Assert.That(merged.Resolve(new LocalizedText("a")), Is.EqualTo("A1"));
            Assert.That(merged.Resolve(new LocalizedText("b")), Is.EqualTo("B2"));
            Assert.That(merged.Resolve(new LocalizedText("c")), Is.EqualTo("C2"));
        }

        [Test]
        public void MergeLeavesBothTablesUnchanged()
        {
            var first = Table("a", "A1");
            var second = Table("a", "A2", "b", "B2");

            first.Merge(second);

            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(first.Resolve(new LocalizedText("a")), Is.EqualTo("A1"));
            Assert.That(second.Count, Is.EqualTo(2));
            Assert.That(TextTable.Empty.Count, Is.Zero);
        }

        [Test]
        public void MergeRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => TextTable.Empty.Merge(null));
        }

        [Test]
        public void LooksUpFormatsByExactKey()
        {
            var table = Table("Menu.Start", "はじめる");

            Assert.That(table.Contains("Menu.Start"), Is.True);
            Assert.That(table.Contains("menu.start"), Is.False);
            Assert.That(table.Contains(null), Is.False);
            Assert.That(table.TryGetFormat("Menu.Start", out var format), Is.True);
            Assert.That(format, Is.EqualTo("はじめる"));
            Assert.That(table.TryGetFormat(null, out _), Is.False);
            Assert.That(table.Keys, Is.EquivalentTo(_menuStartKeys));
        }

        [Test]
        public void RejectsBadEntries()
        {
            Assert.Throws<ArgumentNullException>(() => new TextTable(null));
            Assert.Throws<ArgumentException>(() => Table(string.Empty, "x"));
            Assert.Throws<ArgumentException>(() => Table("k", null));
            Assert.Throws<ArgumentException>(() => Table("k", "1", "k", "2"));
        }

        [Test]
        public void ReadsAFlatJsonObject()
        {
            var table = TextTable.FromJson("{\n  \"match.win\": \"{0} の勝ち\",\r\n\t\"menu.start\" : \"はじめる\"\n}\n");

            Assert.That(table.Count, Is.EqualTo(2));
            Assert.That(table.Resolve(new LocalizedText("match.win", "A")), Is.EqualTo("A の勝ち"));
            Assert.That(table.Resolve(new LocalizedText("menu.start")), Is.EqualTo("はじめる"));
        }

        [Test]
        public void ReadsAnEmptyJsonObject()
        {
            Assert.That(TextTable.FromJson(" { } ").Count, Is.Zero);
        }

        [Test]
        public void SkipsALeadingByteOrderMark()
        {
            Assert.That(TextTable.FromJson("\uFEFF{\"a\":\"b\"}").Resolve(new LocalizedText("a")), Is.EqualTo("b"));
        }

        [TestCase(@"{""k"":""say \""hi\""""}", "say \"hi\"")]
        [TestCase(@"{""k"":""a\\b""}", "a\\b")]
        [TestCase(@"{""k"":""line1\nline2""}", "line1\nline2")]
        [TestCase(@"{""k"":""\u3042\u0041""}", "あA")]
        [TestCase(@"{""k"":""\uD83D\uDE00""}", "\U0001F600")]
        [TestCase(@"{""k"":""\/\b\f\r\t""}", "/\b\f\r\t")]
        public void UnescapesStringValues(string json, string expected)
        {
            TextTable.FromJson(json).TryGetFormat("k", out var format);

            Assert.That(format, Is.EqualTo(expected));
        }

        [Test]
        public void UnescapesKeysToo()
        {
            var table = TextTable.FromJson(@"{""a\""b"":""x""}");

            Assert.That(table.Contains("a\"b"), Is.True);
        }

        [Test]
        public void RejectsNullJson()
        {
            Assert.Throws<ArgumentNullException>(() => TextTable.FromJson(null));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("[]")]
        [TestCase("{")]
        [TestCase("{\"a\":\"b\"")]
        [TestCase("{\"a\":\"b\",}")]
        [TestCase("{\"a\" \"b\"}")]
        [TestCase("{\"a\":\"b\" \"c\":\"d\"}")]
        [TestCase("{a:\"b\"}")]
        [TestCase("{'a':'b'}")]
        [TestCase("{\"a\":1}")]
        [TestCase("{\"a\":null}")]
        [TestCase("{\"a\":{\"b\":\"c\"}}")]
        [TestCase("{\"a\":[\"b\"]}")]
        [TestCase("{\"a\":\"b}")]
        [TestCase("{\"a\":\"b\\")]
        [TestCase("{\"a\":\"\\x\"}")]
        [TestCase("{\"a\":\"\\u12\"}")]
        [TestCase("{\"a\":\"\\u12G4\"}")]
        [TestCase("{\"a\":\"line\nbreak\"}")]
        [TestCase("{\"\":\"b\"}")]
        [TestCase("{\"a\":\"b\",\"a\":\"c\"}")]
        [TestCase("{\"a\":\"b\"} x")]
        [TestCase("{\"a\":\"b\"}{}")]
        public void RejectsBrokenJson(string json)
        {
            Assert.Throws<FormatException>(() => TextTable.FromJson(json));
        }

        [Test]
        public void TellsWhereTheJsonBroke()
        {
            var ex = Assert.Throws<FormatException>(() => TextTable.FromJson("{\n  \"a\": 1\n}"));

            Assert.That(ex.Message, Does.Contain("position 10"));
        }
    }
}
