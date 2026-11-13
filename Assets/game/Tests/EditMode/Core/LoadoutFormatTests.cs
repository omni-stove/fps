using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LoadoutFormatTests
    {
        [Test]
        public void EveryCategoryHasADisplayName()
        {
            foreach (WeaponCategory category in Enum.GetValues(typeof(WeaponCategory)))
            {
                Assert.That(LoadoutFormat.Category(category), Is.Not.Empty, category.ToString());
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => LoadoutFormat.Category((WeaponCategory)999));
        }

        [Test]
        public void SlotTitlesCountFromOne()
        {
            Assert.That(LoadoutFormat.SlotTitle(0), Is.EqualTo("スロット 1"));
            Assert.That(LoadoutFormat.SlotTitle(1), Is.EqualTo("スロット 2"));
            Assert.Throws<ArgumentOutOfRangeException>(() => LoadoutFormat.SlotTitle(-1));
        }

        [Test]
        public void RejectedAddsTheReasonWhenThereIsOne()
        {
            Assert.That(LoadoutFormat.Rejected("slot 0 is empty"), Is.EqualTo("選べません: slot 0 is empty"));
            Assert.That(LoadoutFormat.Rejected(null), Is.EqualTo("選べません"));
            Assert.That(LoadoutFormat.Rejected("  "), Is.EqualTo("選べません"));
        }
    }
}
