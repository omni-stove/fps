using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SaveJsonTests
    {
        [Test]
        public void ReadsEveryKindAsPlainValues()
        {
            var root = SaveJson.Parse("{\n  \"a\": [1, -2, \"x\"],\n  \"b\": {\"c\": true, \"d\": false, \"e\": null}\n}");

            Assert.That(root, Is.InstanceOf<Dictionary<string, object>>());
            var map = (Dictionary<string, object>)root;
            Assert.That(map.Count, Is.EqualTo(2));

            Assert.That(map["a"], Is.InstanceOf<List<object>>());
            var list = (List<object>)map["a"];
            Assert.That(list.Count, Is.EqualTo(3));
            Assert.That(list[0], Is.InstanceOf<long>());
            Assert.That(list[0], Is.EqualTo(1L));
            Assert.That(list[1], Is.EqualTo(-2L));
            Assert.That(list[2], Is.EqualTo("x"));

            var inner = (Dictionary<string, object>)map["b"];
            Assert.That(inner["c"], Is.EqualTo(true));
            Assert.That(inner["d"], Is.EqualTo(false));
            Assert.That(inner.ContainsKey("e"), Is.True);
            Assert.That(inner["e"], Is.Null);
        }

        [Test]
        public void KeysAreCaseSensitive()
        {
            var map = (Dictionary<string, object>)SaveJson.Parse("{\"key\": 1, \"Key\": 2}");

            Assert.That(map["key"], Is.EqualTo(1L));
            Assert.That(map["Key"], Is.EqualTo(2L));
            Assert.That(map.ContainsKey("KEY"), Is.False);
        }

        [TestCase("0", 0L)]
        [TestCase("-0", 0L)]
        [TestCase("42", 42L)]
        [TestCase("-17", -17L)]
        [TestCase("9223372036854775807", long.MaxValue)]
        [TestCase("-9223372036854775808", long.MinValue)]
        public void ReadsIntegersAsLong(string text, long expected)
        {
            var value = SaveJson.Parse(text);

            Assert.That(value, Is.InstanceOf<long>());
            Assert.That(value, Is.EqualTo(expected));
        }

        [TestCase("\"\"", "")]
        [TestCase("\"a\\\"b\\\\c\\/d\\be\\ff\\ng\\rh\\ti\\u00e9\\u00C9\"", "a\"b\\c/d\be\ff\ng\rh\ti\u00e9\u00c9")]
        public void ReadsStrings(string text, string expected)
        {
            Assert.That(SaveJson.Parse(text), Is.EqualTo(expected));
        }

        [Test]
        public void ReadsBackWhatAppendStringWrote()
        {
            var original = "quote\" back\\ line\n cr\r tab\t bell\u0007 é";
            var builder = new StringBuilder();
            SaveJson.AppendString(builder, original);

            Assert.That(SaveJson.Parse(builder.ToString()), Is.EqualTo(original));
        }

        [Test]
        public void AcceptsThirtyTwoLevelsOfNesting()
        {
            var text = new string('[', 32) + "1" + new string(']', 32);

            var value = SaveJson.Parse(text);

            for (var i = 0; i < 32; i++)
            {
                Assert.That(value, Is.InstanceOf<List<object>>());
                value = ((List<object>)value)[0];
            }

            Assert.That(value, Is.EqualTo(1L));
        }

        [Test]
        public void ReadsASavedProfileShape()
        {
            var map = (Dictionary<string, object>)SaveJson.Parse(
                "{\"version\":3,\"xp\":1200,\"unlocked\":[\"rifle\",\"smg\"],\"cosmetics\":{\"Camo\":\"desert\"}}");

            Assert.That(map["version"], Is.EqualTo(3L));
            Assert.That(map["xp"], Is.EqualTo(1200L));
            Assert.That(map["unlocked"], Is.EqualTo(new List<object> { "rifle", "smg" }));
            Assert.That(((Dictionary<string, object>)map["cosmetics"])["Camo"], Is.EqualTo("desert"));
        }

        [Test]
        public void RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => SaveJson.Parse(null));
        }

        [TestCase("", "invalid save data: unexpected end of input at end of input")]
        [TestCase("{not json", "invalid save data: expected a string key at line 1, column 2")]
        [TestCase("{\"a\": 1,}", "invalid save data: expected a string key at line 1, column 9")]
        [TestCase("{\"a\": 1, \"a\": 2}", "invalid save data: duplicate key \"a\" at line 1, column 10")]
        [TestCase("{\"a\": 1", "invalid save data: expected ',' at end of input")]
        [TestCase("{} x", "invalid save data: unexpected text after the value at line 1, column 4")]
        [TestCase("[01]", "invalid save data: numbers must not have leading zeros at line 1, column 3")]
        [TestCase("[1.5]", "invalid save data: only integers are supported at line 1, column 2")]
        [TestCase("{\"n\": 1e3}", "invalid save data: only integers are supported at line 1, column 7")]
        [TestCase("{\"n\": 2E2}", "invalid save data: only integers are supported at line 1, column 7")]
        [TestCase("9223372036854775808", "invalid save data: number is out of range at line 1, column 1")]
        [TestCase("-9223372036854775809", "invalid save data: number is out of range at line 1, column 1")]
        [TestCase("tru", "invalid save data: invalid literal at line 1, column 1")]
        [TestCase("\"abc", "invalid save data: unterminated string at end of input")]
        [TestCase("\"a\nb\"", "invalid save data: control character in string at line 1, column 3")]
        [TestCase("\"\\x\"", "invalid save data: invalid escape '\\x' at line 1, column 3")]
        [TestCase("\uFEFF{}", "invalid save data: unexpected character at the start of the data")]
        public void RejectsBrokenData(string text, string message)
        {
            var e = Assert.Throws<FormatException>(() => SaveJson.Parse(text));

            Assert.That(e.Message, Is.EqualTo(message));
        }

        [Test]
        public void RejectsMoreThanThirtyTwoLevelsOfNesting()
        {
            var text = new string('[', 33) + "1" + new string(']', 33);

            var e = Assert.Throws<FormatException>(() => SaveJson.Parse(text));

            Assert.That(e.Message, Is.EqualTo("invalid save data: nesting is too deep at line 1, column 33"));
        }
    }
}
