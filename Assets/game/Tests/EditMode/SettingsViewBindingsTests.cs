using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 設定画面のキーとボタンの割り当て: 変えるとすぐ効き、重なりを知らせて取り消せ、既定に戻せ、閉じると保存されて起動し直しても残ること。
    /// 本物の設定とプロジェクト全体のアクションを壊さないよう、保存先はテストごとの別のキー、アクションは写しにする。
    /// </summary>
    public sealed class SettingsViewBindingsTests
    {
        private static readonly RebindSlot ReloadKey = new RebindSlot("Reload", InputDeviceKind.KeyboardMouse);
        private static readonly RebindSlot ReloadPad = new RebindSlot("Reload", InputDeviceKind.Gamepad);

        private ControlSettings _appliedBefore;
        private float _volumeBefore;
        private ControlSettingsStore _store;
        private InputBindingOverridesStore _bindingStore;
        private InputActionAsset _asset;
        private InputActionAsset _restarted;
        private SettingsView _view;
        private VisualElement _container;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            _volumeBefore = AudioListener.volume;

            // 案内の文を日本語で確かめるので、この機械で保存した言語に依らないようにする
            AppliedControlSettings.Apply(_appliedBefore.WithLanguage(GameLanguage.Japanese));
            var suffix = Guid.NewGuid().ToString("N");
            _store = new ControlSettingsStore("Fps.Tests.SettingsViewBindings.Settings." + suffix);
            _bindingStore = new InputBindingOverridesStore("Fps.Tests.SettingsViewBindings.Bindings." + suffix);
            _asset = ProjectActionsClone.Create();
            _view = new SettingsView(_store, _bindingStore, _asset);
            _container = new VisualElement();
        }

        [TearDown]
        public void TearDown()
        {
            _view.Close();
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.DeleteKey(_bindingStore.Key);
            PlayerPrefs.Save();
            ProjectActionsClone.Destroy(_asset);
            ProjectActionsClone.Destroy(_restarted);
            _asset = null;
            _restarted = null;
            AppliedControlSettings.Apply(_appliedBefore);
            AudioListener.volume = _volumeBefore;
        }

        [Test]
        public void OpenBuildsAButtonForEverySlotShowingItsKey()
        {
            _view.Open(_container);

            Assert.That(_view.Bindings, Is.Not.Null);
            Assert.That(_view.Bindings.Asset, Is.SameAs(_asset));
            foreach (var slot in PlayerBindings.Slots)
            {
                var button = _container.Q<Button>(SettingsView.RebindButtonName(slot));
                Assert.That(button, Is.Not.Null, slot.ToString());
                Assert.That(button.text, Is.EqualTo(_view.Bindings.DisplayOf(slot)), slot.ToString());
            }

            Assert.That(_container.Q<Button>(SettingsView.BindingsResetButtonName).enabledSelf, Is.True);
            Assert.That(_container.Q<Label>(SettingsView.RebindNoticeName), Is.Not.Null);
            Assert.That(IsDisplayed(_container.Q<Label>(SettingsView.RebindNoticeName).parent), Is.False, "nothing to tell yet");
            Assert.That(
                SettingsView.RebindButtonName(ReloadKey),
                Is.Not.EqualTo(SettingsView.RebindButtonName(ReloadPad)),
                "each device has its own button");
        }

        [Test]
        public void ChangingReloadFromRToFTakesEffectAtOnceAndIsSavedOnClose()
        {
            _view.Open(_container);
            var reload = _asset.FindAction(InputActionPaths.Reload);

            var result = _view.SetBinding(ReloadKey, "<Keyboard>/f");

            Assert.That(result.Changed, Is.True);
            Assert.That(result.Conflicts, Is.Empty);
            Assert.That(_view.PendingConflict, Is.Null);
            Assert.That(reload.bindings, Has.Some.Matches<InputBinding>(b => b.effectivePath == "<Keyboard>/f"));
            Assert.That(reload.bindings, Has.None.Matches<InputBinding>(b => b.effectivePath == "<Keyboard>/r"));
            Assert.That(_view.Bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonWest"), "the gamepad is separate");
            Assert.That(
                _container.Q<Button>(SettingsView.RebindButtonName(ReloadKey)).text,
                Is.EqualTo(_view.Bindings.DisplayOf(ReloadKey)));
            Assert.That(_container.Q<Button>(SettingsView.RebindButtonName(ReloadKey)).ClassListContains("settings__bind--overridden"), Is.True);
            Assert.That(_view.HasUnsavedChanges, Is.True);
            Assert.That(PlayerPrefs.HasKey(_bindingStore.Key), Is.False, "nothing is written until the view closes");

            Assert.That(_view.Close(), Is.True);

            // 起動し直したときと同じく、上書きの無いアセットに読み込む。
            _restarted = ProjectActionsClone.Create();
            Assert.That(new InputBindingOverridesStore(_bindingStore.Key).Load(_restarted), Is.True);
            Assert.That(new PlayerBindings(_restarted).PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False, "the other settings did not change");
        }

        [Test]
        public void TheSavedBindingIsShownWhenOpenedAgain()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadKey, "<Keyboard>/f");
            _view.Close();

            _view.Open(_container);

            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
            Assert.That(_view.HasUnsavedChanges, Is.False);
        }

        [Test]
        public void AnOverlapIsShownAndCanBeUndone()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadKey, "<Keyboard>/f");

            var result = _view.SetBinding(ReloadKey, "<Keyboard>/e");

            Assert.That(result.Conflicts, Has.Count.EqualTo(1));
            Assert.That(result.Conflicts[0].ActionName, Is.EqualTo("Interact"));
            Assert.That(_view.PendingConflict, Is.SameAs(result));
            var notice = _container.Q<Label>(SettingsView.RebindNoticeName);
            Assert.That(IsDisplayed(notice.parent), Is.True);
            Assert.That(notice.text, Does.Contain(PlayerBindings.LabelOf("Interact")));
            Assert.That(IsDisplayed(_container.Q<Button>(SettingsView.RebindUndoButtonName)), Is.True);
            Assert.That(IsDisplayed(_container.Q<Button>(SettingsView.RebindKeepButtonName)), Is.True);
            Assert.That(IsDisplayed(_container.Q<Button>(SettingsView.RebindCancelButtonName)), Is.False);
            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/e"), "it takes effect until undone");

            Assert.That(_view.UndoRebind(), Is.True);

            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"), "back to the binding before the overlap");
            Assert.That(_view.PendingConflict, Is.Null);
            Assert.That(IsDisplayed(notice.parent), Is.False);
            Assert.That(_view.UndoRebind(), Is.False, "nothing more to undo");
        }

        [Test]
        public void UndoingTheFirstChangeGoesBackToTheDefault()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadKey, "<Keyboard>/e");

            _view.UndoRebind();

            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
            Assert.That(_view.Bindings.IsOverridden(ReloadKey), Is.False);

            _view.Close();
            Assert.That(PlayerPrefs.HasKey(_bindingStore.Key), Is.False, "no override is left to save");
        }

        [Test]
        public void AnOverlapCanBeKept()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadPad, "<Gamepad>/buttonSouth");

            Assert.That(_view.PendingConflict.Conflicts[0].ActionName, Is.EqualTo("Jump"));
            Assert.That(_view.KeepRebind(), Is.True);

            Assert.That(_view.PendingConflict, Is.Null);
            Assert.That(_view.Bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonSouth"));
            Assert.That(IsDisplayed(_container.Q<Label>(SettingsView.RebindNoticeName).parent), Is.False);
        }

        [Test]
        public void ResetBindingsGoesBackToRAndIsSavedOnClose()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadKey, "<Keyboard>/f");
            _view.SetBinding(ReloadPad, "<Gamepad>/buttonNorth");
            _view.Close();
            Assert.That(PlayerPrefs.HasKey(_bindingStore.Key), Is.True);

            _view.Open(_container);
            _view.ResetBindingsToDefaults();

            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
            Assert.That(_view.Bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonWest"));
            Assert.That(_container.Q<Button>(SettingsView.RebindButtonName(ReloadKey)).ClassListContains("settings__bind--overridden"), Is.False);
            Assert.That(_view.HasUnsavedChanges, Is.True);

            Assert.That(_view.Close(), Is.True);

            Assert.That(PlayerPrefs.HasKey(_bindingStore.Key), Is.False);
            _restarted = ProjectActionsClone.Create();
            _bindingStore.Load(_restarted);
            Assert.That(new PlayerBindings(_restarted).PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
        }

        [Test]
        public void ResetBindingsLeavesTheOtherSettingsAlone()
        {
            AppliedControlSettings.Apply(ControlSettings.Default.With(ControlValue.MouseSensitivity, 250));
            _view.Open(_container);

            _view.ResetBindingsToDefaults();

            Assert.That(AppliedControlSettings.Current.Get(ControlValue.MouseSensitivity), Is.EqualTo(250));
            Assert.That(_view.HasUnsavedChanges, Is.False, "there were no overrides to reset");
        }

        [Test]
        public void WaitingForAnInputShowsItAndCancellingChangesNothing()
        {
            _view.Open(_container);
            var button = _container.Q<Button>(SettingsView.RebindButtonName(ReloadKey));

            Assert.That(_view.StartRebind(ReloadKey), Is.True);

            Assert.That(_view.Bindings.IsRebinding, Is.True);
            Assert.That(button.text, Is.EqualTo(SettingsView.WaitingText));
            Assert.That(IsDisplayed(_container.Q<Button>(SettingsView.RebindCancelButtonName)), Is.True);
            Assert.That(_container.Q<Label>(SettingsView.RebindNoticeName).text, Does.Contain("Esc"));
            Assert.That(_view.StartRebind(ReloadPad), Is.False, "one at a time");

            _view.CancelRebind();

            Assert.That(_view.Bindings.IsRebinding, Is.False);
            Assert.That(button.text, Is.EqualTo(_view.Bindings.DisplayOf(ReloadKey)));
            Assert.That(_view.Bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
            Assert.That(_view.HasUnsavedChanges, Is.False);
            Assert.That(IsDisplayed(_container.Q<Label>(SettingsView.RebindNoticeName).parent), Is.False);
        }

        [Test]
        public void ClosingWhileWaitingStopsWaiting()
        {
            _view.Open(_container);
            var bindings = _view.Bindings;
            _view.StartRebind(ReloadPad);

            Assert.That(_view.Close(), Is.False, "nothing changed");

            Assert.That(bindings.IsRebinding, Is.False);
            Assert.That(PlayerBindings.IsCapturingInput, Is.False);
            Assert.That(bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonWest"));
        }

        [Test]
        public void ClosingWithAnUndecidedOverlapKeepsIt()
        {
            _view.Open(_container);
            _view.SetBinding(ReloadKey, "<Keyboard>/e");

            Assert.That(_view.Close(), Is.True);

            _restarted = ProjectActionsClone.Create();
            _bindingStore.Load(_restarted);
            Assert.That(new PlayerBindings(_restarted).PathOf(ReloadKey), Is.EqualTo("<Keyboard>/e"));
        }

        [Test]
        public void RebindingNeedsAnOpenViewWithAPlayerMap()
        {
            Assert.Throws<InvalidOperationException>(() => _view.StartRebind(ReloadKey));
            Assert.Throws<InvalidOperationException>(() => _view.SetBinding(ReloadKey, "<Keyboard>/f"));
            Assert.Throws<InvalidOperationException>(() => _view.ResetBindingsToDefaults());
            Assert.That(_view.UndoRebind(), Is.False);
            Assert.That(_view.KeepRebind(), Is.False);

            var noPlayerMap = ScriptableObject.CreateInstance<InputActionAsset>();
            noPlayerMap.AddActionMap("UI");
            var view = new SettingsView(_store, _bindingStore, noPlayerMap);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"^\[Settings\] .*has no 'Player' action map"));
                view.Open(new VisualElement());

                Assert.That(view.IsOpen, Is.True, "the other settings still work");
                Assert.That(view.Bindings, Is.Null);
                Assert.That(view.Root.Q<Button>(SettingsView.BindingsResetButtonName).enabledSelf, Is.False);
                Assert.Throws<InvalidOperationException>(() => view.StartRebind(ReloadKey));
            }
            finally
            {
                view.Close();
                Object.DestroyImmediate(noPlayerMap);
            }
        }

        [Test]
        public void TheOverlapNoticeNamesEachActionOnce()
        {
            var conflicts = new[]
            {
                new BindingConflict("Move", "up", 2, "前へ移動"),
                new BindingConflict("Move", "up", 3, "前へ移動"),
                new BindingConflict("Interact", null, 0, "調べる・起こす"),
            };

            Assert.That(SettingsView.FormatConflict("W", conflicts), Is.EqualTo("W は「前へ移動」「調べる・起こす」にも割り当てられています。"));
            Assert.That(SettingsView.FormatConflict("W", Array.Empty<BindingConflict>()), Is.Empty);
        }

        private static bool IsDisplayed(VisualElement element)
        {
            return element != null && element.style.display.value != DisplayStyle.None;
        }
    }
}
