using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PickupPromptTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _launcher = "00000000-0000-4000-8000-000000000003";

        private static readonly LocalizedText _launcherName = new LocalizedText("weapon.launcher");

        private static PickupCollector Armed()
        {
            return new PickupCollector(Side.Alpha, 90, 100).WithWeapons(
                new[] { new PickupWeaponState(_rifle, 30, 90, false) },
                _rifle);
        }

        [Test]
        public void AWeaponNotYetHeldNestsTheWeaponNameAfterTheKey()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Weapon(_launcher, 4), Armed());

            Assert.That(PickupPrompt.TryDescribe(outcome, "E", _launcherName, out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(PickupPrompt.WeaponKey, "E", _launcherName)));
        }

        [Test]
        public void AWeaponAtTheLimitSaysItSwaps()
        {
            var full = new PickupCollector(Side.Alpha, 90, 100).WithWeapons(
                new[] { new PickupWeaponState(_rifle, 30, 90, false) },
                _rifle,
                1);
            var outcome = PickupJudge.Judge(PickupSpec.Weapon(_launcher, 4), full);

            Assert.That(PickupPrompt.TryDescribe(outcome, "E", _launcherName, out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(PickupPrompt.WeaponSwapKey, "E", _launcherName)));
        }

        [Test]
        public void AWeaponAlreadyHeldSaysItGivesAmmo()
        {
            var rifleName = new LocalizedText("weapon.rifle");
            var outcome = PickupJudge.Judge(PickupSpec.Weapon(_rifle, 30), Armed());

            Assert.That(PickupPrompt.TryDescribe(outcome, "E", rifleName, out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(PickupPrompt.WeaponAmmoKey, "E", rifleName)));
        }

        [Test]
        public void OtherKindsUseTheirOwnKeyWithOnlyTheInteractKey()
        {
            Assert.That(PickupPrompt.TryDescribe(PickupJudge.Judge(PickupSpec.Health(25), Armed()), "E", default, out var health), Is.True);
            Assert.That(health, Is.EqualTo(new LocalizedText(PickupPrompt.HealthKey, "E")));

            Assert.That(PickupPrompt.TryDescribe(PickupJudge.Judge(PickupSpec.ReserveAmmo(10), Armed()), "E", default, out var ammo), Is.True);
            Assert.That(ammo, Is.EqualTo(new LocalizedText(PickupPrompt.ReserveAmmoKey, "E")));

            Assert.That(PickupPrompt.TryDescribe(PickupJudge.Judge(PickupSpec.Tag(1, Side.Bravo), Armed()), "E", default, out var tag), Is.True);
            Assert.That(tag, Is.EqualTo(new LocalizedText(PickupPrompt.TagKey, "E")));
        }

        [Test]
        public void ANullKeyLabelBecomesEmpty()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Health(25), Armed());

            Assert.That(PickupPrompt.TryDescribe(outcome, null, default, out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(PickupPrompt.HealthKey, string.Empty)));
        }

        [Test]
        public void NothingIsShownForWhatCannotBePickedUp()
        {
            var full = new PickupCollector(null, 100, 100);
            var outcome = PickupJudge.Judge(PickupSpec.Health(25), full);

            Assert.That(PickupPrompt.TryDescribe(outcome, "E", default, out var text), Is.False);
            Assert.That(text.IsEmpty, Is.True);
        }

        [Test]
        public void TheWeaponNameIsTheKeyOrTheIdWhenThereIsNoKey()
        {
            Assert.That(WeaponSpec.NameOf(_rifle, " weapon.rifle "), Is.EqualTo(new LocalizedText("weapon.rifle")));
            Assert.That(WeaponSpec.NameOf(_rifle, "  "), Is.EqualTo(LocalizedText.Literal(_rifle)));
            Assert.That(WeaponSpec.NameOf(null, null), Is.EqualTo(LocalizedText.Literal(string.Empty)));
        }
    }
}
