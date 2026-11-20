using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchDisplayNameSettingsTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private static NetMatchFile File(NetMatchSettings settings)
        {
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            return new NetMatchFile(
                "TeamDeathmatch",
                GameRules.Default,
                settings,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null);
        }

        private static string Text(string match)
        {
            return "{\"match\": " + match + ", \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";
        }

        [Test]
        public void TheDefaultIsSixteenCharacters()
        {
            Assert.That(NetMatchSettings.Default.MaxDisplayNameLength, Is.EqualTo(16));
            Assert.That(NetMatchSettings.DefaultMaxDisplayNameLength, Is.EqualTo(16));
        }

        [TestCase(1)]
        [TestCase(21)]
        public void ALimitWithinTheBytesIsKept(int maxLength)
        {
            Assert.That(new NetMatchSettings(maxDisplayNameLength: maxLength).MaxDisplayNameLength, Is.EqualTo(maxLength));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(22)]
        public void ALimitOutsideTheBytesIsRefused(int maxLength)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new NetMatchSettings(maxDisplayNameLength: maxLength));

            Assert.That(e.ParamName, Is.EqualTo("maxDisplayNameLength"));
        }

        [Test]
        public void TheRulesFileChangesTheLimit()
        {
            var read = NetMatchFile.Parse(Text("{\"maxDisplayNameLength\": 10}"), "names.json").Settings;

            Assert.That(read.MaxDisplayNameLength, Is.EqualTo(10));
            Assert.That(PlayerDisplayName.Sanitize("Eleven Char", read.MaxDisplayNameLength), Is.Empty);
            Assert.That(PlayerDisplayName.Sanitize("Ten  Chars", read.MaxDisplayNameLength), Is.EqualTo("Ten  Chars"));
        }

        [Test]
        public void ARulesFileWithoutTheItemUsesTheDefault()
        {
            var read = NetMatchFile.Parse(Text("{\"scoreLimit\": 5}"), "old.json").Settings;

            Assert.That(read.MaxDisplayNameLength, Is.EqualTo(NetMatchSettings.DefaultMaxDisplayNameLength));
        }

        [TestCase("{\"maxDisplayNameLength\": 0}")]
        [TestCase("{\"maxDisplayNameLength\": 22}")]
        [TestCase("{\"maxDisplayNameLength\": \"16\"}")]
        public void ABadLimitNamesTheItem(string match)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "bad.json"));

            Assert.That(e.Item, Is.EqualTo("match.maxDisplayNameLength"));
        }

        [Test]
        public void AChangedLimitIsWrittenAndReadBack()
        {
            var text = NetMatchFile.Write(File(new NetMatchSettings(maxDisplayNameLength: 12)));

            Assert.That(text, Does.Contain("\"maxDisplayNameLength\": 12"));
            Assert.That(NetMatchFile.Parse(text, "names.json").Settings.MaxDisplayNameLength, Is.EqualTo(12));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "names.json")), Is.EqualTo(text));
        }

        [Test]
        public void TheDefaultLimitIsLeftOutSoFilesWrittenBeforeTheItemStayTheSame()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default));

            Assert.That(text, Does.Not.Contain("maxDisplayNameLength"));
            Assert.That(NetMatchFile.Parse(text, "default.json").Settings.MaxDisplayNameLength, Is.EqualTo(16));
        }

        [Test]
        public void WithPrivacyKeepsTheLimit()
        {
            var changed = new NetMatchSettings(maxDisplayNameLength: 8).WithPrivacy(true, "ABC234");

            Assert.That(changed.MaxDisplayNameLength, Is.EqualTo(8));
        }
    }
}
