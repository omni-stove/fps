using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class FlatJsonObjectTests
    {
        [Test]
        public void ReadsStringValues()
        {
            const string json = "{\n  \"EOS_PRODUCT_ID\": \"abc\",\n  \"EOS_CLIENT_SECRET\" : \"s3cr3t\"\n}\n";

            var ok = FlatJsonObject.TryParse(json, out var values, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null);
            Assert.That(values, Has.Count.EqualTo(2));
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

        [TestCase("", "expected '{'")]
        [TestCase("[]", "expected '{'")]
        [TestCase("{\"k\": 1}", "only string values")]
        [TestCase("{\"k\": null}", "only string values")]
        [TestCase("{\"k\": \"v\",}", "expected '\"'")]
        [TestCase("{\"k\" \"v\"}", "expected ':'")]
        [TestCase("{\"k\": \"v\"", "expected '}'")]
        [TestCase("{\"k\": \"v}", "unterminated string")]
        [TestCase("{\"k\": \"\\x\"}", "invalid escape")]
        [TestCase("{\"k\": \"\\u12\"}", "invalid \\u escape")]
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
            Assert.That(error, Does.EndWith("at position 7"));
        }

        [Test]
        public void RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => FlatJsonObject.TryParse(null, out _, out _));
        }
    }
}
