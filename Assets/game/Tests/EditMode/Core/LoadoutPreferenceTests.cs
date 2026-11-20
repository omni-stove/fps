using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LoadoutPreferenceTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";
        private const string _beacon = "00000000-0000-4000-8000-0000000000b1";

        [Test]
        public void TheVersionIs2()
        {
            Assert.That(LoadoutPreference.Version, Is.EqualTo(2));
            Assert.That(LoadoutPreference.OldestReadableVersion, Is.EqualTo(1));
        }

        [Test]
        public void RoundTripsTheWeaponsAndAbilitiesInSlotOrder()
        {
            var json = LoadoutPreference.ToJson(new LoadoutChoice(new[] { _rifle, _handgun }, new[] { _beacon, string.Empty }));

            Assert.That(json, Is.EqualTo(
                "{\"version\":2,\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"],\"abilities\":[\"" + _beacon + "\",\"\"]}"));
            var read = LoadoutPreference.FromJson(json, out var problem);
            Assert.That(problem, Is.Null);
            Assert.That(read.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(read.AbilityIds, Is.EqualTo(new[] { _beacon, string.Empty }));
        }

        [Test]
        public void WeaponsWithoutAbilitiesLeaveTheAbilitiesOut()
        {
            var json = LoadoutPreference.ToJson(new LoadoutChoice(new[] { _rifle, _handgun }));

            Assert.That(json, Is.EqualTo("{\"version\":2,\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"]}"));
            var read = LoadoutPreference.FromJson(json, out var problem);
            Assert.That(problem, Is.Null);
            Assert.That(read.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(read.AbilityIds, Is.Null);
        }

        [Test]
        public void AllNoneAbilitiesAreKeptApartFromNoAbilities()
        {
            var read = LoadoutPreference.FromJson(
                LoadoutPreference.ToJson(new LoadoutChoice(new[] { _rifle }, new[] { string.Empty, string.Empty })), out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(read.AbilityIds, Is.EqualTo(new[] { string.Empty, string.Empty }));
        }

        [Test]
        public void AVersion1SaveRestoresTheWeaponsAndNoAbilities()
        {
            var read = LoadoutPreference.FromJson("{\"version\":1,\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"]}", out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(read.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(read.AbilityIds, Is.Null);
        }

        // 1 の版に abilities は無かったので、あっても読まない
        [Test]
        public void AVersion1SaveIgnoresAbilities()
        {
            var read = LoadoutPreference.FromJson("{\"version\":1,\"weapons\":[\"" + _rifle + "\"],\"abilities\":[\"" + _beacon + "\"]}", out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(read.AbilityIds, Is.Null);
        }

        [Test]
        public void EscapesIdsThatNeedIt()
        {
            var odd = "quote\" back\\slash";

            var restored = LoadoutPreference.FromJson(LoadoutPreference.ToJson(new LoadoutChoice(new[] { odd }, new[] { odd })), out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(restored.WeaponIds, Is.EqualTo(new[] { odd }));
            Assert.That(restored.AbilityIds, Is.EqualTo(new[] { odd }));
        }

        [Test]
        public void ToJsonRejectsEmptyLoadouts()
        {
            Assert.Throws<ArgumentNullException>(() => LoadoutPreference.ToJson(null));
            Assert.Throws<ArgumentNullException>(() => new LoadoutChoice(null));
            Assert.Throws<ArgumentException>(() => LoadoutPreference.ToJson(new LoadoutChoice(Array.Empty<string>())));
            Assert.Throws<ArgumentException>(() => LoadoutPreference.ToJson(new LoadoutChoice(new[] { _rifle, string.Empty })));
            Assert.Throws<ArgumentException>(() => LoadoutPreference.ToJson(new LoadoutChoice(new[] { _rifle, null })));
            Assert.Throws<ArgumentException>(() => new LoadoutChoice(new[] { _rifle }, new[] { _beacon, null }));
        }

        [Test]
        public void TheChoiceKeepsItsOwnCopy()
        {
            var weapons = new[] { _rifle };
            var abilities = new[] { _beacon };
            var choice = new LoadoutChoice(weapons, abilities);

            weapons[0] = _handgun;
            abilities[0] = string.Empty;

            Assert.That(choice.WeaponIds, Is.EqualTo(new[] { _rifle }));
            Assert.That(choice.AbilityIds, Is.EqualTo(new[] { _beacon }));
        }

        [TestCase(null, "empty")]
        [TestCase("", "empty")]
        [TestCase("{not json", "not valid JSON")]
        [TestCase("[\"a\"]", "not a JSON object")]
        [TestCase("{\"weapons\":[\"a\"]}", "unknown version")]
        [TestCase("{\"version\":0,\"weapons\":[\"a\"]}", "unknown version")]
        [TestCase("{\"version\":3,\"weapons\":[\"a\"]}", "unknown version")]
        [TestCase("{\"version\":\"2\",\"weapons\":[\"a\"]}", "unknown version")]
        [TestCase("{\"version\":1}", "no weapons")]
        [TestCase("{\"version\":2,\"weapons\":[]}", "no weapons")]
        [TestCase("{\"version\":2,\"weapons\":\"a\"}", "no weapons")]
        [TestCase("{\"version\":2,\"weapons\":[\"a\",1]}", "slot 1")]
        [TestCase("{\"version\":1,\"weapons\":[\"\"]}", "slot 0")]
        [TestCase("{\"version\":2,\"weapons\":[\"a\"],\"abilities\":\"b\"}", "not a list")]
        [TestCase("{\"version\":2,\"weapons\":[\"a\"],\"abilities\":[\"b\",1]}", "ability slot 1")]
        [TestCase("{\"version\":2,\"weapons\":[\"a\"],\"abilities\":[null]}", "ability slot 0")]
        public void FromJsonRejectsBrokenText(string text, string expected)
        {
            Assert.That(LoadoutPreference.FromJson(text, out var problem), Is.Null);
            Assert.That(problem, Does.Contain(expected));
        }
    }
}
