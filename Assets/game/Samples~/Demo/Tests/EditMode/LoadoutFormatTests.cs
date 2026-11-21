using System;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class LoadoutFormatTests
    {
        [Test]
        public void EveryCategoryHasADisplayName()
        {
            // Unity の .NET には Enum.GetValues<TEnum>() が無いので、dotnet でだけ出る CA2263 を止める
#pragma warning disable CA2263
            foreach (WeaponCategory category in Enum.GetValues(typeof(WeaponCategory)))
#pragma warning restore CA2263
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
        public void AbilitySlotTitlesCountFromOneAndNoneHasAName()
        {
            Assert.That(LoadoutFormat.AbilitySlotTitle(0), Is.EqualTo("能力 1"));
            Assert.That(LoadoutFormat.AbilitySlotTitle(1), Is.EqualTo("能力 2"));
            Assert.Throws<ArgumentOutOfRangeException>(() => LoadoutFormat.AbilitySlotTitle(-1));
            Assert.That(LoadoutFormat.NoAbility, Is.EqualTo("なし"));
            Assert.That(LoadoutFormat.PreviousAbility, Is.EqualTo("<"));
            Assert.That(LoadoutFormat.NextAbility, Is.EqualTo(">"));
        }

        [Test]
        public void PresetTextsCountFromOneAndNameUnnamedPresetsByTheirNumber()
        {
            Assert.That(LoadoutFormat.PresetTitle(0, 3), Is.EqualTo("プリセット 1/3"));
            Assert.That(LoadoutFormat.PresetTitle(2, 3), Is.EqualTo("プリセット 3/3"));
            Assert.Throws<ArgumentOutOfRangeException>(() => LoadoutFormat.PresetTitle(3, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => LoadoutFormat.PresetTitle(-1, 3));

            var choice = new LoadoutChoice(new[] { "rifle" });
            Assert.That(LoadoutFormat.PresetName(new LoadoutPreset(string.Empty, choice), 1), Is.EqualTo("ロードアウト 2"));
            Assert.That(LoadoutFormat.PresetName(new LoadoutPreset("近距離", choice), 1), Is.EqualTo("近距離"));
            Assert.Throws<ArgumentNullException>(() => LoadoutFormat.PresetName(null, 0));

            Assert.That(LoadoutFormat.PresetFull(5), Is.EqualTo("プリセットは 5 個まで保存できます"));
            Assert.That(LoadoutFormat.PresetAdded("近距離"), Is.EqualTo("近距離 として保存しました"));
            Assert.That(LoadoutFormat.PresetRenamed("遠距離"), Is.EqualTo("名前を 遠距離 に変えました"));
            Assert.That(LoadoutFormat.PresetSwitched("遠距離"), Is.EqualTo("遠距離 に切り替えました。次の出現から使います"));
            Assert.That(LoadoutFormat.PresetNameInvalid(24), Is.EqualTo("この名前は使えません (24 文字まで、改行なし)"));
            Assert.That(LoadoutFormat.SaveAsNewPreset, Is.EqualTo("新しく保存"));
            Assert.That(LoadoutFormat.RenamePreset, Is.EqualTo("名前を変更"));
            Assert.That(LoadoutFormat.PresetNameLabel, Is.EqualTo("名前"));
            Assert.That(LoadoutFormat.PreviousPreset, Is.EqualTo("<"));
            Assert.That(LoadoutFormat.NextPreset, Is.EqualTo(">"));
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
