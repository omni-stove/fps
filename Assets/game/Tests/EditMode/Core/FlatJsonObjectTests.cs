using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class FlatJsonObjectTests
    {
        [Test]
        public void ReadsStringValues()
        {
            const string json = "{\n  \"EOS_PRODUCT_ID\": \"abc\",\n  \"EOS_CLIENT_SECRET\" : \"s3cr3t\"\n}\n";

            var ok = FlatJsonObject.TryParse(json, out var values, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null);
            Assert.That(values.Count, Is.EqualTo(2));
            Assert.That(values["EOS_PRODUCT_ID"], Is.EqualTo("abc"));
            Assert.That(values["EOS_CLIENT_SECRET"], Is.EqualTo("s3cr3t"));
        }

        [Test]
        public void ReadsEmptyObjectWithBom()
        {
            var ok = FlatJsonObject.TryParse("\uFEFF { } ", out var values, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(values, Is.Empty);
        }

        [Test]
        public void DecodesEscapes()
        {
            var ok = FlatJsonObject.TryParse("{\"k\": \"a\\\"b\\\\c\\/d\\n\\u0041\"}", out var values, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(values["k"], Is.EqualTo("a\"b\\c/d\nA"));
        }

        [TestCase("", "unexpected end of input")]
        [TestCase("[]", "expected '{'")]
        [TestCase("{\"k\": 1}", "only string values")]
        [TestCase("{\"k\": null}", "only string values")]
        [TestCase("{\"k\": {\"a\": \"b\"}}", "only string values")]
        [TestCase("{\"k\": \"v\",}", "expected a string key")]
        [TestCase("{\"k\" \"v\"}", "expected ':'")]
        [TestCase("{\"k\": \"v\"", "expected ','")]
        [TestCase("{\"k\": \"v}", "unterminated string")]
        [TestCase("{\"k\": \"\\x\"}", "invalid escape")]
        [TestCase("{\"k\": \"\\u12\"}", "invalid unicode escape")]
        [TestCase("{\"k\": \"v\"} extra", "unexpected text")]
        [TestCase("{\"k\": \"v\", \"k\": \"w\"}", "duplicate key")]
        public void RejectsInvalidJson(string json, string expected)
        {
            var ok = FlatJsonObject.TryParse(json, out var values, out var error);

            Assert.That(ok, Is.False);
            Assert.That(values, Is.Null);
            Assert.That(error, Does.Contain(expected));
        }

        [Test]
        public void ReportsPosition()
        {
            var ok = FlatJsonObject.TryParse("{\"k\": 1}", out _, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.EndWith("at line 1, column 7"));
        }

        [Test]
        public void ReportsLineAndColumnAcrossLines()
        {
            var ok = FlatJsonObject.TryParse("{\n  \"a\": \"x\",\n  \"b\": true\n}", out _, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Is.EqualTo("only string values are supported for \"b\" at line 3, column 8"));
        }

        [Test]
        public void ReportsPositionOfNonObjectRoot()
        {
            var ok = FlatJsonObject.TryParse("  []", out _, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Is.EqualTo("expected '{' at line 1, column 3"));
        }

        [Test]
        public void RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => FlatJsonObject.TryParse(null, out _, out _));
        }

        [Test]
        public void FormatsPairsInOrderOnOneLine()
        {
            var text = FlatJsonObject.Format(new[] { Pair("type", "kill"), Pair("killerId", "0") });

            Assert.That(text, Is.EqualTo("{\"type\":\"kill\",\"killerId\":\"0\"}"));
        }

        [Test]
        public void FormatsEmptyObject()
        {
            Assert.That(FlatJsonObject.Format(Array.Empty<KeyValuePair<string, string>>()), Is.EqualTo("{}"));
        }

        [Test]
        public void FormatEscapesSoTheLineStaysOneLine()
        {
            var text = FlatJsonObject.Format(new[] { Pair("k\"", "a\"b\\c\nd\re\tf\u0001g\u2028h") });

            Assert.That(text, Is.EqualTo("{\"k\\\"\":\"a\\\"b\\\\c\\nd\\re\\tf\\u0001g\\u2028h\"}"));
        }

        [Test]
        public void FormatKeepsSurrogatePairsAndEscapesLoneSurrogates()
        {
            var text = FlatJsonObject.Format(new[] { Pair("k", "😀|\uD800") });

            Assert.That(text, Is.EqualTo("{\"k\":\"😀|\\ud800\"}"));
        }

        [Test]
        public void FormattedTextReadsBack()
        {
            var pairs = new[] { Pair("a", "x\"y\\z\n\u0000"), Pair("b", ""), Pair("c", "/\u2029") };

            var ok = FlatJsonObject.TryParse(FlatJsonObject.Format(pairs), out var values, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(values.Count, Is.EqualTo(3));
            Assert.That(values["a"], Is.EqualTo("x\"y\\z\n\u0000"));
            Assert.That(values["b"], Is.Empty);
            Assert.That(values["c"], Is.EqualTo("/\u2029"));
        }

        [Test]
        public void FormatRejectsNullsAndDuplicateKeys()
        {
            Assert.Throws<ArgumentNullException>(() => FlatJsonObject.Format(null));
            Assert.Throws<ArgumentNullException>(() => FlatJsonObject.Format(new[] { Pair(null, "v") }));
            Assert.Throws<ArgumentNullException>(() => FlatJsonObject.Format(new[] { Pair("k", null) }));
            Assert.Throws<ArgumentException>(() => FlatJsonObject.Format(new[] { Pair("k", "v"), Pair("k", "w") }));
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }
    }
}
