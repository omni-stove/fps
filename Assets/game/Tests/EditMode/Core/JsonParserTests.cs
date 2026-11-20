using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class JsonParserTests
    {
        private static readonly string[] _expectedKeyOrder ={ "z", "a", "m" };

        [Test]
        public void ReadsEveryKindWithItsPosition()
        {
            var root = new JsonParser("{\n  \"a\": [1, -2.5e3, \"x\"],\n  \"b\": {\"c\": true, \"d\": false, \"e\": null}\n}").ParseDocument();

            Assert.That(root.Kind, Is.EqualTo(JsonNodeKind.Object));
            Assert.That(root.Position, Is.EqualTo("line 1, column 1"));
            Assert.That(root.Members.Count, Is.EqualTo(2));

            Assert.That(root.Members[0].Key, Is.EqualTo("a"));
            var array = root.Members[0].Value;
            Assert.That(array.Kind, Is.EqualTo(JsonNodeKind.Array));
            Assert.That(array.Position, Is.EqualTo("line 2, column 8"));
            Assert.That(array.Elements.Count, Is.EqualTo(3));
            Assert.That(array.Elements[0].Kind, Is.EqualTo(JsonNodeKind.Number));
            Assert.That(array.Elements[0].Text, Is.EqualTo("1"));
            Assert.That(array.Elements[0].Position, Is.EqualTo("line 2, column 9"));
            Assert.That(array.Elements[1].Text, Is.EqualTo("-2.5e3"));
            Assert.That(array.Elements[2].Kind, Is.EqualTo(JsonNodeKind.String));
            Assert.That(array.Elements[2].Text, Is.EqualTo("x"));

            Assert.That(root.Members[1].Key, Is.EqualTo("b"));
            var inner = root.Members[1].Value;
            Assert.That(inner.Kind, Is.EqualTo(JsonNodeKind.Object));
            Assert.That(inner.Position, Is.EqualTo("line 3, column 8"));
            Assert.That(inner.Members[0].Key, Is.EqualTo("c"));
            Assert.That(inner.Members[0].Value.Kind, Is.EqualTo(JsonNodeKind.True));
            Assert.That(inner.Members[1].Value.Kind, Is.EqualTo(JsonNodeKind.False));
            Assert.That(inner.Members[2].Value.Kind, Is.EqualTo(JsonNodeKind.Null));
            Assert.That(inner.Members[2].Value.Text, Is.Null);
        }

        [Test]
        public void KeepsMembersInTheWrittenOrder()
        {
            var root = new JsonParser("{\"z\": 1, \"a\": 2, \"m\": 3}").ParseDocument();

            Assert.That(root.Members.ConvertAll(m => m.Key), Is.EqualTo(_expectedKeyOrder));
        }

        [TestCase("0")]
        [TestCase("-0")]
        [TestCase("1.50")]
        [TestCase("2E+3")]
        [TestCase("7e-2")]
        [TestCase("123456789012345678901234567890")]
        public void KeepsNumbersAsWritten(string number)
        {
            var node = new JsonParser(number).ParseDocument();

            Assert.That(node.Kind, Is.EqualTo(JsonNodeKind.Number));
            Assert.That(node.Text, Is.EqualTo(number));
        }

        [Test]
        public void ReadsEveryEscape()
        {
            var node = new JsonParser("\"a\\\"b\\\\c\\/d\\be\\ff\\ng\\rh\\ti\\u00e9\\u00C9\"").ParseDocument();

            Assert.That(node.Text, Is.EqualTo("a\"b\\c/d\be\ff\ng\rh\ti\u00e9\u00c9"));
        }

        [Test]
        public void SkipsALeadingByteOrderMark()
        {
            var root = new JsonParser("\uFEFF{\"a\": 1}").ParseDocument();

            Assert.That(root.Kind, Is.EqualTo(JsonNodeKind.Object));
            Assert.That(root.Position, Is.EqualTo("line 1, column 1"));
            Assert.That(root.Members[0].Value.Position, Is.EqualTo("line 1, column 7"));
        }

        [Test]
        public void ReadsEmptyContainersAndSurroundingWhitespace()
        {
            var root = new JsonParser(" \t\r\n{\"o\": {}, \"a\": []}\r\n ").ParseDocument();

            Assert.That(root.Position, Is.EqualTo("line 2, column 1"));
            Assert.That(root.Members[0].Value.Members, Is.Empty);
            Assert.That(root.Members[1].Value.Elements, Is.Empty);
        }

        [Test]
        public void AcceptsEightLevelsOfNesting()
        {
            var node = new JsonParser("[[[[[[[[1]]]]]]]]").ParseDocument();

            for (var i = 0; i < 8; i++)
            {
                Assert.That(node.Kind, Is.EqualTo(JsonNodeKind.Array));
                node = node.Elements[0];
            }

            Assert.That(node.Text, Is.EqualTo("1"));
        }

        [TestCase("", "unexpected end of input at end of input")]
        [TestCase("   ", "unexpected end of input at end of input")]
        [TestCase("{\"a\": 1,}", "expected a string key at line 1, column 9")]
        [TestCase("{\"a\": 1, \"a\": 2}", "duplicate key \"a\" at line 1, column 10")]
        [TestCase("{\"a\" 1}", "expected ':' at line 1, column 6")]
        [TestCase("{\"a\": 1", "expected ',' at end of input")]
        [TestCase("[1 2]", "expected ',' at line 1, column 4")]
        [TestCase("{} x", "unexpected text after the value at line 1, column 4")]
        [TestCase("[01]", "numbers must not have leading zeros at line 1, column 3")]
        [TestCase("1.", "expected a digit at end of input")]
        [TestCase("1e+", "expected a digit at end of input")]
        [TestCase("-x", "expected a digit at line 1, column 2")]
        [TestCase("+1", "unexpected character '+' at line 1, column 1")]
        [TestCase("tru", "invalid literal at line 1, column 1")]
        [TestCase("nul ", "invalid literal at line 1, column 1")]
        [TestCase("\"abc", "unterminated string at end of input")]
        [TestCase("\"a\\", "unterminated string at end of input")]
        [TestCase("\"a\nb\"", "control character in string at line 1, column 3")]
        [TestCase("\"\\x\"", "invalid escape '\\x' at line 1, column 3")]
        [TestCase("\"\\u12\"", "incomplete unicode escape at line 1, column 4")]
        [TestCase("\"\\u12G4\"", "invalid unicode escape at line 1, column 6")]
        [TestCase("{\r\n\"a\": x}", "unexpected character 'x' at line 2, column 6")]
        [TestCase("[[[[[[[[[1]]]]]]]]]", "nesting is too deep at line 1, column 9")]
        [TestCase("{\"a\": {\"b\": {\"c\": {\"d\": {\"e\": {\"f\": {\"g\": {\"h\": {}}}}}}}}}", "nesting is too deep at line 1, column 49")]
        public void RejectsMalformedTextWithThePosition(string text, string message)
        {
            var e = Assert.Throws<FormatException>(() => new JsonParser(text).ParseDocument());

            Assert.That(e.Message, Is.EqualTo(message));
        }
    }
}
