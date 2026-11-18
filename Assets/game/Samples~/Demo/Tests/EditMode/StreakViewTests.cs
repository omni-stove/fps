using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;
using Vector3 = System.Numerics.Vector3;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class StreakViewTests
    {
        // 名前のキーが空の枠は Id を、キーのある枠は表 (items.json の weapon.rifle は "Rifle") を名前にする
        private static readonly StreakSlot _uavReady = new StreakSlot("uav", string.Empty, 3, true);
        private static readonly StreakSlot _rifleNext = new StreakSlot("rifle-drop", "weapon.rifle", 5, false);
        private static readonly StreakSlot _strikeLater = new StreakSlot("strike", string.Empty, 7, false);

        private Hud _hud;

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
            if (_hud != null)
            {
                var settings = _hud.OwnedPanelSettings;
                Object.DestroyImmediate(_hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }

                _hud = null;
            }
        }

        [Test]
        public void TheHudCarriesTheStreakViewHiddenUntilAValueIsPublished()
        {
            var view = CreateHud().Streak;

            Assert.That(view, Is.Not.Null);
            Assert.That(_hud.GetComponents<StreakView>(), Has.Length.EqualTo(1));
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Lines, Is.Empty);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Element.childCount, Is.EqualTo(0));
        }

        [Test]
        public void APublishedStreakShowsTheCountTheNextRewardAndTheReadyRewardWithItsKey()
        {
            var view = CreateHud().Streak;

            GameSignals.Streak.Publish(new StreakStatus(3, new[] { _uavReady, _rifleNext, _strikeLater }));

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.Lines, Is.EqualTo(new[] { "ストリーク 3", "Rifle まで あと 2", ReadyLine(view.KeyLabel, "uav") }));
            Assert.That(RowTexts(view), Is.EqualTo(view.Lines));
            Assert.That(view.IsReadyLine(0), Is.False);
            Assert.That(view.IsReadyLine(1), Is.False);
            Assert.That(view.IsReadyLine(2), Is.True);
            Assert.That(view.Element.Children().Last().ClassListContains(StreakView.ReadyRowClass), Is.True);
            Assert.That(view.Element.Children().First().ClassListContains(StreakView.ReadyRowClass), Is.False);
        }

        [Test]
        public void ClearingTheValueHidesTheViewAndANewValueReplacesTheRows()
        {
            var view = CreateHud().Streak;
            GameSignals.Streak.Publish(new StreakStatus(3, new[] { _uavReady, _rifleNext }));

            GameSignals.Streak.Publish(new StreakStatus(1, new[] { _rifleNext }));
            Assert.That(view.Lines, Is.EqualTo(new[] { "ストリーク 1", "Rifle まで あと 4" }));
            Assert.That(RowTexts(view), Is.EqualTo(view.Lines), "rows left over from the longer streak are taken away");
            Assert.That(view.KeyLabel, Is.Empty, "no key while nothing is ready");

            GameSignals.Streak.Clear();
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Lines, Is.Empty);
            Assert.That(view.Element.childCount, Is.EqualTo(0));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void RebindingCatchesUpWithTheValuePublishedWhileUnbound()
        {
            var hud = CreateHud();
            var view = hud.Streak;
            view.Unbind();

            GameSignals.Streak.Publish(new StreakStatus(2, new[] { _rifleNext }));
            Assert.That(view.IsShown, Is.False, "an unbound view does not listen");

            hud.RebuildView();
            Assert.That(view.Lines, Is.EqualTo(new[] { "ストリーク 2", "Rifle まで あと 3" }));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void AStreakWithNoSlotsShowsOnlyTheCount()
        {
            var view = CreateHud().Streak;

            GameSignals.Streak.Publish(new StreakStatus(0, null));

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Lines, Is.EqualTo(new[] { "ストリーク 0" }));
        }

        [Test]
        public void TheLinesNameTheKeyOrLeaveItOutWhenTheDeviceHasNone()
        {
            var status = new StreakStatus(3, new[] { _uavReady, _rifleNext });

            Assert.That(Resolve(StreakText.Lines(status, "4")), Is.EqualTo(new[] { "ストリーク 3", "Rifle まで あと 2", "[4] uav を使う" }));
            Assert.That(Resolve(StreakText.Lines(status, " ")), Is.EqualTo(new[] { "ストリーク 3", "Rifle まで あと 2", "uav を使える" }));
            Assert.That(Resolve(StreakText.Lines(status, null)), Is.EqualTo(new[] { "ストリーク 3", "Rifle まで あと 2", "uav を使える" }));
        }

        [Test]
        public void TheNextRewardIsTheNearestOneNotYetReached()
        {
            var spent = new StreakSlot("spent", string.Empty, 2, false);
            var nearest = new StreakSlot("nearest", string.Empty, 6, false);
            var status = new StreakStatus(4, new[] { _strikeLater, spent, nearest, _uavReady });

            Assert.That(StreakText.TryFindNext(status, out var next), Is.True);
            Assert.That(next, Is.EqualTo(nearest), "a slot already reached but not ready (spent) is skipped, and the nearer one wins over the earlier");
            Assert.That(
                Resolve(StreakText.Lines(status, "4")),
                Is.EqualTo(new[] { "ストリーク 4", "nearest まで あと 2", "[4] uav を使う" }));
        }

        [Test]
        public void ThereIsNoNextLineOnceEverySlotIsReached()
        {
            var status = new StreakStatus(9, new[] { _uavReady, new StreakSlot("strike", string.Empty, 7, true) });

            Assert.That(StreakText.TryFindNext(status, out _), Is.False);
            Assert.That(
                Resolve(StreakText.Lines(status, "4")),
                Is.EqualTo(new[] { "ストリーク 9", "[4] uav を使う", "[4] strike を使う" }),
                "every ready slot gets its own line in slot order");
        }

        [Test]
        public void WhileChoosingTheViewShowsTheGuidanceInsteadOfTheCount()
        {
            var view = CreateHud().Streak;
            GameSignals.Streak.Publish(new StreakStatus(5, new[] { _uavReady }));

            GameSignals.StreakTargeting.Publish(Choosing());

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.IsTargeting, Is.True);
            Assert.That(view.Lines, Is.EqualTo(new[]
            {
                "空爆 の地点と向きを選ぶ",
                "移動で地点、視点で向きを動かす",
                KeyLine(view.ConfirmKeyLabel, "決定", "攻撃で決定"),
                KeyLine(view.CancelKeyLabel, "取消", "構えで取消"),
            }));
            Assert.That(RowTexts(view), Is.EqualTo(view.Lines));
            Assert.That(view.Element.Children().All(row => row.ClassListContains(StreakView.TargetingRowClass)), Is.True);
            Assert.That(view.Element.Children().Any(row => row.ClassListContains(StreakView.ReadyRowClass)), Is.False);

            GameSignals.StreakTargeting.Clear();

            Assert.That(view.IsTargeting, Is.False);
            Assert.That(view.Lines, Is.EqualTo(new[] { "ストリーク 5", ReadyLine(view.KeyLabel, "uav") }), "the count comes back after choosing");
            Assert.That(view.Element.Children().Any(row => row.ClassListContains(StreakView.TargetingRowClass)), Is.False);
        }

        [Test]
        public void TheGuidanceShowsEvenWithoutAStreakValue()
        {
            var view = CreateHud().Streak;

            GameSignals.StreakTargeting.Publish(Choosing());

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            GameSignals.StreakTargeting.Clear();
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheGuidanceLinesNameTheKeysOrTheButtonsWhenTheDeviceHasNone()
        {
            Assert.That(
                Resolve(StreakText.TargetingLines(Choosing(), "LMB", " RMB ")),
                Is.EqualTo(new[] { "空爆 の地点と向きを選ぶ", "移動で地点、視点で向きを動かす", "[LMB] 決定", "[RMB] 取消" }));
            Assert.That(
                Resolve(StreakText.TargetingLines(Choosing(), null, " ")),
                Is.EqualTo(new[] { "空爆 の地点と向きを選ぶ", "移動で地点、視点で向きを動かす", "攻撃で決定", "構えで取消" }));

            var unnamed = new StreakTargetingStatus("strike", string.Empty, Vector3.Zero, Vector3.Zero, 0f, 30f, 10f);
            Assert.That(Localizer.Resolve(StreakText.TargetingLines(unnamed, "LMB", "RMB")[0]), Is.EqualTo("strike の地点と向きを選ぶ"), "a reward without a name key shows its id");
        }

        [Test]
        public void EveryStreakKeyIsInBothLanguages()
        {
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in new[]
                {
                    StreakText.KillsKey,
                    StreakText.NextKey,
                    StreakText.ReadyKey,
                    StreakText.ReadyWithoutKeyKey,
                    StreakText.TargetChooseKey,
                    StreakText.TargetMoveKey,
                    StreakText.TargetConfirmKey,
                    StreakText.TargetConfirmWithoutKeyKey,
                    StreakText.TargetCancelKey,
                    StreakText.TargetCancelWithoutKeyKey,
                    StreakSpec.DefaultUavNameKey,
                    StreakSpec.DefaultAirstrikeNameKey,
                })
                {
                    Assert.That(table.Get(key), Is.Not.EqualTo(key), language + " " + key);
                }
            }
        }

        private Hud CreateHud()
        {
            _hud = Hud.Create("StreakTests.Hud");
            return _hud;
        }

        private static StreakTargetingStatus Choosing()
        {
            return new StreakTargetingStatus(
                StreakSpec.AirstrikeRewardId, StreakSpec.DefaultAirstrikeNameKey, Vector3.Zero, new Vector3(4f, 0f, 9f), 30f, 100f, 24f);
        }

        private static string KeyLine(string keyLabel, string action, string withoutKey)
        {
            return string.IsNullOrEmpty(keyLabel) ? withoutKey : "[" + keyLabel + "] " + action;
        }

        private static string ReadyLine(string keyLabel, string rewardName)
        {
            return string.IsNullOrEmpty(keyLabel) ? rewardName + " を使える" : "[" + keyLabel + "] " + rewardName + " を使う";
        }

        private static string[] RowTexts(StreakView view)
        {
            return view.Element.Children().Select(row => ((Label)row).text).ToArray();
        }

        private static string[] Resolve(System.Collections.Generic.IEnumerable<LocalizedText> texts)
        {
            return texts.Select(text => Localizer.Resolve(text)).ToArray();
        }
    }
}
