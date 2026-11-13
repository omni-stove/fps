using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LoadoutChoiceLogTests
    {
        private const string _rifle = "00000000-0000-4000-8000-00000000000a";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";

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
        public void WritesTheAnswerTheOwnerReceived()
        {
            Assert.That(LoadoutChoiceLog.Answer(true, null), Is.EqualTo("[Net] loadout answer accepted applies=next-spawn"));
            Assert.That(LoadoutChoiceLog.Answer(false, "no match is running"),
                Is.EqualTo("[Net] loadout answer rejected reason=no match is running"));
        }
    }
}
