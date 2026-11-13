using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 割り当ての上書きを PlayerPrefs に保存し、起動し直したとき (新しいアセットの写し) に読み込めること。
    /// 本物の設定とプロジェクト全体のアクションを壊さないよう、キーはテストごとに変え、アセットは写しを使う。
    /// </summary>
    public sealed class InputBindingOverridesStoreTests
    {
        private static readonly RebindSlot ReloadKey = new RebindSlot("Reload", InputDeviceKind.KeyboardMouse);
        private static readonly RebindSlot ReloadPad = new RebindSlot("Reload", InputDeviceKind.Gamepad);
        private static readonly RebindSlot MoveUpKey = new RebindSlot("Move", InputDeviceKind.KeyboardMouse, "up");

        private readonly List<InputActionAsset> _assets = new List<InputActionAsset>();
        private InputBindingOverridesStore _store;

        [SetUp]
        public void SetUp()
        {
            _store = new InputBindingOverridesStore("Fps.Tests.InputBindingOverrides." + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.Save();
            foreach (var asset in _assets)
            {
                ProjectActionsClone.Destroy(asset);
            }

            _assets.Clear();
        }

        [Test]
        public void SavedOverridesAreLoadedBackAfterARestart()
        {
            var before = NewBindings();
            before.ApplyOverride(ReloadKey, "<Keyboard>/f");
            before.ApplyOverride(ReloadPad, "<Gamepad>/buttonNorth");
            before.ApplyOverride(MoveUpKey, "<Keyboard>/i");

            _store.Save(before.Asset);

            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.True);
            Assert.That(PlayerPrefs.GetString(_store.Key), Does.Contain("<Keyboard>/f"));

            // 起動し直したときと同じく、上書きの無い新しいアセットに新しいストアで読む。
            var after = NewBindings();
            Assert.That(after.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));

            Assert.That(new InputBindingOverridesStore(_store.Key).Load(after.Asset), Is.True);

            Assert.That(after.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
            Assert.That(after.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonNorth"));
            Assert.That(after.PathOf(MoveUpKey), Is.EqualTo("<Keyboard>/i"));
            Assert.That(after.PathOf(new RebindSlot("Jump", InputDeviceKind.KeyboardMouse)), Is.EqualTo("<Keyboard>/space"), "untouched bindings stay");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheLoadedOverrideTakesEffectOnTheAction()
        {
            var before = NewBindings();
            before.ApplyOverride(ReloadKey, "<Keyboard>/f");
            _store.Save(before.Asset);

            var after = NewBindings();
            _store.Load(after.Asset);

            var reload = after.Asset.FindAction(InputActionPaths.Reload);
            Assert.That(reload.bindings, Has.Some.Matches<InputBinding>(b => b.effectivePath == "<Keyboard>/f"));
            Assert.That(reload.bindings, Has.None.Matches<InputBinding>(b => b.effectivePath == "<Keyboard>/r"));
        }

        [Test]
        public void SavingWithoutOverridesRemovesTheKeySoTheDefaultsComeBack()
        {
            var bindings = NewBindings();
            bindings.ApplyOverride(ReloadKey, "<Keyboard>/f");
            _store.Save(bindings.Asset);

            bindings.ResetAll();
            _store.Save(bindings.Asset);

            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False);
            var after = NewBindings();
            Assert.That(_store.Load(after.Asset), Is.False);
            Assert.That(after.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
        }

        [Test]
        public void LoadingWithNothingSavedClearsEarlierOverridesSilently()
        {
            var bindings = NewBindings();
            bindings.ApplyOverride(ReloadKey, "<Keyboard>/f");

            Assert.That(_store.Load(bindings.Asset), Is.False);

            Assert.That(bindings.HasOverrides, Is.False);
            Assert.That(bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LoadingReplacesEarlierOverrides()
        {
            var saved = NewBindings();
            saved.ApplyOverride(ReloadKey, "<Keyboard>/f");
            _store.Save(saved.Asset);

            var bindings = NewBindings();
            bindings.ApplyOverride(ReloadPad, "<Gamepad>/buttonNorth");
            _store.Load(bindings.Asset);

            Assert.That(bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
            Assert.That(bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonWest"));
        }

        [Test]
        public void BrokenTextFallsBackToTheDefaultsWithAWarning()
        {
            var bindings = NewBindings();
            bindings.ApplyOverride(ReloadKey, "<Keyboard>/f");
            PlayerPrefs.SetString(_store.Key, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] binding overrides .*could not be read"));

            Assert.That(_store.Load(bindings.Asset), Is.False);

            Assert.That(bindings.HasOverrides, Is.False);
            Assert.That(bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValueOfAnotherTypeFallsBackToTheDefaultsWithAWarning()
        {
            var bindings = NewBindings();
            PlayerPrefs.SetInt(_store.Key, 3);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Input\] binding overrides "));

            Assert.That(_store.Load(bindings.Asset), Is.False);
            Assert.That(bindings.HasOverrides, Is.False);
        }

        [Test]
        public void DeleteRemovesTheKey()
        {
            var bindings = NewBindings();
            bindings.ApplyOverride(ReloadKey, "<Keyboard>/f");
            _store.Save(bindings.Asset);

            _store.Delete();

            Assert.That(_store.HasSaved, Is.False);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new InputBindingOverridesStore(null));
            Assert.Throws<ArgumentException>(() => new InputBindingOverridesStore(string.Empty));
            Assert.Throws<ArgumentNullException>(() => _store.Save(null));
            Assert.Throws<ArgumentNullException>(() => _store.Load(null));
        }

        [Test]
        public void UsesTheGameKeyByDefaultAndItDiffersFromTheOtherSettings()
        {
            Assert.That(new InputBindingOverridesStore().Key, Is.EqualTo(InputBindingOverridesStore.DefaultKey));
            Assert.That(InputBindingOverridesStore.DefaultKey, Is.Not.EqualTo(ControlSettingsStore.DefaultKey));
        }

        private PlayerBindings NewBindings()
        {
            var asset = ProjectActionsClone.Create();
            _assets.Add(asset);
            return new PlayerBindings(asset);
        }
    }
}
