using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LoadoutPreferenceTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";

        [Test]
        public void RoundTripsTheIdsInSlotOrder()
        {
            var json = LoadoutPreference.ToJson(new[] { _rifle, _handgun });

            Assert.That(json, Is.EqualTo("{\"version\":1,\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"]}"));
            Assert.That(LoadoutPreference.FromJson(json, out var problem), Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(problem, Is.Null);
        }

        [Test]
        public void EscapesIdsThatNeedIt()
        {
            var odd = "quote\" back\\slash";

            var restored = LoadoutPreference.FromJson(LoadoutPreference.ToJson(new[] { odd }), out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(restored, Is.EqualTo(new[] { odd }));
        }

        [Test]
        public void ToJsonRejectsEmptyLoadouts()
        {
            Assert.Throws<ArgumentNullException>(() => LoadoutPreference.ToJson(null));
            Assert.Throws<ArgumentException>(() => LoadoutPreference.ToJson(new string[0]));
            Assert.Throws<ArgumentException>(() => LoadoutPreference.ToJson(new[] { _rifle, string.Empty }));
            Assert.Throws<ArgumentException>(() => LoadoutPreference.ToJson(new[] { _rifle, null }));
        }

        [TestCase(null, "empty")]
        [TestCase("", "empty")]
        [TestCase("{not json", "not valid JSON")]
        [TestCase("[\"a\"]", "not a JSON object")]
        [TestCase("{\"weapons\":[\"a\"]}", "unknown version")]
        [TestCase("{\"version\":2,\"weapons\":[\"a\"]}", "unknown version")]
        [TestCase("{\"version\":1}", "no weapons")]
        [TestCase("{\"version\":1,\"weapons\":[]}", "no weapons")]
        [TestCase("{\"version\":1,\"weapons\":\"a\"}", "no weapons")]
        [TestCase("{\"version\":1,\"weapons\":[\"a\",1]}", "slot 1")]
        [TestCase("{\"version\":1,\"weapons\":[\"\"]}", "slot 0")]
        public void FromJsonRejectsBrokenText(string text, string expected)
        {
            Assert.That(LoadoutPreference.FromJson(text, out var problem), Is.Null);
            Assert.That(problem, Does.Contain(expected));
        }
    }
}
