using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LoadoutChoiceLogTests
    {
        private const string _rifle = "00000000-0000-4000-8000-00000000000a";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";

        private static readonly string[] _beaconAndEmpty = { "beacon", "" };
        private static readonly string[] _beaconOnly = { "beacon" };

        private static WeaponSpec Spec(string id, WeaponCategory category)
        {
            return new WeaponSpec(id, null, category, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None);
        }

        [Test]
        public void WritesTheRequestedIdsInSlotOrder()
        {
            Assert.That(LoadoutChoiceLog.Requested(new[] { _rifle, _handgun }),
                Is.EqualTo("[Net] loadout request weapons=" + _rifle + "," + _handgun));
            Assert.That(LoadoutChoiceLog.Requested(Array.Empty<string>()), Is.EqualTo("[Net] loadout request weapons=-"));
            Assert.Throws<ArgumentNullException>(() => LoadoutChoiceLog.Requested(null));
        }

        [Test]
        public void WritesTheRequestedAbilitiesWithADashForAnEmptySlot()
        {
            Assert.That(LoadoutChoiceLog.Requested(new[] { _rifle }, _beaconAndEmpty),
                Is.EqualTo("[Net] loadout request weapons=" + _rifle + " abilities=beacon,-"));
            Assert.That(LoadoutChoiceLog.Requested(new[] { _rifle }, null),
                Is.EqualTo("[Net] loadout request weapons=" + _rifle), "a request without abilities reads like a weapons-only one");
            Assert.Throws<ArgumentNullException>(() => LoadoutChoiceLog.Requested(null, _beaconOnly));
            Assert.That(LoadoutChoiceLog.SavedAbilitiesNotSent("the saved abilities are not all offered"),
                Is.EqualTo("[Net] loadout the saved abilities are not sent; the server uses the loadout's: the saved abilities are not all offered"));
            Assert.That(LoadoutChoiceLog.SavedAbilitiesNotSent(null),
                Is.EqualTo("[Net] loadout the saved abilities are not sent; the server uses the loadout's: -"));
        }

        [Test]
        public void WritesTheAcceptedWeaponsAndWhenTheyApply()
        {
            var weapons = new[] { Spec(_rifle, WeaponCategory.AssaultRifle), Spec(_handgun, WeaponCategory.Handgun) };

            Assert.That(LoadoutChoiceLog.Accepted(3, weapons),
                Is.EqualTo("[Net] loadout accepted client=3 weapons=" + _rifle + "," + _handgun + " applies=next-spawn"));
            Assert.Throws<ArgumentNullException>(() => LoadoutChoiceLog.Accepted(3, null));
        }

        [Test]
        public void WritesTheRejectionReason()
        {
            Assert.That(LoadoutChoiceLog.Rejected(3, "slot 0 does not accept Handgun"),
                Is.EqualTo("[Net] loadout rejected client=3 reason=slot 0 does not accept Handgun"));
            Assert.That(LoadoutChoiceLog.Rejected(3, null), Is.EqualTo("[Net] loadout rejected client=3 reason=-"));
        }

        [Test]
        public void WritesTheAcceptedAbilitiesWithADashForNone()
        {
            Assert.That(LoadoutChoiceLog.AcceptedAbilities(3, new[] { "beacon", string.Empty }),
                Is.EqualTo("[Net] loadout abilities accepted client=3 abilities=beacon,- applies=next-spawn"));
            Assert.That(LoadoutChoiceLog.AcceptedAbilities(3, Array.Empty<string>()),
                Is.EqualTo("[Net] loadout abilities accepted client=3 abilities=- applies=next-spawn"));
            Assert.Throws<ArgumentNullException>(() => LoadoutChoiceLog.AcceptedAbilities(3, null));
        }

        [Test]
        public void WritesWhyTheAbilitiesWereRejected()
        {
            Assert.That(LoadoutChoiceLog.RejectedAbilities(3, "ability slot 1 names mine, which is not in the deployables"),
                Is.EqualTo("[Net] loadout abilities rejected client=3 reason=ability slot 1 names mine, which is not in the deployables"));
            Assert.That(LoadoutChoiceLog.RejectedAbilities(3, null), Is.EqualTo("[Net] loadout abilities rejected client=3 reason=-"));
        }

        [Test]
        public void WritesWhyTheLoadoutScreenDoesNotOfferAbilities()
        {
            Assert.That(LoadoutChoiceLog.AbilitiesNotOffered("ability catalog 'Abilities' ability #1 is empty"),
                Is.EqualTo("[Net] loadout the loadout screen does not offer abilities: ability catalog 'Abilities' ability #1 is empty"));
            Assert.That(LoadoutChoiceLog.AbilitiesNotOffered(null), Is.EqualTo("[Net] loadout the loadout screen does not offer abilities: -"));
        }

        [Test]
        public void WritesTheChosenCosmeticOrADashWhenItIsTakenOff()
        {
            Assert.That(LoadoutChoiceLog.Cosmetic(3, "skin-desert"), Is.EqualTo("[Net] loadout cosmetic client=3 id=skin-desert"));
            Assert.That(LoadoutChoiceLog.Cosmetic(3, ""), Is.EqualTo("[Net] loadout cosmetic client=3 id=-"));
            Assert.That(LoadoutChoiceLog.Cosmetic(3, null), Is.EqualTo("[Net] loadout cosmetic client=3 id=-"));
            Assert.That(LoadoutChoiceLog.RequestedCosmetic("skin-desert"), Is.EqualTo("[Net] loadout request cosmetic=skin-desert"));
            Assert.That(LoadoutChoiceLog.CosmeticTooLong("x", 64), Is.EqualTo("[Net] loadout the cosmetic 'x' is longer than 64 bytes and is not sent"));
        }

        [Test]
        public void WritesTheAnswerTheOwnerReceived()
        {
            Assert.That(LoadoutChoiceLog.Answer(true, null), Is.EqualTo("[Net] loadout answer accepted applies=next-spawn"));
            Assert.That(LoadoutChoiceLog.Answer(false, "no match is running"),
                Is.EqualTo("[Net] loadout answer rejected reason=no match is running"));
        }
    }
}
