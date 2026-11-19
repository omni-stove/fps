using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Vector3 = System.Numerics.Vector3;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class StreakViewTests
    {
        private const float Tolerance = 1e-4f;

        // 名前のキーが空の枠は Id を、キーのある枠は表 (items.json の weapon.rifle は "Rifle") を名前にする
        private static readonly StreakSlot _uavReady = new StreakSlot("uav", string.Empty, 3, true);
        private static readonly StreakSlot _rifleNext = new StreakSlot("rifle-drop", "weapon.rifle", 5, false);
        private static readonly StreakSlot _strikeLater = new StreakSlot("strike", string.Empty, 7, false);
        private static readonly StreakSlot _strikeReady = new StreakSlot("strike", string.Empty, 7, true);

        private readonly List<Object> _owned = new List<Object>();
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

            foreach (var owned in _owned)
            {
                if (owned != null)
                {
                    Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        [Test]
        public void TheHudCarriesTheStreakViewHiddenUntilAValueIsPublished()
        {
            var view = CreateHud().Streak;

            Assert.That(view, Is.Not.Null);
            Assert.That(_hud.GetComponents<StreakView>(), Has.Length.EqualTo(1));
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Cards, Is.Empty);
            Assert.That(view.KillsText, Is.Empty);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Element.childCount, Is.EqualTo(0));
        }

        [Test]
        public void APublishedStreakShowsTheCountAndACardPerRewardFilledByTheKills()
        {
            var view = CreateHud().Streak;
            view.Icons = null;

            GameSignals.Streak.Publish(new StreakStatus(3, new[] { _uavReady, _rifleNext, _strikeLater }));

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.KillsText, Is.EqualTo("ストリーク 3"));
            Assert.That(view.Cards.Select(card => card.RewardId), Is.EqualTo(new[] { "uav", "rifle-drop", "strike" }), "one card per reward in slot order");
            Assert.That(view.Cards.Select(card => card.Name), Is.EqualTo(new[] { "uav", "Rifle", "strike" }));
            Assert.That(view.Cards.Select(card => card.State), Is.EqualTo(new[] { StreakCardState.Selected, StreakCardState.Charging, StreakCardState.Charging }));
            Assert.That(view.Cards[0].Caption, Is.EqualTo(UseCaption(view.KeyLabel)));
            Assert.That(view.Cards[1].Caption, Is.EqualTo("3/5"));
            Assert.That(view.Cards[2].Caption, Is.EqualTo("3/7"));
            Assert.That(view.Cards[0].Fill, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(view.Cards[1].Fill, Is.EqualTo(0.6f).Within(Tolerance));
            Assert.That(view.Cards[2].Fill, Is.EqualTo(3f / 7f).Within(Tolerance));
            Assert.That(view.SwitchText, Is.Empty, "one ready reward has nothing to switch to");

            Assert.That(view.Element.Q<Label>(className: StreakView.KillsClass).text, Is.EqualTo("ストリーク 3"));
            var cards = CardElements(view);
            Assert.That(cards.Count, Is.EqualTo(3));
            Assert.That(cards[0].ClassListContains(StreakView.StateClasses[StreakCardState.Selected]), Is.True);
            Assert.That(cards[1].ClassListContains(StreakView.StateClasses[StreakCardState.Charging]), Is.True);
            Assert.That(cards[1].Q<Label>(className: StreakView.CaptionClass).text, Is.EqualTo("3/5"));
            var fill = cards[1].Q<VisualElement>(className: StreakView.GaugeFillClass).style.width.value;
            Assert.That(fill.unit, Is.EqualTo(LengthUnit.Percent));
            Assert.That(fill.value, Is.EqualTo(60f).Within(1e-2f));
            Assert.That(view.Element.Q<Label>(className: StreakView.SwitchClass), Is.Null);
        }

        [Test]
        public void ClearingTheValueHidesTheViewAndANewValueReplacesTheCards()
        {
            var view = CreateHud().Streak;
            GameSignals.Streak.Publish(new StreakStatus(3, new[] { _uavReady, _rifleNext }));

            GameSignals.Streak.Publish(new StreakStatus(1, new[] { _rifleNext }));
            Assert.That(view.KillsText, Is.EqualTo("ストリーク 1"));
            Assert.That(view.Cards.Select(card => card.Caption), Is.EqualTo(new[] { "1/5" }));
            Assert.That(CardElements(view).Count, Is.EqualTo(1), "cards left over from the longer streak are taken away");
            Assert.That(view.KeyLabel, Is.Empty, "no key while nothing is ready");

            GameSignals.Streak.Clear();
            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Cards, Is.Empty);
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
            Assert.That(view.KillsText, Is.EqualTo("ストリーク 2"));
            Assert.That(view.Cards.Select(card => card.Caption), Is.EqualTo(new[] { "2/5" }));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void AStreakWithNoSlotsShowsOnlyTheCount()
        {
            var view = CreateHud().Streak;

            GameSignals.Streak.Publish(new StreakStatus(0, null));

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Cards, Is.Empty);
            Assert.That(view.Element.childCount, Is.EqualTo(1));
            Assert.That(view.Element.Q<Label>(className: StreakView.KillsClass).text, Is.EqualTo("ストリーク 0"));
        }

        [Test]
        public void TheCaptionNamesTheKeyOrLeavesItOutWhenTheDeviceHasNone()
        {
            var status = new StreakStatus(3, new[] { _uavReady, _rifleNext });

            Assert.That(Localizer.Resolve(StreakText.Caption(status, _uavReady, "4")), Is.EqualTo("[4] 使う"));
            Assert.That(Localizer.Resolve(StreakText.Caption(status, _uavReady, " ")), Is.EqualTo("使える"));
            Assert.That(Localizer.Resolve(StreakText.Caption(status, _uavReady, null)), Is.EqualTo("使える"));
            Assert.That(Localizer.Resolve(StreakText.Caption(status, _rifleNext, "4")), Is.EqualTo("3/5"), "a reward still charging shows its count, not the key");
        }

        [Test]
        public void ARewardReachedButAlreadyUsedThisLifeIsSpentAndStaysFull()
        {
            var spent = new StreakSlot("spent", string.Empty, 2, false);
            var status = new StreakStatus(4, new[] { spent, _rifleNext });

            Assert.That(StreakText.StateOf(status, spent), Is.EqualTo(StreakCardState.Spent));
            Assert.That(StreakText.Fill(status, spent), Is.EqualTo(1f));
            Assert.That(Localizer.Resolve(StreakText.Caption(status, spent, "4")), Is.EqualTo("使用済み"));
            Assert.That(StreakText.StateOf(status, _rifleNext), Is.EqualTo(StreakCardState.Charging));
            Assert.That(StreakText.Fill(status, _rifleNext), Is.EqualTo(0.8f).Within(Tolerance));
        }

        [Test]
        public void TheUseKeyFollowsTheSelectedRewardAndTheSwitchLineAppearsWithTwoReady()
        {
            var view = CreateHud().Streak;

            GameSignals.Streak.Publish(new StreakStatus(9, new[] { _uavReady, _strikeReady }, "strike"));

            Assert.That(view.Cards.Select(card => card.State), Is.EqualTo(new[] { StreakCardState.Held, StreakCardState.Selected }));
            Assert.That(view.Cards[0].Caption, Is.EqualTo("使える"));
            Assert.That(view.Cards[1].Caption, Is.EqualTo(UseCaption(view.KeyLabel)));
            Assert.That(view.SwitchText, Is.EqualTo(KeyLine(view.SwitchKeyLabel, "切り替え", "ストリークの切り替えで選ぶ")));
            var cards = CardElements(view);
            Assert.That(cards[0].ClassListContains(StreakView.StateClasses[StreakCardState.Held]), Is.True);
            Assert.That(cards[1].ClassListContains(StreakView.StateClasses[StreakCardState.Selected]), Is.True);
            Assert.That(view.Element.Q<Label>(className: StreakView.SwitchClass).text, Is.EqualTo(view.SwitchText));

            Assert.That(Localizer.Resolve(StreakText.Switch("3")), Is.EqualTo("[3] 切り替え"));
            Assert.That(Localizer.Resolve(StreakText.Switch(" ")), Is.EqualTo("ストリークの切り替えで選ぶ"));

            // 1 つ使うと、切り替えの案内は無くなる。
            GameSignals.Streak.Publish(new StreakStatus(9, new[] { _uavReady, new StreakSlot("strike", string.Empty, 7, false) }));
            Assert.That(view.Cards.Select(card => card.State), Is.EqualTo(new[] { StreakCardState.Selected, StreakCardState.Spent }));
            Assert.That(view.SwitchText, Is.Empty);
            Assert.That(view.SwitchKeyLabel, Is.Empty);
            Assert.That(view.Element.Q<Label>(className: StreakView.SwitchClass), Is.Null);
        }

        [Test]
        public void ARewardWithAnIconShowsItAndOneWithoutShowsItsNameInTheBox()
        {
            var view = CreateHud().Streak;
            var texture = Own(new Texture2D(2, 2));
            var catalog = Own(ScriptableObject.CreateInstance<StreakIconCatalog>());
            catalog.Add("uav", texture);
            view.Icons = catalog;

            GameSignals.Streak.Publish(new StreakStatus(3, new[] { _uavReady, _strikeLater }));

            Assert.That(view.Cards[0].Icon, Is.SameAs(texture));
            Assert.That(view.Cards[1].Icon, Is.Null);
            var cards = CardElements(view);
            var withIcon = cards[0].Q<VisualElement>(className: StreakView.IconClass);
            Assert.That(withIcon.style.backgroundImage.value.texture, Is.SameAs(texture));
            Assert.That(withIcon.Q<Label>(className: StreakView.IconNameClass), Is.Null);
            var withoutIcon = cards[1].Q<VisualElement>(className: StreakView.IconClass);
            Assert.That(withoutIcon.Q<Label>(className: StreakView.IconNameClass).text, Is.EqualTo("strike"));
        }

        [Test]
        public void TheIconCatalogIsFoundByRewardId()
        {
            var texture = Own(new Texture2D(2, 2));
            var catalog = Own(ScriptableObject.CreateInstance<StreakIconCatalog>());
            catalog.Add("airstrike", texture);

            Assert.That(catalog.Find("airstrike"), Is.SameAs(texture));
            Assert.That(catalog.Find("uav"), Is.Null);
            Assert.That(catalog.Find(null), Is.Null);
            Assert.That(StreakIconCatalog.ResourcePath, Is.EqualTo(PresentationCatalog.CatalogResourcePath("StreakIcon")));
        }

        [Test]
        public void WhileChoosingTheViewShowsTheGuidanceInsteadOfTheCards()
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
            Assert.That(view.Cards, Is.Empty);
            Assert.That(view.Element.Children().Select(row => ((Label)row).text), Is.EqualTo(view.Lines));
            Assert.That(view.Element.Children().All(row => row.ClassListContains(StreakView.TargetingRowClass)), Is.True);

            GameSignals.StreakTargeting.Clear();

            Assert.That(view.IsTargeting, Is.False);
            Assert.That(view.Lines, Is.Empty);
            Assert.That(view.KillsText, Is.EqualTo("ストリーク 5"), "the count comes back after choosing");
            Assert.That(CardElements(view).Count, Is.EqualTo(1));
            Assert.That(view.Element.Query(className: StreakView.TargetingRowClass).ToList(), Is.Empty);
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
                    StreakText.ProgressKey,
                    StreakText.UseKey,
                    StreakText.ReadyKey,
                    StreakText.SpentKey,
                    StreakText.SwitchKey,
                    StreakText.SwitchWithoutKeyKey,
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

        private T Own<T>(T owned)
            where T : Object
        {
            _owned.Add(owned);
            return owned;
        }

        private static List<VisualElement> CardElements(StreakView view)
        {
            return view.Element.Query<VisualElement>(className: StreakView.CardClass).ToList();
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

        private static string UseCaption(string keyLabel)
        {
            return KeyLine(keyLabel, "使う", "使える");
        }

        private static string[] Resolve(IEnumerable<LocalizedText> texts)
        {
            return texts.Select(text => Localizer.Resolve(text)).ToArray();
        }
    }
}
