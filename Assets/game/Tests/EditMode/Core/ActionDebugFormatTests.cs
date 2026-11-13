using System;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ActionDebugFormatTests
    {
        [Test]
        public void DefaultStateListsEveryItemInOrderWithNothingActive()
        {
            var lines = ActionDebugFormat.Lines(default);

            Assert.That(lines.Select(line => line.Text).ToArray(), Is.EqualTo(new[]
            {
                "ダッシュ: OFF",
                "しゃがみ: OFF",
                "回避: OFF",
                "乗り越え: OFF",
                "接地: OFF",
                "カバー: OFF",
                "ブラインドファイア: OFF",
                "ADS: OFF",
                "射撃: OFF",
                "リロード: OFF (アクティブ: -)",
                "ダウン: OFF",
                "肩: 右",
                "視点: 一人称",
                "武器: -",
                "ばらつき: 0.0°",
            }));
            Assert.That(lines.Length, Is.EqualTo(ActionDebugFormat.LineCount));
            Assert.That(lines.Any(line => line.IsActive), Is.False);
        }

        [TestCase(PlayerActionConditions.Sprinting, 0, "ダッシュ: ON")]
        [TestCase(PlayerActionConditions.Crouching, 1, "しゃがみ: ON")]
        [TestCase(PlayerActionConditions.Dodging, 2, "回避: ON")]
        [TestCase(PlayerActionConditions.Vaulting, 3, "乗り越え: ON")]
        [TestCase(PlayerActionConditions.Grounded, 4, "接地: ON")]
        [TestCase(PlayerActionConditions.InCover, 5, "カバー: ON")]
        [TestCase(PlayerActionConditions.BlindFiring, 6, "ブラインドファイア: ON")]
        [TestCase(PlayerActionConditions.Aiming, 7, "ADS: ON")]
        [TestCase(PlayerActionConditions.Firing, 8, "射撃: ON")]
        [TestCase(PlayerActionConditions.Reloading, 9, "リロード: ON 0% (アクティブ: -)")]
        [TestCase(PlayerActionConditions.Downed, 10, "ダウン: ON")]
        public void EachConditionLightsOnlyItsOwnLine(PlayerActionConditions flag, int index, string expected)
        {
            var lines = ActionDebugFormat.Lines(State(flag));

            Assert.That(lines[index].Text, Is.EqualTo(expected));
            for (var i = 0; i < lines.Length; i++)
            {
                Assert.That(lines[i].IsActive, Is.EqualTo(i == index), lines[i].Text);
            }
        }

        [Test]
        public void ValuesAreShownWithoutLightingTheLine()
        {
            var state = new PlayerActionState(
                PlayerActionConditions.None, 0f, null, ShoulderSide.Left, ViewMode.ThirdPerson, "Rifle", 2.5f);

            var lines = ActionDebugFormat.Lines(state);

            Assert.That(lines[11], Is.EqualTo(new ActionDebugLine("肩: 左", false)));
            Assert.That(lines[12], Is.EqualTo(new ActionDebugLine("視点: 三人称", false)));
            Assert.That(lines[13], Is.EqualTo(new ActionDebugLine("武器: Rifle", false)));
            Assert.That(lines[14], Is.EqualTo(new ActionDebugLine("ばらつき: 2.5°", false)));
        }

        [Test]
        public void ReloadLineShowsProgressAndTheActiveReloadResult()
        {
            var state = new PlayerActionState(
                PlayerActionConditions.Reloading, 0.456f, ActiveReloadResult.Fail, ShoulderSide.Right, ViewMode.FirstPerson, "Rifle", 0f);

            Assert.That(ActionDebugFormat.Lines(state)[9], Is.EqualTo(new ActionDebugLine("リロード: ON 45% (アクティブ: 失敗)", true)));
        }

        [Test]
        public void SuccessfulActiveReloadStaysVisibleAfterTheReloadEnds()
        {
            var state = new PlayerActionState(
                PlayerActionConditions.None, 0f, ActiveReloadResult.Success, ShoulderSide.Right, ViewMode.FirstPerson, "Rifle", 0f);

            Assert.That(ActionDebugFormat.Lines(state)[9], Is.EqualTo(new ActionDebugLine("リロード: OFF (アクティブ: 成功)", false)));
        }

        [TestCase(0f, "ON 0% (アクティブ: -)")]
        [TestCase(0.29f, "ON 29% (アクティブ: -)")]
        [TestCase(0.999f, "ON 99% (アクティブ: -)")]
        [TestCase(1f, "ON 100% (アクティブ: -)")]
        public void ReloadProgressIsFlooredToWholePercent(float progress, string expected)
        {
            Assert.That(ActionDebugFormat.Reload(true, progress, null), Is.EqualTo(expected));
        }

        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        [TestCase(float.NaN)]
        public void ReloadRejectsProgressOutOfRange(float progress)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ActionDebugFormat.Reload(true, progress, null));
        }

        [TestCase(0f, "0.0°")]
        [TestCase(2.5f, "2.5°")]
        [TestCase(90f, "90.0°")]
        public void SpreadHasOneDecimal(float degrees, string expected)
        {
            Assert.That(ActionDebugFormat.Spread(degrees), Is.EqualTo(expected));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void SpreadRejectsNegativeOrNonFinite(float degrees)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ActionDebugFormat.Spread(degrees));
        }

        [Test]
        public void LineTreatsNullTextAsEmpty()
        {
            Assert.That(new ActionDebugLine(null, true).Text, Is.EqualTo(string.Empty));
            Assert.That(default(ActionDebugLine).Text, Is.EqualTo(string.Empty));
            Assert.That(default(ActionDebugLine), Is.EqualTo(new ActionDebugLine(string.Empty, false)));
        }

        private static PlayerActionState State(PlayerActionConditions flags)
        {
            return new PlayerActionState(flags, 0f, null, ShoulderSide.Right, ViewMode.FirstPerson, string.Empty, 0f);
        }
    }
}
