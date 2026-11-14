using System;
using System.Linq;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="PlayerBindings"/> が設定画面の各行の割り当てを見つけ、上書きと重なりの検出をすること。
    /// 上書きはアセットに残るので、プロジェクト全体のアクションの写しで試す。
    /// </summary>
    public sealed class PlayerBindingsTests
    {
        private static readonly RebindSlot ReloadKey = new RebindSlot("Reload", InputDeviceKind.KeyboardMouse);
        private static readonly RebindSlot ReloadPad = new RebindSlot("Reload", InputDeviceKind.Gamepad);
        private static readonly RebindSlot JumpPad = new RebindSlot("Jump", InputDeviceKind.Gamepad);
        private static readonly RebindSlot MoveUpKey = new RebindSlot("Move", InputDeviceKind.KeyboardMouse, "up");

        private InputActionAsset _asset;
        private PlayerBindings _bindings;
        private ControlSettings _appliedBefore;

        [SetUp]
        public void SetUp()
        {
            // 重なりの名前を日本語で確かめるので、この機械で保存した言語に依らないようにする
            _appliedBefore = AppliedControlSettings.Current;
            AppliedControlSettings.Apply(_appliedBefore.WithLanguage(GameLanguage.Japanese));
            _asset = ProjectActionsClone.Create();
            _bindings = new PlayerBindings(_asset);
        }

        [TearDown]
        public void TearDown()
        {
            _bindings.Dispose();
            ProjectActionsClone.Destroy(_asset);
            AppliedControlSettings.Apply(_appliedBefore);
        }

        [Test]
        public void EverySlotHasABindingInItsDeviceGroup()
        {
            Assert.That(PlayerBindings.Slots, Is.Not.Empty);
            foreach (var slot in PlayerBindings.Slots)
            {
                var index = _bindings.BindingIndexOf(slot);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), slot.ToString());

                var binding = _asset.FindActionMap(PlayerBindings.MapName).FindAction(slot.ActionName).bindings[index];
                Assert.That(InputBinding.MaskByGroup(PlayerBindings.GroupOf(slot.Device)).Matches(binding), Is.True, slot.ToString());
                Assert.That(binding.isComposite, Is.False, slot.ToString());
                Assert.That(binding.isPartOfComposite, Is.EqualTo(slot.Part != null), slot.ToString());
                Assert.That(_bindings.PathOf(slot), Is.Not.Empty, slot.ToString());
                Assert.That(_bindings.DisplayOf(slot), Is.Not.Empty, slot.ToString());
                Assert.That(PlayerBindings.LabelOf(slot.ActionName, slot.Part), Is.Not.EqualTo(slot.ActionName), "every row has a label for the player");
            }
        }

        [Test]
        public void TheSlotsAreTheKeyboardAndGamepadButtonsOfThePlayerMap()
        {
            Assert.That(PlayerBindings.Slots.Distinct().Count(), Is.EqualTo(PlayerBindings.Slots.Count), "no slot is listed twice");
            Assert.That(PlayerBindings.Slots, Has.Member(ReloadKey));
            Assert.That(PlayerBindings.Slots, Has.Member(ReloadPad));
            Assert.That(PlayerBindings.Slots, Has.Member(MoveUpKey));
            Assert.That(PlayerBindings.Slots, Has.None.Matches<RebindSlot>(s => s.ActionName == "Look"));
            Assert.That(PlayerBindings.Slots, Has.None.Matches<RebindSlot>(s => s.ActionName == "Pause"), "the menu must stay reachable");
            Assert.That(
                PlayerBindings.Slots,
                Has.None.Matches<RebindSlot>(s => s.ActionName == "Move" && s.Device == InputDeviceKind.Gamepad),
                "the gamepad moves with the stick");
        }

        [Test]
        public void SlotsPointAtTheFirstBindingOfTheirGroup()
        {
            Assert.That(_bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
            Assert.That(_bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonWest"));
            Assert.That(_bindings.PathOf(MoveUpKey), Is.EqualTo("<Keyboard>/w"), "not the arrow key");
            Assert.That(_bindings.PathOf(new RebindSlot("Next", InputDeviceKind.KeyboardMouse)), Is.EqualTo("<Keyboard>/2"));
            Assert.That(_bindings.BindingIndexOf(new RebindSlot("Nope", InputDeviceKind.KeyboardMouse)), Is.EqualTo(-1));
            Assert.That(_bindings.Has(new RebindSlot("Move", InputDeviceKind.Gamepad, "up")), Is.False);
        }

        [Test]
        public void TheDefaultBindingsDoNotOverlap()
        {
            foreach (var slot in PlayerBindings.Slots)
            {
                Assert.That(_bindings.FindConflicts(slot), Is.Empty, slot.ToString());
            }
        }

        [Test]
        public void AKeyUsedByAnotherActionIsAConflict()
        {
            var conflicts = _bindings.FindConflicts(ReloadKey, "<Keyboard>/e");

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].ActionName, Is.EqualTo("Interact"));
            Assert.That(conflicts[0].Part, Is.Null);
            Assert.That(conflicts[0].Label, Is.EqualTo(PlayerBindings.LabelOf("Interact")));
        }

        [Test]
        public void APartOfTheMoveCompositeIsAConflict()
        {
            var conflicts = _bindings.FindConflicts(ReloadKey, "<Keyboard>/w");

            Assert.That(conflicts, Has.Count.EqualTo(1));
            Assert.That(conflicts[0].ActionName, Is.EqualTo("Move"));
            Assert.That(conflicts[0].Part, Is.EqualTo("up"));
            Assert.That(conflicts[0].Label, Is.EqualTo("前へ移動"));
        }

        [Test]
        public void ABindingThatIsNotASlotStillCounts()
        {
            // 上矢印は設定画面の行には無いが、Move に割り当たっている。
            var conflicts = _bindings.FindConflicts(ReloadKey, "<Keyboard>/upArrow");

            Assert.That(conflicts.Select(c => c.ActionName), Is.EqualTo(new[] { "Move" }));
        }

        [Test]
        public void GamepadConflictsAreFoundToo()
        {
            var conflicts = _bindings.FindConflicts(JumpPad, "<Gamepad>/buttonWest");

            Assert.That(conflicts.Select(c => c.ActionName), Is.EqualTo(new[] { "Reload" }));
        }

        [Test]
        public void PathsAreComparedIgnoringCase()
        {
            Assert.That(_bindings.FindConflicts(ReloadKey, "<keyboard>/E"), Has.Count.EqualTo(1));
        }

        [Test]
        public void AFreeKeyAndTheSlotItselfAreNotConflicts()
        {
            Assert.That(_bindings.FindConflicts(ReloadKey, "<Keyboard>/f"), Is.Empty);
            Assert.That(_bindings.FindConflicts(ReloadKey, "<Keyboard>/r"), Is.Empty, "keeping the same key");
            Assert.That(_bindings.FindConflicts(ReloadKey, null), Is.Empty);
        }

        [Test]
        public void AnOverrideIsSeenByTheConflictCheckOfOtherSlots()
        {
            _bindings.ApplyOverride(ReloadKey, "<Keyboard>/f");

            Assert.That(_bindings.FindConflicts(new RebindSlot("Interact", InputDeviceKind.KeyboardMouse), "<Keyboard>/r"), Is.Empty, "R is free now");
            var conflicts = _bindings.FindConflicts(new RebindSlot("Interact", InputDeviceKind.KeyboardMouse), "<Keyboard>/f");
            Assert.That(conflicts.Select(c => c.ActionName), Is.EqualTo(new[] { "Reload" }));
        }

        [Test]
        public void FindConflictsOfASlotUsesItsCurrentBinding()
        {
            _bindings.ApplyOverride(ReloadKey, "<Keyboard>/e");

            Assert.That(_bindings.FindConflicts(ReloadKey).Select(c => c.ActionName), Is.EqualTo(new[] { "Interact" }));
        }

        [Test]
        public void OverridesChangeOnlyTheirSlotAndCanBeRestoredOrReset()
        {
            _bindings.ApplyOverride(ReloadKey, "<Keyboard>/f");

            Assert.That(_bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
            Assert.That(_bindings.OverridePathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));
            Assert.That(_bindings.IsOverridden(ReloadKey), Is.True);
            Assert.That(_bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonWest"), "the gamepad is separate");
            Assert.That(_bindings.HasOverrides, Is.True);

            _bindings.ApplyOverride(ReloadKey, "<Keyboard>/t");
            _bindings.RestoreOverride(ReloadKey, "<Keyboard>/f");
            Assert.That(_bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/f"));

            _bindings.RestoreOverride(ReloadKey, null);
            Assert.That(_bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
            Assert.That(_bindings.IsOverridden(ReloadKey), Is.False);

            _bindings.ApplyOverride(ReloadPad, "<Gamepad>/buttonNorth");
            _bindings.ApplyOverride(MoveUpKey, "<Keyboard>/i");
            _bindings.ResetAll();
            Assert.That(_bindings.HasOverrides, Is.False);
            Assert.That(_bindings.PathOf(ReloadPad), Is.EqualTo("<Gamepad>/buttonWest"));
            Assert.That(_bindings.PathOf(MoveUpKey), Is.EqualTo("<Keyboard>/w"));
        }

        [Test]
        public void StartRebindWaitsUntilCancelledAndLeavesTheBindingAlone()
        {
            var reload = _asset.FindAction(InputActionPaths.Reload);
            reload.Enable();
            RebindResult result = null;

            Assert.That(_bindings.StartRebind(ReloadKey, r => result = r), Is.True);

            Assert.That(_bindings.IsRebinding, Is.True);
            Assert.That(_bindings.RebindingSlot, Is.EqualTo(ReloadKey));
            Assert.That(PlayerBindings.IsCapturingInput, Is.True);
            Assert.That(reload.enabled, Is.False, "an enabled action cannot be rebound");
            Assert.That(_bindings.StartRebind(ReloadPad, _ => { }), Is.False, "one at a time");

            _bindings.CancelRebind();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Completed, Is.False);
            Assert.That(result.Changed, Is.False);
            Assert.That(_bindings.IsRebinding, Is.False);
            Assert.That(PlayerBindings.IsCapturingInput, Is.False);
            Assert.That(reload.enabled, Is.True, "enabled again afterwards");
            Assert.That(_bindings.PathOf(ReloadKey), Is.EqualTo("<Keyboard>/r"));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayerBindings(null));
            Assert.Throws<ArgumentException>(() => _bindings.ApplyOverride(ReloadKey, string.Empty));
            Assert.Throws<ArgumentException>(() => _bindings.ApplyOverride(new RebindSlot("Nope", InputDeviceKind.Gamepad), "<Gamepad>/start"));
            Assert.Throws<ArgumentException>(() => _bindings.StartRebind(new RebindSlot("Nope", InputDeviceKind.Gamepad), _ => { }));
            Assert.Throws<ArgumentNullException>(() => _bindings.StartRebind(ReloadKey, null));
            Assert.Throws<ArgumentException>(() => new RebindSlot(string.Empty, InputDeviceKind.Gamepad));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RebindSlot("Reload", (InputDeviceKind)9));

            var noPlayerMap = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                noPlayerMap.AddActionMap("UI");
                Assert.Throws<ArgumentException>(() => new PlayerBindings(noPlayerMap));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(noPlayerMap);
            }
        }
    }
}
