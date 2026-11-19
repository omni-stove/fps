using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class InputActionLabelsTests
    {
        [Test]
        public void EverySlotHasALabelForThePlayer()
        {
            foreach (var slot in PlayerBindings.Slots)
            {
                Assert.That(InputActionLabels.LabelOf(slot.ActionName, slot.Part), Is.Not.EqualTo(slot.ActionName), slot.ToString());
            }
        }

        [TestCase("Melee", "近接攻撃")]
        [TestCase("Prone", "伏せ")]
        [TestCase("Ability1", "アビリティ 1")]
        [TestCase("Ability2", "アビリティ 2")]
        [TestCase("SquadCommand", "分隊への指示")]
        [TestCase("SwitchAmmoType", "弾薬の切り替え")]
        [TestCase("CycleMagnification", "倍率の切り替え")]
        [TestCase("HoldBreath", "息止め")]
        [TestCase("SwitchGrenade", "グレネードの切り替え")]
        [TestCase("Inventory", "持ち物")]
        [TestCase("UseStreak", "ストリークを使う")]
        [TestCase("SwitchStreak", "ストリークの切り替え")]
        [TestCase("LeanLeft", "左へリーン")]
        [TestCase("LeanRight", "右へリーン")]
        [TestCase("Chat", "チャット")]
        [TestCase("TeamChat", "チームチャット")]
        [TestCase("PushToTalk", "押して話す")]
        public void NewButtonsAreLabelledInJapanese(string actionName, string label)
        {
            Assert.That(InputActionLabels.LabelOf(actionName), Is.EqualTo(label));
        }

        [Test]
        public void AConflictIsLabelledByItsActionAndPart()
        {
            Assert.That(InputActionLabels.LabelOf(new BindingConflict("Interact", null, 0)), Is.EqualTo(InputActionLabels.LabelOf("Interact")));
            Assert.That(InputActionLabels.LabelOf(new BindingConflict("Move", "up", 2)), Is.EqualTo("前へ移動"));
        }
    }
}
