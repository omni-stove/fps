using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class PickupPromptTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _launcher = "00000000-0000-4000-8000-000000000003";
        private const string _interact = "Player/Interact";

        private static readonly LocalizedText _launcherName = new LocalizedText("weapon.launcher");

        private static PickupHint Hint(PickupHintAction action, string contentId = null, string nameKey = null, int count = 0)
        {
            return new PickupHint(action, contentId, nameKey, count, _interact);
        }

        [Test]
        public void AWeaponNotYetHeldNestsTheWeaponNameAfterTheKey()
        {
            Assert.That(PickupPrompt.TryDescribe(Hint(PickupHintAction.TakeWeapon, _launcher, "weapon.launcher"), "E", out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(PickupPrompt.WeaponKey, "E", _launcherName)));
        }

        [Test]
        public void AWeaponAtTheLimitSaysItSwaps()
        {
            Assert.That(PickupPrompt.TryDescribe(Hint(PickupHintAction.SwapWeapon, _launcher, "weapon.launcher"), "E", out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(PickupPrompt.WeaponSwapKey, "E", _launcherName)));
        }

        [Test]
        public void AWeaponAlreadyHeldSaysItGivesAmmo()
        {
            Assert.That(PickupPrompt.TryDescribe(Hint(PickupHintAction.WeaponAmmo, _rifle, "weapon.rifle"), "E", out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(PickupPrompt.WeaponAmmoKey, "E", new LocalizedText("weapon.rifle"))));
        }

        [Test]
        public void AWeaponWithoutANameKeyIsNamedByItsId()
        {
            Assert.That(PickupPrompt.TryDescribe(Hint(PickupHintAction.TakeWeapon, _launcher), "E", out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(PickupPrompt.WeaponKey, "E", LocalizedText.Literal(_launcher))));
        }

        [Test]
        public void OtherKindsUseTheirOwnKeyWithOnlyTheInteractKey()
        {
            Assert.That(PickupPrompt.TryDescribe(Hint(PickupHintAction.Health), "E", out var health), Is.True);
            Assert.That(health, Is.EqualTo(new LocalizedText(PickupPrompt.HealthKey, "E")));

            Assert.That(PickupPrompt.TryDescribe(Hint(PickupHintAction.ReserveAmmo), "E", out var ammo), Is.True);
            Assert.That(ammo, Is.EqualTo(new LocalizedText(PickupPrompt.ReserveAmmoKey, "E")));

            Assert.That(PickupPrompt.TryDescribe(Hint(PickupHintAction.Armor), "E", out var armor), Is.True);
            Assert.That(armor, Is.EqualTo(new LocalizedText(PickupPrompt.ArmorKey, "E")));

            Assert.That(PickupPrompt.TryDescribe(Hint(PickupHintAction.Tag), "E", out var tag), Is.True);
            Assert.That(tag, Is.EqualTo(new LocalizedText(PickupPrompt.TagKey, "E")));
        }

        [Test]
        public void AnItemNamesTheItemAndTheCount()
        {
            Assert.That(PickupPrompt.TryDescribe(Hint(PickupHintAction.Item, "item-id", "item.herb", 2), "E", out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(PickupPrompt.ItemKey, "E", new LocalizedText("item.herb"), "2")));
        }

        [Test]
        public void ANullKeyLabelBecomesEmpty()
        {
            Assert.That(PickupPrompt.TryDescribe(Hint(PickupHintAction.Health), null, out var text), Is.True);
            Assert.That(text, Is.EqualTo(new LocalizedText(PickupPrompt.HealthKey, string.Empty)));
        }

        [Test]
        public void NothingIsShownForAnUnknownAction()
        {
            Assert.That(PickupPrompt.TryDescribe(Hint((PickupHintAction)99), "E", out var text), Is.False);
            Assert.That(text.IsEmpty, Is.True);
        }

        [Test]
        public void TheWeaponNameIsTheKeyOrTheIdWhenThereIsNoKey()
        {
            Assert.That(DisplayNames.Of(" weapon.rifle ", _rifle), Is.EqualTo(new LocalizedText("weapon.rifle")));
            Assert.That(DisplayNames.Of("  ", _rifle), Is.EqualTo(LocalizedText.Literal(_rifle)));
            Assert.That(DisplayNames.Of(null, null), Is.EqualTo(LocalizedText.Literal(string.Empty)));
        }

        [Test]
        public void EveryPickupTextExistsInTheTable()
        {
            foreach (var key in new[]
                     {
                         PickupPrompt.WeaponKey, PickupPrompt.WeaponSwapKey, PickupPrompt.WeaponAmmoKey, PickupPrompt.ReserveAmmoKey,
                         PickupPrompt.HealthKey, PickupPrompt.ArmorKey, PickupPrompt.TagKey, PickupPrompt.ItemKey,
                     })
            {
                Assert.That(Localizer.Resolve(new LocalizedText(key, "E", LocalizedText.Literal("x"), "1")), Does.Contain("E"), key);
            }
        }
    }
}
