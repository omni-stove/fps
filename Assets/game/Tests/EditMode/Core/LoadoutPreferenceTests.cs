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

        private static readonly LoadoutPresetSpec _spec = new LoadoutPresetSpec(3, 8);

        [Test]
        public void TheVersionIs3()
        {
            Assert.That(LoadoutPreference.Version, Is.EqualTo(3));
            Assert.That(LoadoutPreference.LastSingleLoadoutVersion, Is.EqualTo(2));
            Assert.That(LoadoutPreference.OldestReadableVersion, Is.EqualTo(1));
        }

        [Test]
        public void RoundTripsThePresetsTheirNamesAndTheActiveOne()
        {
            var book = new LoadoutPresetBook(_spec);
            book.Add("Rush", new LoadoutChoice(new[] { _rifle, _handgun }, new[] { _beacon, string.Empty }));
            book.Add(string.Empty, new LoadoutChoice(new[] { _handgun }));
            book.Select(1);

            var json = LoadoutPreference.ToJson(book);

            Assert.That(json, Is.EqualTo(
                "{\"version\":3,\"active\":1,\"presets\":["
                + "{\"name\":\"Rush\",\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"],\"abilities\":[\"" + _beacon + "\",\"\"]},"
                + "{\"name\":\"\",\"weapons\":[\"" + _handgun + "\"]}]}"));
            var read = LoadoutPreference.FromJson(json, _spec, out var problem, out var dropped);
            Assert.That(problem, Is.Null);
            Assert.That(dropped, Is.EqualTo(0));
            Assert.That(read.Count, Is.EqualTo(2));
            Assert.That(read.ActiveIndex, Is.EqualTo(1));
            Assert.That(read[0].Name, Is.EqualTo("Rush"));
            Assert.That(read[0].Choice.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(read[0].Choice.AbilityIds, Is.EqualTo(new[] { _beacon, string.Empty }));
            Assert.That(read[1].HasName, Is.False);
            Assert.That(read[1].Choice.WeaponIds, Is.EqualTo(new[] { _handgun }));
            Assert.That(read[1].Choice.AbilityIds, Is.Null, "a preset saved without abilities keeps the loadout's abilities");
        }

        [Test]
        public void AnEmptyBookIsSavedAndReadBackEmpty()
        {
            var json = LoadoutPreference.ToJson(new LoadoutPresetBook(_spec));

            Assert.That(json, Is.EqualTo("{\"version\":3,\"active\":0,\"presets\":[]}"));
            var read = LoadoutPreference.FromJson(json, _spec, out var problem);
            Assert.That(problem, Is.Null);
            Assert.That(read.Count, Is.EqualTo(0));
            Assert.That(read.Active, Is.Null);
        }

        [Test]
        public void AVersion2SaveBecomesTheFirstUnnamedPreset()
        {
            var read = LoadoutPreference.FromJson(
                "{\"version\":2,\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"],\"abilities\":[\"" + _beacon + "\",\"\"]}",
                _spec,
                out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(read.Count, Is.EqualTo(1));
            Assert.That(read.ActiveIndex, Is.EqualTo(0));
            Assert.That(read.Active.Name, Is.Empty);
            Assert.That(read.Active.Choice.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(read.Active.Choice.AbilityIds, Is.EqualTo(new[] { _beacon, string.Empty }));
        }

        [Test]
        public void AVersion2SaveIsWrittenBackAsVersion3()
        {
            var read = LoadoutPreference.FromJson("{\"version\":2,\"weapons\":[\"" + _rifle + "\"]}", _spec, out _);

            Assert.That(LoadoutPreference.ToJson(read), Is.EqualTo(
                "{\"version\":3,\"active\":0,\"presets\":[{\"name\":\"\",\"weapons\":[\"" + _rifle + "\"]}]}"));
        }

        [Test]
        public void AVersion1SaveRestoresTheWeaponsAndNoAbilities()
        {
            var read = LoadoutPreference.FromJson("{\"version\":1,\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"]}", _spec, out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(read.Count, Is.EqualTo(1));
            Assert.That(read.Active.Choice.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(read.Active.Choice.AbilityIds, Is.Null);
        }

        // 1 の版に abilities は無かったので、あっても読まない
        [Test]
        public void AVersion1SaveIgnoresAbilities()
        {
            var read = LoadoutPreference.FromJson(
                "{\"version\":1,\"weapons\":[\"" + _rifle + "\"],\"abilities\":[\"" + _beacon + "\"]}", _spec, out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(read.Active.Choice.AbilityIds, Is.Null);
        }

        [Test]
        public void PresetsBeyondTheLimitAreDroppedFromTheEnd()
        {
            var json = "{\"version\":3,\"active\":1,\"presets\":["
                + Preset("A", _rifle) + "," + Preset("B", _handgun) + "," + Preset("C", _rifle) + "," + Preset("D", _handgun) + "]}";

            var read = LoadoutPreference.FromJson(json, _spec, out var problem, out var dropped);

            Assert.That(problem, Is.Null);
            Assert.That(dropped, Is.EqualTo(1));
            Assert.That(read.Count, Is.EqualTo(3));
            Assert.That(read[2].Name, Is.EqualTo("C"));
            Assert.That(read.ActiveIndex, Is.EqualTo(1));
            Assert.That(read.IsFull, Is.True);
        }

        [Test]
        public void ADroppedActivePresetFallsBackToTheFirst()
        {
            var json = "{\"version\":3,\"active\":3,\"presets\":["
                + Preset("A", _rifle) + "," + Preset("B", _handgun) + "," + Preset("C", _rifle) + "," + Preset("D", _handgun) + "]}";

            var read = LoadoutPreference.FromJson(json, _spec, out var problem, out var dropped);

            Assert.That(problem, Is.Null);
            Assert.That(dropped, Is.EqualTo(1));
            Assert.That(read.ActiveIndex, Is.EqualTo(0));
        }

        [Test]
        public void ANameLongerThanTheLimitIsCutInsteadOfDroppingThePreset()
        {
            var read = LoadoutPreference.FromJson(
                "{\"version\":3,\"presets\":[" + Preset("  Long range rifle ", _rifle) + "]}", _spec, out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(read.ActiveIndex, Is.EqualTo(0), "a save without active uses the first preset");
            Assert.That(read[0].Name, Is.EqualTo("Long ran"));
        }

        [Test]
        public void EscapesIdsAndNamesThatNeedIt()
        {
            var odd = "quote\" back\\slash";
            var book = new LoadoutPresetBook(new LoadoutPresetSpec(1, 32));
            book.Add("a \"b\" \\c", new LoadoutChoice(new[] { odd }, new[] { odd }));

            var restored = LoadoutPreference.FromJson(LoadoutPreference.ToJson(book), book.Spec, out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(restored[0].Name, Is.EqualTo("a \"b\" \\c"));
            Assert.That(restored[0].Choice.WeaponIds, Is.EqualTo(new[] { odd }));
            Assert.That(restored[0].Choice.AbilityIds, Is.EqualTo(new[] { odd }));
        }

        [Test]
        public void ToJsonAndFromJsonRejectMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => LoadoutPreference.ToJson(null));
            Assert.Throws<ArgumentNullException>(() => LoadoutPreference.FromJson("{}", null, out _));
            Assert.Throws<ArgumentNullException>(() => new LoadoutChoice(null));
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
        [TestCase("{\"version\":4,\"presets\":[]}", "unknown version")]
        [TestCase("{\"version\":\"2\",\"weapons\":[\"a\"]}", "unknown version")]
        [TestCase("{\"version\":1}", "no weapons")]
        [TestCase("{\"version\":2,\"weapons\":[]}", "no weapons")]
        [TestCase("{\"version\":2,\"weapons\":\"a\"}", "no weapons")]
        [TestCase("{\"version\":2,\"weapons\":[\"a\",1]}", "slot 1")]
        [TestCase("{\"version\":1,\"weapons\":[\"\"]}", "slot 0")]
        [TestCase("{\"version\":2,\"weapons\":[\"a\"],\"abilities\":\"b\"}", "not a list")]
        [TestCase("{\"version\":2,\"weapons\":[\"a\"],\"abilities\":[\"b\",1]}", "ability slot 1")]
        [TestCase("{\"version\":2,\"weapons\":[\"a\"],\"abilities\":[null]}", "ability slot 0")]
        [TestCase("{\"version\":3}", "no list of presets")]
        [TestCase("{\"version\":3,\"presets\":{}}", "no list of presets")]
        [TestCase("{\"version\":3,\"presets\":[1]}", "preset 0: it is not a JSON object")]
        [TestCase("{\"version\":3,\"presets\":[{\"name\":1,\"weapons\":[\"a\"]}]}", "preset 0: its name is not text")]
        [TestCase("{\"version\":3,\"presets\":[{\"name\":\"a\\nb\",\"weapons\":[\"a\"]}]}", "preset 0: a loadout preset name must not contain control characters")]
        [TestCase("{\"version\":3,\"presets\":[{\"name\":\"a\",\"weapons\":[\"a\"]},{\"weapons\":[]}]}", "preset 1: the saved loadout has no weapons")]
        [TestCase("{\"version\":3,\"presets\":[{\"weapons\":[\"a\"],\"abilities\":[2]}]}", "preset 0: ability slot 0")]
        [TestCase("{\"version\":3,\"active\":1,\"presets\":[{\"weapons\":[\"a\"]}]}", "active preset")]
        [TestCase("{\"version\":3,\"active\":-1,\"presets\":[{\"weapons\":[\"a\"]}]}", "active preset")]
        [TestCase("{\"version\":3,\"active\":\"0\",\"presets\":[{\"weapons\":[\"a\"]}]}", "active preset")]
        public void FromJsonRejectsBrokenText(string text, string expected)
        {
            Assert.That(LoadoutPreference.FromJson(text, _spec, out var problem), Is.Null);
            Assert.That(problem, Does.Contain(expected));
        }

        private static string Preset(string name, string weaponId)
        {
            return "{\"name\":\"" + name + "\",\"weapons\":[\"" + weaponId + "\"]}";
        }
    }
}
