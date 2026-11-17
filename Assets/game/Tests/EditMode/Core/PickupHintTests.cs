using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PickupHintTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _launcher = "00000000-0000-4000-8000-000000000003";
        private const string _interact = "Player/Interact";

        private static PickupCollector Armed(int maxWeapons = 0)
        {
            return new PickupCollector(Side.Alpha, 90, 100).WithWeapons(
                new[] { new PickupWeaponState(_rifle, 30, 90, false) },
                _rifle,
                maxWeapons);
        }

        [Test]
        public void AWeaponNotYetHeldIsTakenWithItsIdAndNameKey()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Weapon(_launcher, 4), Armed());

            Assert.That(PickupHint.TryFrom(outcome, " weapon.launcher ", _interact, out var hint), Is.True);
            Assert.That(hint, Is.EqualTo(new PickupHint(PickupHintAction.TakeWeapon, _launcher, "weapon.launcher", 0, _interact)));
        }

        [Test]
        public void AWeaponAtTheLimitIsSwapped()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Weapon(_launcher, 4), Armed(1));

            Assert.That(PickupHint.TryFrom(outcome, "weapon.launcher", _interact, out var hint), Is.True);
            Assert.That(hint.Action, Is.EqualTo(PickupHintAction.SwapWeapon));
            Assert.That(hint.ContentId, Is.EqualTo(_launcher));
        }

        [Test]
        public void AWeaponAlreadyHeldGivesAmmo()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Weapon(_rifle, 30), Armed());

            Assert.That(PickupHint.TryFrom(outcome, "weapon.rifle", _interact, out var hint), Is.True);
            Assert.That(hint, Is.EqualTo(new PickupHint(PickupHintAction.WeaponAmmo, _rifle, "weapon.rifle", 0, _interact)));
        }

        [Test]
        public void OtherKindsCarryNoContent()
        {
            Assert.That(PickupHint.TryFrom(PickupJudge.Judge(PickupSpec.Health(25), Armed()), "ignored", _interact, out var health), Is.True);
            Assert.That(health, Is.EqualTo(new PickupHint(PickupHintAction.Health, null, null, 0, _interact)));

            Assert.That(PickupHint.TryFrom(PickupJudge.Judge(PickupSpec.ReserveAmmo(10), Armed()), null, _interact, out var ammo), Is.True);
            Assert.That(ammo.Action, Is.EqualTo(PickupHintAction.ReserveAmmo));

            Assert.That(PickupHint.TryFrom(PickupJudge.Judge(PickupSpec.Tag(1, Side.Bravo), Armed()), null, _interact, out var tag), Is.True);
            Assert.That(tag.Action, Is.EqualTo(PickupHintAction.Tag));
            Assert.That(tag.ContentId, Is.Null);
            Assert.That(tag.NameKey, Is.Empty);
        }

        [Test]
        public void NothingIsHintedForWhatCannotBePickedUp()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Health(25), new PickupCollector(null, 100, 100));

            Assert.That(PickupHint.TryFrom(outcome, null, _interact, out var hint), Is.False);
            Assert.That(hint, Is.EqualTo(default(PickupHint)));
        }

        [Test]
        public void NullsBecomeEmptyAndEqualHintsMatch()
        {
            var hint = new PickupHint(PickupHintAction.Armor, null, null, 0, null);

            Assert.That(hint.NameKey, Is.Empty);
            Assert.That(hint.InputActionPath, Is.Empty);
            Assert.That(hint, Is.EqualTo(new PickupHint(PickupHintAction.Armor, null, "  ", 0, string.Empty)));
            Assert.That(hint == new PickupHint(PickupHintAction.Armor, null, null, 1, null), Is.False);
        }

        [Test]
        public void AnAmmoCrateHintMatchesByRoomAndInput()
        {
            Assert.That(new AmmoCrateHint(true, null).InputActionPath, Is.Empty);
            var crate = new AmmoCrateHint(true, _interact);
            var same = new AmmoCrateHint(true, _interact);
            Assert.That(crate, Is.EqualTo(same));
            Assert.That(new AmmoCrateHint(true, _interact) == new AmmoCrateHint(false, _interact), Is.False);
        }
    }
}
