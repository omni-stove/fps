using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// スティックのデッドゾーンを processor の上書きとして付け、割り当ての上書き (<see cref="PlayerBindings"/>・<see cref="InputBindingOverridesStore"/>) と互いを消さないこと。
    /// プロジェクト全体のアクションと本物の設定を壊さないよう、アセットは写しを使い、キーはテストごとに変える。
    /// </summary>
    public sealed class StickDeadzoneApplierTests
    {
        private static readonly RebindSlot ReloadKey = new RebindSlot("Reload", InputDeviceKind.KeyboardMouse);
        private static readonly RebindSlot ReloadPad = new RebindSlot("Reload", InputDeviceKind.Gamepad);

        private readonly List<InputActionAsset> _assets = new List<InputActionAsset>();
        private readonly List<GameObject> _objects = new List<GameObject>();
        private InputBindingOverridesStore _store;

        [SetUp]
        public void SetUp()
        {
            _store = new InputBindingOverridesStore("Shooter.Tests.StickDeadzoneApplier." + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.Save();
            foreach (var go in _objects)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            foreach (var asset in _assets)
            {
                ProjectActionsClone.Destroy(asset);
            }

            _objects.Clear();
            _assets.Clear();
        }

        [TestCase(null, 0.2f, "stickDeadzone(min=0.2)")]
        [TestCase("", 0.125f, "stickDeadzone(min=0.125)")]
        [TestCase(null, 0.5f, "stickDeadzone(min=0.5)")]
        [TestCase(null, 0.001f, "stickDeadzone(min=0.001)")]
        [TestCase(null, 0f, "stickDeadzone(min=0.0001)")]
        [TestCase("invertVector2", 0.3f, "stickDeadzone(min=0.3),invertVector2")]
        [TestCase("invertVector2, stickDeadzone(min=0.1,max=0.9) ,scaleVector2(x=2,y=3)", 0.3f, "stickDeadzone(min=0.3),invertVector2,scaleVector2(x=2,y=3)")]
        [TestCase("StickDeadzone", 0.4f, "stickDeadzone(min=0.4)")]
        public void ProcessorsPutTheDeadzoneFirstAndReplaceAnEarlierOne(string baseProcessors, float deadzone, string expected)
        {
            Assert.That(StickDeadzoneApplier.ProcessorsFor(baseProcessors, deadzone), Is.EqualTo(expected));
        }

        [Test]
        public void ProcessorsRejectAnOutOfRangeDeadzone()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StickDeadzoneApplier.ProcessorsFor(null, -0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => StickDeadzoneApplier.ProcessorsFor(null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => StickDeadzoneApplier.ProcessorsFor(null, float.NaN));
        }

        [Test]
        public void ApplyPutsTheProcessorOnBothSticksOnly()
        {
            var asset = NewAsset();

            var changed = StickDeadzoneApplier.Apply(asset, 0.25f);

            Assert.That(changed, Is.GreaterThanOrEqualTo(2));
            Assert.That(LeftStick(asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.25)"));
            Assert.That(RightStick(asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.25)"));
            Assert.That(LeftStick(asset).effectiveProcessors, Is.EqualTo("stickDeadzone(min=0.25)"));
            Assert.That(LeftStick(asset).overridePath, Is.Null, "the stick keeps its own path");

            foreach (var map in asset.actionMaps)
            {
                foreach (var binding in map.bindings)
                {
                    if (!StickDeadzoneApplier.IsStickBinding(binding))
                    {
                        Assert.That(binding.overrideProcessors, Is.Null, $"{binding.action} {binding.path} is not a stick");
                    }
                }
            }
        }

        [Test]
        public void ApplyingTheSameDeadzoneAgainChangesNothing()
        {
            var asset = NewAsset();
            StickDeadzoneApplier.Apply(asset, 0.25f);

            Assert.That(StickDeadzoneApplier.Apply(asset, 0.25f), Is.EqualTo(0));
            Assert.That(LeftStick(asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.25)"));
        }

        [Test]
        public void ApplyingANewDeadzoneReplacesTheOldOne()
        {
            var asset = NewAsset();
            StickDeadzoneApplier.Apply(asset, 0.1f);

            StickDeadzoneApplier.Apply(asset, 0.4f);

            Assert.That(LeftStick(asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.4)"));
            Assert.That(RightStick(asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.4)"));
        }

        [Test]
        public void ApplyKeepsTheRebindsAndTheStickOverrides()
        {
            var bindings = new PlayerBindings(NewAsset());
            bindings.ApplyOverride(ReloadKey, "<Keyboard>/f");
            bindings.ApplyOverride(ReloadPad, "<Gamepad>/buttonNorth");
            var move = bindings.Asset.FindAction(InputActionPaths.Move);
            var stickIndex = IndexOf(move, StickDeadzoneApplier.LeftStickPath);
            move.ApplyBindingOverride(stickIndex, new InputBinding { overridePath = StickDeadzoneApplier.LeftStickPath, overrideInteractions = "press" });

            StickDeadzoneApplier.Apply(bindings.Asset, 0.3f);

            Assert.That(bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
            Assert.That(bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonNorth"));
            var stick = move.bindings[stickIndex];
            Assert.That(stick.overridePath, Is.EqualTo(StickDeadzoneApplier.LeftStickPath));
            Assert.That(stick.overrideInteractions, Is.EqualTo("press"));
            Assert.That(stick.overrideProcessors, Is.EqualTo("stickDeadzone(min=0.3)"));
        }

        [Test]
        public void RebindingAfterApplyKeepsTheDeadzone()
        {
            var bindings = new PlayerBindings(NewAsset());
            StickDeadzoneApplier.Apply(bindings.Asset, 0.3f);

            bindings.ApplyOverride(ReloadKey, "<Keyboard>/f");

            Assert.That(LeftStick(bindings.Asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.3)"));
            Assert.That(RightStick(bindings.Asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.3)"));
            Assert.That(bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
        }

        [Test]
        public void ApplyingAfterLoadingSavedRebindsKeepsBoth()
        {
            var saved = new PlayerBindings(NewAsset());
            saved.ApplyOverride(ReloadKey, "<Keyboard>/f");
            _store.Save(saved.Asset);

            // 起動し直したときと同じく、デッドゾーンを付けたあとで保存した上書きを読み込み、読み直しで外れた processor を付け直す。
            var restarted = new PlayerBindings(NewAsset());
            StickDeadzoneApplier.Apply(restarted.Asset, 0.2f);
            Assert.That(_store.Load(restarted.Asset), Is.True);
            Assert.That(LeftStick(restarted.Asset).overrideProcessors, Is.Null, "loading replaces every override");

            StickDeadzoneApplier.Apply(restarted.Asset, 0.2f);

            Assert.That(restarted.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
            Assert.That(LeftStick(restarted.Asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.2)"));
            Assert.That(RightStick(restarted.Asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.2)"));
        }

        [Test]
        public void ApplyingReplacesAStaleDeadzoneThatWasSavedWithTheRebinds()
        {
            var saved = new PlayerBindings(NewAsset());
            saved.ApplyOverride(ReloadKey, "<Keyboard>/f");
            StickDeadzoneApplier.Apply(saved.Asset, 0.1f);
            _store.Save(saved.Asset);

            var restarted = new PlayerBindings(NewAsset());
            _store.Load(restarted.Asset);
            StickDeadzoneApplier.Apply(restarted.Asset, 0.35f);

            Assert.That(restarted.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
            Assert.That(LeftStick(restarted.Asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.35)"));
        }

        [Test]
        public void RemoveTakesOffOnlyTheProcessor()
        {
            var bindings = new PlayerBindings(NewAsset());
            bindings.ApplyOverride(ReloadKey, "<Keyboard>/f");
            var look = bindings.Asset.FindAction(InputActionPaths.Look);
            var stickIndex = IndexOf(look, StickDeadzoneApplier.RightStickPath);
            look.ApplyBindingOverride(stickIndex, new InputBinding { overridePath = StickDeadzoneApplier.RightStickPath });
            StickDeadzoneApplier.Apply(bindings.Asset, 0.3f);

            var removed = StickDeadzoneApplier.Remove(bindings.Asset);

            Assert.That(removed, Is.GreaterThanOrEqualTo(2));
            Assert.That(LeftStick(bindings.Asset).overrideProcessors, Is.Null);
            Assert.That(LeftStick(bindings.Asset).overridePath, Is.Null);
            Assert.That(look.bindings[stickIndex].overrideProcessors, Is.Null);
            Assert.That(look.bindings[stickIndex].overridePath, Is.EqualTo(StickDeadzoneApplier.RightStickPath));
            Assert.That(bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
            Assert.That(StickDeadzoneApplier.Remove(bindings.Asset), Is.EqualTo(0));
        }

        [Test]
        public void SavingAfterRemovingWithoutRebindsDeletesTheKey()
        {
            var asset = NewAsset();
            StickDeadzoneApplier.Apply(asset, 0.3f);
            StickDeadzoneApplier.Remove(asset);

            _store.Save(asset);

            Assert.That(_store.HasSaved, Is.False);
        }

        [Test]
        public void RefreshAppliesTheSettingsDeadzoneToTheGivenActions()
        {
            var asset = NewAsset();
            var applier = NewApplier();
            applier.Actions = asset;

            applier.Refresh(ControlSettings.Default.WithStickDeadzone(300));

            Assert.That(applier.AppliedActions, Is.SameAs(asset));
            Assert.That(LeftStick(asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.3)"));

            applier.Refresh(ControlSettings.Default.WithStickDeadzone(0));

            Assert.That(RightStick(asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.0001)"));
        }

        [Test]
        public void TheDefaultSettingMatchesTheInputSystemDefault()
        {
            var asset = NewAsset();
            var applier = NewApplier();
            applier.Actions = asset;

            applier.Refresh(ControlSettings.Default);

            Assert.That(LeftStick(asset).overrideProcessors, Is.EqualTo("stickDeadzone(min=0.125)"));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => StickDeadzoneApplier.Apply(null, 0.1f));
            Assert.Throws<ArgumentNullException>(() => StickDeadzoneApplier.Remove(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => StickDeadzoneApplier.Apply(NewAsset(), 1.5f));
            Assert.Throws<ArgumentNullException>(() => NewApplier().Refresh(null));
        }

        [Test]
        public void AnOutOfRangeDeadzoneLeavesEveryBindingAlone()
        {
            var asset = NewAsset();

            Assert.Throws<ArgumentOutOfRangeException>(() => StickDeadzoneApplier.Apply(asset, -1f));

            Assert.That(LeftStick(asset).overrideProcessors, Is.Null);
            Assert.That(RightStick(asset).overrideProcessors, Is.Null);
        }

        private InputActionAsset NewAsset()
        {
            var asset = ProjectActionsClone.Create();
            _assets.Add(asset);
            return asset;
        }

        private StickDeadzoneApplier NewApplier()
        {
            var go = new GameObject("StickDeadzoneApplierTests");
            _objects.Add(go);
            return go.AddComponent<StickDeadzoneApplier>();
        }

        private static InputBinding LeftStick(InputActionAsset asset)
        {
            var move = asset.FindAction(InputActionPaths.Move);
            return move.bindings[IndexOf(move, StickDeadzoneApplier.LeftStickPath)];
        }

        private static InputBinding RightStick(InputActionAsset asset)
        {
            var look = asset.FindAction(InputActionPaths.Look);
            return look.bindings[IndexOf(look, StickDeadzoneApplier.RightStickPath)];
        }

        private static int IndexOf(InputAction action, string path)
        {
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                if (string.Equals(bindings[i].path, path, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            Assert.Fail($"{action.name} has no binding for {path}");
            return -1;
        }
    }
}
