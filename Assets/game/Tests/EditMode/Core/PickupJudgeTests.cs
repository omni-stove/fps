using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PickupJudgeTests
    {
        private const string Rifle = "00000000-0000-4000-8000-000000000001";
        private const string Handgun = "00000000-0000-4000-8000-000000000002";
        private const string Launcher = "00000000-0000-4000-8000-000000000003";

        private static PickupCollector Healthy(Side? side = null)
        {
            return new PickupCollector(side, 100, 100);
        }

        private static PickupCollector Armed(int rifleReserve = 30, int handgunReserve = 20, string current = Rifle, bool infinite = false)
        {
            return new PickupCollector(Side.Alpha, 100, 100).WithWeapons(
                new[]
                {
                    new PickupWeaponState(Rifle, rifleReserve, 90, infinite),
                    new PickupWeaponState(Handgun, handgunReserve, 40, infinite),
                },
                current);
        }

        [Test]
        public void HealthHealsOnlyWhatIsMissing()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Health(25), new PickupCollector(null, 90, 100));

            Assert.That(outcome.IsCollected, Is.True);
            Assert.That(outcome.Kind, Is.EqualTo(PickupKind.Health));
            Assert.That(outcome.Health, Is.EqualTo(10));
        }

        [Test]
        public void HealthIsLeftWhenFull()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Health(25), Healthy());

            Assert.That(outcome.IsCollected, Is.False);
            Assert.That(outcome.Rejection, Is.EqualTo(PickupRejection.HealthFull));
            Assert.That(outcome.Health, Is.Zero);
        }

        [Test]
        public void DownCollectorsPickUpNothing()
        {
            var down = new PickupCollector(null, 0, 100);

            Assert.That(PickupJudge.Judge(PickupSpec.Health(25), down).Rejection, Is.EqualTo(PickupRejection.CollectorDown));
            Assert.That(PickupJudge.Judge(PickupSpec.Tag(1, Side.Bravo), down).Rejection, Is.EqualTo(PickupRejection.CollectorDown));
        }

        [Test]
        public void SideConditionIsCheckedBeforeTheKind()
        {
            var bravoOnly = PickupSpec.Health(25, PickupEligibility.Only(Side.Bravo));
            var hurtAlpha = new PickupCollector(Side.Alpha, 50, 100);
            var hurtBravo = new PickupCollector(Side.Bravo, 50, 100);

            Assert.That(PickupJudge.Judge(bravoOnly, hurtAlpha).Rejection, Is.EqualTo(PickupRejection.SideNotAllowed));
            Assert.That(PickupJudge.Judge(bravoOnly, hurtBravo).IsCollected, Is.True);
            Assert.That(PickupJudge.Judge(bravoOnly, new PickupCollector(null, 50, 100)).Rejection, Is.EqualTo(PickupRejection.SideNotAllowed));
        }

        [Test]
        public void EligibilityRules()
        {
            Assert.That(PickupEligibility.Anyone.Allows(null), Is.True);
            Assert.That(PickupEligibility.Anyone.Allows(Side.Bravo), Is.True);
            Assert.That(default(PickupEligibility), Is.EqualTo(PickupEligibility.Anyone));

            Assert.That(PickupEligibility.Only(Side.Alpha).Allows(Side.Alpha), Is.True);
            Assert.That(PickupEligibility.Only(Side.Alpha).Allows(Side.Bravo), Is.False);
            Assert.That(PickupEligibility.Only(Side.Alpha).Allows(null), Is.False);

            Assert.That(PickupEligibility.Except(Side.Alpha).Allows(Side.Alpha), Is.False);
            Assert.That(PickupEligibility.Except(Side.Alpha).Allows(new Side(2)), Is.True);
            Assert.That(PickupEligibility.Except(Side.Alpha).Allows(null), Is.True);

            Assert.That(PickupEligibility.Of(PickupSideRule.ExceptSide, Side.Bravo), Is.EqualTo(PickupEligibility.Except(Side.Bravo)));
            Assert.That(PickupEligibility.Of(PickupSideRule.Anyone, Side.Bravo), Is.EqualTo(PickupEligibility.Anyone));
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupEligibility.Of((PickupSideRule)99, Side.Alpha));
        }

        [Test]
        public void NewWeaponIsAdded()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Weapon(Launcher, 2), Armed());

            Assert.That(outcome.IsCollected, Is.True);
            Assert.That(outcome.AddsWeapon, Is.True);
            Assert.That(outcome.WeaponId, Is.EqualTo(Launcher));
            Assert.That(outcome.ReserveAmmo, Is.Zero);
        }

        [Test]
        public void OwnedWeaponTurnsIntoItsReserveAmmo()
        {
            var outcome = PickupJudge.Judge(PickupSpec.Weapon(Rifle, 30), Armed(rifleReserve: 80));

            Assert.That(outcome.IsCollected, Is.True);
            Assert.That(outcome.AddsWeapon, Is.False);
            Assert.That(outcome.WeaponId, Is.EqualTo(Rifle));
            Assert.That(outcome.ReserveAmmo, Is.EqualTo(10), "cut at the reserve limit");
        }

        [Test]
        public void OwnedWeaponIsLeftWhenItsReserveIsFullOrItGivesNoAmmo()
        {
            Assert.That(PickupJudge.Judge(PickupSpec.Weapon(Rifle, 30), Armed(rifleReserve: 90)).Rejection, Is.EqualTo(PickupRejection.AmmoFull));
            Assert.That(PickupJudge.Judge(PickupSpec.Weapon(Rifle, 0), Armed(rifleReserve: 0)).Rejection, Is.EqualTo(PickupRejection.AmmoFull));
        }

        [Test]
        public void CollectorsWithoutWeaponsCannotTakeWeaponsOrAmmo()
        {
            Assert.That(PickupJudge.Judge(PickupSpec.Weapon(Rifle, 30), Healthy()).Rejection, Is.EqualTo(PickupRejection.CannotHoldWeapons));
            Assert.That(PickupJudge.Judge(PickupSpec.ReserveAmmo(30), Healthy()).Rejection, Is.EqualTo(PickupRejection.CannotHoldWeapons));
        }

        [Test]
        public void AmmoGoesToTheCurrentWeaponFirst()
        {
            var outcome = PickupJudge.Judge(PickupSpec.ReserveAmmo(15), Armed(rifleReserve: 30, handgunReserve: 0, current: Handgun));

            Assert.That(outcome.WeaponId, Is.EqualTo(Handgun));
            Assert.That(outcome.ReserveAmmo, Is.EqualTo(15));
        }

        [Test]
        public void AmmoFallsBackToTheFirstWeaponWithRoomWhenTheCurrentIsFull()
        {
            var outcome = PickupJudge.Judge(PickupSpec.ReserveAmmo(15), Armed(rifleReserve: 90, handgunReserve: 35, current: Rifle));

            Assert.That(outcome.WeaponId, Is.EqualTo(Handgun));
            Assert.That(outcome.ReserveAmmo, Is.EqualTo(5));
        }

        [Test]
        public void AmmoForANamedWeaponGoesOnlyToIt()
        {
            Assert.That(PickupJudge.Judge(PickupSpec.ReserveAmmo(15, Handgun), Armed(current: Rifle)).WeaponId, Is.EqualTo(Handgun));
            Assert.That(PickupJudge.Judge(PickupSpec.ReserveAmmo(15, Launcher), Armed()).Rejection, Is.EqualTo(PickupRejection.NoWeapon));
            Assert.That(PickupJudge.Judge(PickupSpec.ReserveAmmo(15, Rifle), Armed(rifleReserve: 90)).Rejection, Is.EqualTo(PickupRejection.AmmoFull));
        }

        [Test]
        public void AmmoIsLeftWhenEveryReserveIsFullOrInfinite()
        {
            Assert.That(PickupJudge.Judge(PickupSpec.ReserveAmmo(15), Armed(90, 40)).Rejection, Is.EqualTo(PickupRejection.AmmoFull));
            Assert.That(PickupJudge.Judge(PickupSpec.ReserveAmmo(15), Armed(0, 0, infinite: true)).Rejection, Is.EqualTo(PickupRejection.AmmoFull));
            Assert.That(
                PickupJudge.Judge(PickupSpec.ReserveAmmo(15), Healthy().WithWeapons(Array.Empty<PickupWeaponState>(), null)).Rejection,
                Is.EqualTo(PickupRejection.NoWeapon));
        }

        [Test]
        public void ArmorNeedsAHolderWithRoom()
        {
            Assert.That(PickupJudge.Judge(PickupSpec.Armor(50), Healthy()).Rejection, Is.EqualTo(PickupRejection.NoArmor));
            Assert.That(PickupJudge.Judge(PickupSpec.Armor(50), Healthy().WithArmor(100, 100)).Rejection, Is.EqualTo(PickupRejection.ArmorFull));

            var outcome = PickupJudge.Judge(PickupSpec.Armor(50), Healthy().WithArmor(70, 100));
            Assert.That(outcome.IsCollected, Is.True);
            Assert.That(outcome.Armor, Is.EqualTo(30));
        }

        [Test]
        public void TagsAreAlwaysTakenAndTellWhoseSideTheyBelongTo()
        {
            var enemyTag = PickupJudge.Judge(PickupSpec.Tag(3, Side.Bravo), Healthy(Side.Alpha));
            Assert.That(enemyTag.IsCollected, Is.True);
            Assert.That(enemyTag.TagOwnerId, Is.EqualTo(3));
            Assert.That(enemyTag.TagOwnerSide, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(enemyTag.IsOwnSideTag, Is.False);

            Assert.That(PickupJudge.Judge(PickupSpec.Tag(3, Side.Bravo), Healthy(Side.Bravo)).IsOwnSideTag, Is.True);
            Assert.That(PickupJudge.Judge(PickupSpec.Tag(3, null), Healthy(Side.Bravo)).IsOwnSideTag, Is.False, "unknown owner side");
            Assert.That(PickupJudge.Judge(PickupSpec.Tag(3, Side.Bravo), Healthy()).IsOwnSideTag, Is.False, "collector without a side");
        }

        [Test]
        public void SpecsRejectBadValues()
        {
            Assert.Throws<ArgumentException>(() => PickupSpec.Weapon(null, 0));
            Assert.Throws<ArgumentException>(() => PickupSpec.Weapon(string.Empty, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupSpec.Weapon(Rifle, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupSpec.ReserveAmmo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupSpec.Health(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupSpec.Armor(-5));
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupSpec.Tag(-2, null));

            Assert.That(PickupSpec.ReserveAmmo(5, string.Empty).WeaponId, Is.Null, "empty means the current weapon");
            Assert.That(PickupSpec.Tag(PickupSpec.NoOwner, null).Amount, Is.EqualTo(1));
        }

        [Test]
        public void CollectorRejectsBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupCollector(null, 10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupCollector(null, 101, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupCollector(null, -1, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => Healthy().WithArmor(10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Healthy().WithArmor(11, 10));
            Assert.Throws<ArgumentException>(() => Healthy().WithWeapons(new[] { new PickupWeaponState(Rifle, 0, 0, false) }, Handgun));
            Assert.Throws<ArgumentException>(() => new PickupWeaponState(string.Empty, 0, 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupWeaponState(Rifle, -1, 0, false));
            Assert.Throws<ArgumentNullException>(() => PickupJudge.Judge(null, Healthy()));
            Assert.Throws<ArgumentNullException>(() => PickupJudge.Judge(PickupSpec.Health(1), null));
            Assert.Throws<ArgumentException>(() => PickupOutcome.Rejected(PickupKind.Health, PickupRejection.None));
        }

        [Test]
        public void WeaponStateReadsTheWeapon()
        {
            var spec = new WeaponSpec(Rifle, null, WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 10, 100f, DamageFalloff.None);
            var state = PickupWeaponState.Of(new Weapon(spec, 30, 60));

            Assert.That(state.WeaponId, Is.EqualTo(Rifle));
            Assert.That(state.ReserveAmmo, Is.EqualTo(60));
            Assert.That(state.MaxReserveAmmo, Is.EqualTo(90));
            Assert.That(state.Room, Is.EqualTo(30));
            Assert.That(PickupWeaponState.Of(new Weapon(spec, GameRules.Default.WithInfiniteReserve(true))).Room, Is.Zero);
        }
    }
}
