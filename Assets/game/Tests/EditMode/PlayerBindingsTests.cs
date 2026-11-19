using System;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Tests.EditMode
{
    // 上書きはアセットに残るので、プロジェクト全体のアクションの写しで試す。
    public sealed class PlayerBindingsTests
    {
        private static readonly RebindSlot ReloadKey = new RebindSlot("Reload", InputDeviceKind.KeyboardMouse);
        private static readonly RebindSlot ReloadPad = new RebindSlot("Reload", InputDeviceKind.Gamepad);
        private static readonly RebindSlot JumpPad = new RebindSlot("Jump", InputDeviceKind.Gamepad);
        private static readonly RebindSlot MoveUpKey = new RebindSlot("Move", InputDeviceKind.KeyboardMouse, "up");

        private InputActionAsset _asset;
        private PlayerBindings _bindings;

        [SetUp]
        public void SetUp()
        {
            _asset = ProjectActionsClone.Create();
            _bindings = new PlayerBindings(_asset, KeySharingSpec.Default);
        }

        [TearDown]
        public void TearDown()
        {
            _bindings.Dispose();
            ProjectActionsClone.Destroy(_asset);
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

        [TestCase("Melee")]
        [TestCase("Prone")]
        [TestCase("Ability1")]
        [TestCase("Ability2")]
        [TestCase("SquadCommand")]
        [TestCase("SwitchAmmoType")]
        [TestCase("CycleMagnification")]
        [TestCase("HoldBreath")]
        [TestCase("SwitchGrenade")]
        [TestCase("SwitchStreak")]
        [TestCase("Inventory")]
        [TestCase("Previous")]
        [TestCase("LeanLeft")]
        [TestCase("LeanRight")]
        [TestCase("Chat")]
        [TestCase("TeamChat")]
        [TestCase("PushToTalk")]
        public void KeyboardOnlyButtonsHaveOnlyAKeyboardRow(string actionName)
        {
            Assert.That(PlayerBindings.Slots, Has.Member(new RebindSlot(actionName, InputDeviceKind.KeyboardMouse)));
            Assert.That(PlayerBindings.Slots, Has.No.Member(new RebindSlot(actionName, InputDeviceKind.Gamepad)));
        }

        [TestCase("Emote", "<Gamepad>/dpad/up", "Ping")]
        [TestCase("Countermeasure", "<Gamepad>/rightShoulder", "Cover")]
        public void EmoteAndCountermeasureHaveAGamepadRowSharedOnlyWithTheirPartner(string actionName, string path, string partner)
        {
            var slot = new RebindSlot(actionName, InputDeviceKind.Gamepad);

            Assert.That(PlayerBindings.Slots, Has.Member(slot));
            Assert.That(_bindings.PathOf(slot), Is.EqualTo(path));
            Assert.That(_bindings.FindConflicts(slot).Select(c => c.ActionName), Is.EqualTo(new[] { partner }), "without key sharing the partner overlaps");
            using (var bundled = new PlayerBindings(_asset))
            {
                Assert.That(bundled.FindConflicts(slot), Is.Empty, "the bundled key-sharing.json allows the pair");
                Assert.That(bundled.FindConflicts(new RebindSlot(partner, InputDeviceKind.Gamepad)), Is.Empty, "the rule works from either side");
                Assert.That(bundled.FindConflicts(slot, "<Gamepad>/buttonSouth").Select(c => c.ActionName), Is.EqualTo(new[] { "Jump" }), "other buttons still conflict");
            }
        }

        [TestCase("LeanLeft", "<Mouse>/backButton")]
        [TestCase("LeanRight", "<Mouse>/forwardButton")]
        [TestCase("Chat", "<Keyboard>/enter")]
        [TestCase("TeamChat", "<Keyboard>/u")]
        [TestCase("PushToTalk", "<Keyboard>/capsLock")]
        [TestCase("Emote", "<Keyboard>/j")]
        [TestCase("Countermeasure", "<Keyboard>/f")]
        public void LeanChatAndVoiceStartOnTheirKeys(string actionName, string path)
        {
            var slot = new RebindSlot(actionName, InputDeviceKind.KeyboardMouse);

            Assert.That(_bindings.PathOf(slot), Is.EqualTo(path));
            Assert.That(_bindings.FindConflicts(slot), Is.Empty);
        }

        [Test]
        public void InventoryOpensWithI()
        {
            Assert.That(_bindings.PathOf(new RebindSlot("Inventory", InputDeviceKind.KeyboardMouse)), Is.EqualTo("<Keyboard>/i"));
        }

        [Test]
        public void UseStreakIsOn4AndTheRightOfTheDpad()
        {
            Assert.That(_bindings.PathOf(new RebindSlot("UseStreak", InputDeviceKind.KeyboardMouse)), Is.EqualTo("<Keyboard>/4"));
            Assert.That(_bindings.PathOf(new RebindSlot("UseStreak", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/dpad/right"));
            Assert.That(PlayerBindings.Slots, Has.Member(new RebindSlot("UseStreak", InputDeviceKind.Gamepad)));
        }

        [Test]
        public void SwitchStreakIsOn3NextToUseStreak()
        {
            var slot = new RebindSlot("SwitchStreak", InputDeviceKind.KeyboardMouse);

            Assert.That(_bindings.PathOf(slot), Is.EqualTo("<Keyboard>/3"));
            Assert.That(_bindings.FindConflicts(slot), Is.Empty);
        }

        [Test]
        public void NextIsOnTheLeftOfTheDpad()
        {
            Assert.That(_bindings.PathOf(new RebindSlot("Next", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/dpad/left"));
        }

        [Test]
        public void PingHasAKeyboardAndAGamepadRow()
        {
            Assert.That(_bindings.PathOf(new RebindSlot("Ping", InputDeviceKind.KeyboardMouse)), Is.EqualTo("<Mouse>/middleButton"));
            Assert.That(_bindings.PathOf(new RebindSlot("Ping", InputDeviceKind.Gamepad)), Is.EqualTo("<Gamepad>/dpad/up"));
            Assert.That(PlayerBindings.Slots, Has.Member(new RebindSlot("Ping", InputDeviceKind.Gamepad)));
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
        public void TheDefaultBindingsOverlapOnlyWhereTheBundledKeySharingAllowsIt()
        {
            using (var bundled = new PlayerBindings(_asset))
            {
                foreach (var slot in PlayerBindings.Slots)
                {
                    Assert.That(bundled.FindConflicts(slot), Is.Empty, slot.ToString());
                    foreach (var conflict in _bindings.FindConflicts(slot))
                    {
                        Assert.That(KitTuning.KeySharing.Allows(slot.ActionName, conflict.ActionName), Is.True, slot + " overlaps " + conflict);
                    }
                }
            }
        }

        [Test]
        public void AKeyUsedByAnotherActionIsAConflict()
        {
            var conflicts = _bindings.FindConflicts(ReloadKey, "<Keyboard>/e");

            Assert.That(conflicts.Count(), Is.EqualTo(1));
            Assert.That(conflicts[0].ActionName, Is.EqualTo("Interact"));
            Assert.That(conflicts[0].Part, Is.Null);
            Assert.That(conflicts[0].BindingIndex, Is.EqualTo(_bindings.BindingIndexOf(new RebindSlot("Interact", InputDeviceKind.KeyboardMouse))));
            Assert.That(conflicts[0].ToString(), Does.StartWith("Interact["));
        }

        [Test]
        public void APartOfTheMoveCompositeIsAConflict()
        {
            var conflicts = _bindings.FindConflicts(ReloadKey, "<Keyboard>/w");

            Assert.That(conflicts.Count(), Is.EqualTo(1));
            Assert.That(conflicts[0].ActionName, Is.EqualTo("Move"));
            Assert.That(conflicts[0].Part, Is.EqualTo("up"));
            Assert.That(conflicts[0].BindingIndex, Is.EqualTo(_bindings.BindingIndexOf(MoveUpKey)));
            Assert.That(conflicts[0].ToString(), Does.StartWith("Move/up["));
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
            Assert.That(_bindings.FindConflicts(ReloadKey, "<keyboard>/E").Count(), Is.EqualTo(1));
        }

        [Test]
        public void AFreeKeyAndTheSlotItselfAreNotConflicts()
        {
            Assert.That(_bindings.FindConflicts(ReloadKey, "<Keyboard>/m"), Is.Empty);
            Assert.That(_bindings.FindConflicts(ReloadKey, "<Keyboard>/r"), Is.Empty, "keeping the same key");
            Assert.That(_bindings.FindConflicts(ReloadKey, null), Is.Empty);
        }

        [Test]
        public void AnOverrideIsSeenByTheConflictCheckOfOtherSlots()
        {
            _bindings.ApplyOverride(ReloadKey, "<Keyboard>/m");

            Assert.That(_bindings.FindConflicts(new RebindSlot("Interact", InputDeviceKind.KeyboardMouse), "<Keyboard>/r"), Is.Empty, "R is free now");
            var conflicts = _bindings.FindConflicts(new RebindSlot("Interact", InputDeviceKind.KeyboardMouse), "<Keyboard>/m");
            Assert.That(conflicts.Select(c => c.ActionName), Is.EqualTo(new[] { "Reload" }));
        }

        [Test]
        public void FindConflictsOfASlotUsesItsCurrentBinding()
        {
            _bindings.ApplyOverride(ReloadKey, "<Keyboard>/e");

            Assert.That(_bindings.FindConflicts(ReloadKey).Select(c => c.ActionName), Is.EqualTo(new[] { "Interact" }));
        }

        [Test]
        public void AnActionAllowedToShareAKeyIsNotAConflict()
        {
            var sharing = new KeySharingSpec(new[] { new KeySharingRule(new[] { "Reload", "Interact" }, "Interact") });
            using (var bindings = new PlayerBindings(_asset, sharing))
            {
                Assert.That(bindings.Sharing, Is.SameAs(sharing));
                Assert.That(bindings.FindConflicts(ReloadKey, "<Keyboard>/e"), Is.Empty, "Reload may share E with Interact");
                Assert.That(
                    bindings.FindConflicts(new RebindSlot("Interact", InputDeviceKind.KeyboardMouse), "<Keyboard>/r"),
                    Is.Empty,
                    "the rule works from either side");
                Assert.That(bindings.FindConflicts(ReloadKey, "<Keyboard>/w").Select(c => c.ActionName), Is.EqualTo(new[] { "Move" }), "other actions still conflict");
                Assert.That(bindings.FindConflicts(JumpPad, "<Gamepad>/buttonWest").Select(c => c.ActionName), Is.EqualTo(new[] { "Reload" }));
            }

            Assert.Throws<ArgumentNullException>(() => new PlayerBindings(_asset, null));
        }

        [Test]
        public void TheOneArgumentConstructorReadsTheConfiguredSharing()
        {
            using (var bindings = new PlayerBindings(_asset))
            {
                Assert.That(bindings.Sharing, Is.SameAs(KitTuning.KeySharing));
                Assert.That(bindings.FindConflicts(ReloadKey, "<Keyboard>/e"), Is.Empty, "the bundled key-sharing.json lets Reload share with Interact");
            }
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

            _bindings.ApplyOverride(ReloadKey, "<Keyboard>/m");
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
