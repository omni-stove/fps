using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class StringTableTests
    {
        private static readonly string[] _expectedKeys = { "settings.title", "settings.back" };
        private static readonly int[] _expectedPlaceholders = { 0, 2 };

        private List<string> _warnings;

        [SetUp]
        public void SetUp()
        {
            _warnings = new List<string>();
        }

        [Test]
        public void GetReturnsTheTextOfTheKey()
        {
            var table = Parse("{'settings.title':'設定','settings.back':'戻る'}");

            Assert.That(table.Get("settings.title"), Is.EqualTo("設定"));
            Assert.That(table.Get("settings.back"), Is.EqualTo("戻る"));
            Assert.That(table.Count, Is.EqualTo(2));
            Assert.That(table.Keys, Is.EquivalentTo(_expectedKeys));
            Assert.That(_warnings, Is.Empty);
        }

        [Test]
        public void KeysAreCaseSensitive()
        {
            var table = Parse("{'Title':'設定'}");

            Assert.That(table.Contains("Title"), Is.True);
            Assert.That(table.Contains("title"), Is.False);
            Assert.That(table.Get("title"), Is.EqualTo("title"));
        }

        [Test]
        public void AMissingKeyReturnsTheKeyItselfAndWarnsOnce()
        {
            var table = Parse("{'settings.title':'設定'}");

            Assert.That(table.Get("settings.missing"), Is.EqualTo("settings.missing"));
            Assert.That(table.Get("settings.missing"), Is.EqualTo("settings.missing"));
            Assert.That(table.Format("settings.missing", 1), Is.EqualTo("settings.missing"));

            Assert.That(_warnings, Has.Count.EqualTo(1), "every frame may ask for the same key");
            Assert.That(_warnings[0], Does.Contain("'settings.missing'"));

            table.Get("settings.other");
            Assert.That(_warnings, Has.Count.EqualTo(2), "each missing key is reported");
        }

        [Test]
        public void ContainsDoesNotWarn()
        {
            var table = Parse("{'a':'A'}");

            Assert.That(table.Contains("a"), Is.True);
            Assert.That(table.Contains("b"), Is.False);
            Assert.That(_warnings, Is.Empty);
        }

        [Test]
        public void WithoutAWarningSinkAMissingKeyStillReturnsTheKey()
        {
            Assert.That(StringTable.TryParse("{}", null, out var table, out _), Is.True);

            Assert.That(table.Get("x"), Is.EqualTo("x"));
        }

        [Test]
        public void FormatInsertsTheValuesByNumber()
        {
            var table = Parse("{'prompt':'「{0}」({1}) に割り当てる入力をしてください。{2} でやめます。','swap':'{1} and {0} and {0}'}");

            Assert.That(table.Format("prompt", "撃つ", "ゲームパッド", "Start"), Is.EqualTo("「撃つ」(ゲームパッド) に割り当てる入力をしてください。Start でやめます。"));
            Assert.That(table.Format("swap", "a", "b"), Is.EqualTo("b and a and a"));
            Assert.That(_warnings, Is.Empty);
        }

        [Test]
        public void FormatWritesNumbersTheSameWayInEveryCulture()
        {
            var table = Parse("{'value':'{0} / {1}'}");

            Assert.That(table.Format("value", 1.5, 12345), Is.EqualTo("1.5 / 12345"));
        }

        [Test]
        public void FormatWritesNullAsNothing()
        {
            var table = Parse("{'value':'[{0}]'}");

            Assert.That(table.Format("value", new object[] { null }), Is.EqualTo("[]"));
        }

        [Test]
        public void DoubledBracesAreLiteralBraces()
        {
            var table = Parse("{'value':'{{0}} is {0}, }} {{'}");

            Assert.That(table.Format("value", "x"), Is.EqualTo("{0} is x, } {"));
            Assert.That(_warnings, Is.Empty);
        }

        // 訳の誤りで画面が止まらないよう、差し込めない所はそのまま残す
        [TestCase("{")]
        [TestCase("}")]
        [TestCase("{a}")]
        [TestCase("{-1}")]
        [TestCase("{ 0}")]
        [TestCase("{0")]
        [TestCase("{1234567}")]
        public void TextThatIsNotAPlaceholderIsLeftAsIs(string text)
        {
            var table = new StringTable(new Dictionary<string, string> { ["value"] = text }, _warnings.Add);

            Assert.That(table.Format("value", "x"), Is.EqualTo(text));
            Assert.That(_warnings, Is.Empty);
        }

        [Test]
        public void APlaceholderWithoutAValueIsLeftAsIsAndWarnedOnce()
        {
            var table = Parse("{'value':'{0} hit {1} with {2}'}");

            Assert.That(table.Format("value", "A", "B"), Is.EqualTo("A hit B with {2}"));
            Assert.That(table.Format("value", "A", "B"), Is.EqualTo("A hit B with {2}"));

            Assert.That(_warnings, Has.Count.EqualTo(1));
            Assert.That(_warnings[0], Does.Contain("'value'").And.Contain("{2}"));
        }

        [Test]
        public void GetDoesNotSubstitute()
        {
            var table = Parse("{'value':'{0}%'}");

            Assert.That(table.Get("value"), Is.EqualTo("{0}%"));
        }

        [Test]
        public void PlaceholdersOfListsEachNumberOnceInOrder()
        {
            Assert.That(StringTable.PlaceholdersOf("{2} {0} {2} {{1}} {x}"), Is.EqualTo(_expectedPlaceholders));
            Assert.That(StringTable.PlaceholdersOf("none"), Is.Empty);
        }

        [Test]
        public void TheTableDoesNotChangeWhenTheSourceChanges()
        {
            var source = new Dictionary<string, string> { ["a"] = "A" };
            var table = new StringTable(source);

            source["a"] = "changed";
            source["b"] = "B";

            Assert.That(table.Get("a"), Is.EqualTo("A"));
            Assert.That(table.Contains("b"), Is.False);
        }

        [Test]
        public void ReadsAByteOrderMarkAndWhitespace()
        {
            Assert.That(StringTable.TryParse("﻿\n{ \"a\" : \"A\" ,\r\n\t\"b\":\"\\u3042\\n\" }\n", null, out var table, out var error), Is.True, error);

            Assert.That(table.Get("a"), Is.EqualTo("A"));
            Assert.That(table.Get("b"), Is.EqualTo("あ\n"));
        }

        // 値がすべて文字列の、入れ子の無いオブジェクトでなければ読まない
        [TestCase("")]
        [TestCase("[]")]
        [TestCase("{'a':'A',}")]
        [TestCase("{'a':'A','a':'B'}")]
        [TestCase("{'a':1}")]
        [TestCase("{'a':null}")]
        [TestCase("{'a':{'b':'B'}}")]
        [TestCase("{'':'empty'}")]
        public void RejectsBrokenTables(string text)
        {
            Assert.That(StringTable.TryParse(Json(text), _warnings.Add, out var table, out var error), Is.False);

            Assert.That(table, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void RejectsNullArguments()
        {
            var table = Parse("{'a':'A'}");

            Assert.Throws<ArgumentNullException>(() => StringTable.TryParse(null, null, out _, out _));
            Assert.Throws<ArgumentNullException>(() => new StringTable(null));
            Assert.Throws<ArgumentNullException>(() => new StringTable(new[] { new KeyValuePair<string, string>("a", null) }));
            Assert.Throws<ArgumentNullException>(() => table.Get(null));
            Assert.Throws<ArgumentNullException>(() => table.Contains(null));
            Assert.Throws<ArgumentNullException>(() => table.Format("a", null));
            Assert.Throws<ArgumentNullException>(() => StringTable.PlaceholdersOf(null));
        }

        private StringTable Parse(string text)
        {
            Assert.That(StringTable.TryParse(Json(text), _warnings.Add, out var table, out var error), Is.True, error);
            return table;
        }

        /// <summary>読みやすさのため ' で書いた JSON を " に直す。</summary>
        private static string Json(string text)
        {
            return text.Replace('\'', '"');
        }
    }
}
