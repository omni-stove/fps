using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed class PlayerBindings : IDisposable
    {
        public const string LogTag = "[Input]";

        public const string MapName = "Player";

        public const string KeyboardMouseGroup = "Keyboard&Mouse";

        public const string GamepadGroup = "Gamepad";

        public const string KeyboardCancelPath = "<Keyboard>/escape";

        public const string GamepadCancelPath = "<Gamepad>/start";

        // 指を離すときに隣のキーへ触れたなど、ほぼ同時に来たほかの候補を比べるための待ち。
        private const float MatchWaitSeconds = 0.1f;

        private static readonly string[] _keyboardMousePaths = { "<Keyboard>", "<Mouse>" };

        // 動かしただけで反応する軸と、どのキーでも押される合成のボタンは割り当てにしない。
        private static readonly string[] _keyboardMouseExcluded =
        {
            "<Keyboard>/anyKey", "<Mouse>/position", "<Mouse>/delta", "<Mouse>/press", "<Pointer>/position", "<Pointer>/delta", "<Pointer>/press",
        };

        private static readonly string[] _gamepadPaths = { "<Gamepad>" };

        // スティックの方向は少し倒れているだけでも拾い、メニューを動かすつもりの入力が割り当てになりやすい。
        private static readonly string[] _gamepadExcluded = { "<Gamepad>/leftStick", "<Gamepad>/rightStick" };

        // ゲームパッドに空いているボタンが無く割り当てていないので、キーボードの行だけを出すアクション。_slots より先に初期化する。
        private static readonly string[] _keyboardOnlyActions = { "Melee", "Prone", "Ability1", "Ability2", "SquadCommand", "NetStats", "SwitchAmmoType", "CycleMagnification", "HoldBreath", "SwitchGrenade", "SwitchStreak", "Inventory", "Previous", "LeanLeft", "LeanRight", "Chat", "TeamChat", "PushToTalk", "Emote", "Countermeasure" };

        private static readonly RebindSlot[] _slots = BuildSlots();

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static int _capturing;
        private static int _lastCaptureEndFrame = int.MinValue;

        private readonly InputActionAsset _asset;
        private readonly InputActionMap _map;

        private InputActionRebindingExtensions.RebindingOperation _operation;
        private RebindSlot _rebindingSlot;
        private Action<RebindResult> _finished;
        private string _previousOverridePath;
        private string _previousPath;
        private bool _reenableAfterRebind;

        public PlayerBindings(InputActionAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var map = asset.FindActionMap(MapName);
            if (map == null)
            {
                throw new ArgumentException($"Input Actions '{asset.name}' has no '{MapName}' action map", nameof(asset));
            }

            _asset = asset;
            _map = map;
        }

        public static IReadOnlyList<RebindSlot> Slots => _slots;

        // 待ち終えた直後 (同じかその次のフレーム) も true にするのは、取り消しに押した Esc / Start でポーズ画面や設定画面まで閉じないようにするため。
        public static bool IsCapturingInput =>
            _capturing > 0
            || (Application.isPlaying && _lastCaptureEndFrame != int.MinValue && Time.frameCount - _lastCaptureEndFrame <= 1);

        public InputActionAsset Asset => _asset;

        public InputActionMap Map => _map;

        public bool IsRebinding => _operation != null;

        public RebindSlot? RebindingSlot => _operation != null ? _rebindingSlot : (RebindSlot?)null;

        public bool HasOverrides
        {
            get
            {
                foreach (var binding in _map.bindings)
                {
                    if (binding.overridePath != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static string GroupOf(InputDeviceKind device)
        {
            switch (device)
            {
                case InputDeviceKind.KeyboardMouse:
                    return KeyboardMouseGroup;
                case InputDeviceKind.Gamepad:
                    return GamepadGroup;
                default:
                    throw new ArgumentOutOfRangeException(nameof(device), device, "unknown input device kind");
            }
        }

        public int BindingIndexOf(RebindSlot slot)
        {
            var action = _map.FindAction(slot.ActionName);
            return action != null ? BindingIndexOf(action, slot) : -1;
        }

        public bool Has(RebindSlot slot)
        {
            return BindingIndexOf(slot) >= 0;
        }

        public string PathOf(RebindSlot slot)
        {
            var action = _map.FindAction(slot.ActionName);
            var index = action != null ? BindingIndexOf(action, slot) : -1;
            return index >= 0 ? action.bindings[index].effectivePath : null;
        }

        public bool IsOverridden(RebindSlot slot)
        {
            return OverridePathOf(slot) != null;
        }

        public string OverridePathOf(RebindSlot slot)
        {
            var action = _map.FindAction(slot.ActionName);
            var index = action != null ? BindingIndexOf(action, slot) : -1;
            return index >= 0 ? action.bindings[index].overridePath : null;
        }

        public string DisplayOf(RebindSlot slot)
        {
            var action = _map.FindAction(slot.ActionName);
            var index = action != null ? BindingIndexOf(action, slot) : -1;
            if (index < 0)
            {
                return string.Empty;
            }

            var display = action.GetBindingDisplayString(index);
            return string.IsNullOrEmpty(display) ? action.bindings[index].effectivePath ?? string.Empty : display;
        }

        public IReadOnlyList<BindingConflict> FindConflicts(RebindSlot slot)
        {
            return FindConflicts(slot, PathOf(slot));
        }

        public IReadOnlyList<BindingConflict> FindConflicts(RebindSlot slot, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Array.Empty<BindingConflict>();
            }

            var slotAction = _map.FindAction(slot.ActionName);
            var slotIndex = slotAction != null ? BindingIndexOf(slotAction, slot) : -1;
            var conflicts = new List<BindingConflict>();
            foreach (var action in _map.actions)
            {
                var bindings = action.bindings;
                for (var i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];
                    if (binding.isComposite || (action == slotAction && i == slotIndex))
                    {
                        continue;
                    }

                    if (!string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var part = binding.isPartOfComposite ? binding.name : null;
                    conflicts.Add(new BindingConflict(action.name, part, i));
                }
            }

            return conflicts;
        }

        public void ApplyOverride(RebindSlot slot, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path must not be empty", nameof(path));
            }

            var action = RequireAction(slot, out var index);
            action.ApplyBindingOverride(index, path);
        }

        public void RestoreOverride(RebindSlot slot, string overridePath)
        {
            var action = RequireAction(slot, out var index);
            if (overridePath == null)
            {
                action.RemoveBindingOverride(index);
            }
            else
            {
                action.ApplyBindingOverride(index, overridePath);
            }
        }

        public void ResetAll()
        {
            CancelRebind();
            _map.RemoveAllBindingOverrides();
        }

        // 有効なアクションは割り当てを変えられないので、待つ間だけ無効にし、終わったら有効に戻す。
        public bool StartRebind(RebindSlot slot, Action<RebindResult> finished)
        {
            if (finished == null)
            {
                throw new ArgumentNullException(nameof(finished));
            }

            if (_operation != null)
            {
                return false;
            }

            var action = RequireAction(slot, out var index);
            var binding = action.bindings[index];
            _rebindingSlot = slot;
            _finished = finished;
            _previousOverridePath = binding.overridePath;
            _previousPath = binding.effectivePath;
            _reenableAfterRebind = action.enabled;
            if (_reenableAfterRebind)
            {
                action.Disable();
            }

            var cancelPath = slot.Device == InputDeviceKind.Gamepad ? GamepadCancelPath : KeyboardCancelPath;
            var operation = action.PerformInteractiveRebinding(index)
                .WithCancelingThrough(cancelPath)
                .WithMatchingEventsBeingSuppressed()
                .OnMatchWaitForAnother(MatchWaitSeconds);

            foreach (var path in slot.Device == InputDeviceKind.Gamepad ? _gamepadPaths : _keyboardMousePaths)
            {
                operation.WithControlsHavingToMatchPath(path);
            }

            foreach (var path in slot.Device == InputDeviceKind.Gamepad ? _gamepadExcluded : _keyboardMouseExcluded)
            {
                operation.WithControlsExcluding(path);
            }

            operation.OnComplete(op => Finish(op, action, index, true));
            operation.OnCancel(op => Finish(op, action, index, false));

            _operation = operation;
            _capturing++;
            try
            {
                operation.Start();
            }
            catch
            {
                _operation = null;
                _finished = null;
                _capturing = Math.Max(0, _capturing - 1);
                operation.Dispose();
                if (_reenableAfterRebind && !action.enabled)
                {
                    action.Enable();
                }

                throw;
            }

            Debug.Log($"{LogTag} waiting for a new input for {slot} (now {_previousPath}, cancel with {cancelPath})");
            return true;
        }

        public void CancelRebind()
        {
            _operation?.Cancel();
        }

        public void Dispose()
        {
            CancelRebind();
        }

        private void Finish(InputActionRebindingExtensions.RebindingOperation operation, InputAction action, int index, bool completed)
        {
            if (!ReferenceEquals(operation, _operation))
            {
                return;
            }

            var slot = _rebindingSlot;
            var finished = _finished;
            _operation = null;
            _finished = null;
            _capturing = Math.Max(0, _capturing - 1);
            _lastCaptureEndFrame = Time.frameCount;

            if (_reenableAfterRebind && !action.enabled)
            {
                action.Enable();
            }

            var path = action.bindings[index].effectivePath;
            var conflicts = completed ? FindConflicts(slot, path) : Array.Empty<BindingConflict>();
            var result = new RebindResult(slot, completed, _previousOverridePath, _previousPath, path, conflicts);
            operation.Dispose();

            if (!completed)
            {
                Debug.Log($"{LogTag} kept {slot} on {path} (cancelled)");
            }
            else if (conflicts.Count == 0)
            {
                Debug.Log($"{LogTag} rebound {slot} {_previousPath} -> {path}");
            }
            else
            {
                Debug.LogWarning($"{LogTag} rebound {slot} {_previousPath} -> {path}, which is also used by {string.Join(", ", conflicts)}");
            }

            try
            {
                finished?.Invoke(result);
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} a rebind handler threw: {e.Message}");
                Debug.LogException(e);
            }
        }

        private InputAction RequireAction(RebindSlot slot, out int index)
        {
            var action = _map.FindAction(slot.ActionName);
            index = action != null ? BindingIndexOf(action, slot) : -1;
            if (index < 0)
            {
                throw new ArgumentException($"Input Actions '{_asset.name}' has no binding for {slot}", nameof(slot));
            }

            return action;
        }

        private static int BindingIndexOf(InputAction action, RebindSlot slot)
        {
            var mask = InputBinding.MaskByGroup(GroupOf(slot.Device));
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isComposite || !mask.Matches(binding))
                {
                    continue;
                }

                if (slot.Part == null)
                {
                    if (!binding.isPartOfComposite)
                    {
                        return i;
                    }
                }
                else if (binding.isPartOfComposite && string.Equals(binding.name, slot.Part, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static RebindSlot[] BuildSlots()
        {
            var slots = new List<RebindSlot>();
            foreach (var part in new[] { "up", "down", "left", "right" })
            {
                slots.Add(new RebindSlot("Move", InputDeviceKind.KeyboardMouse, part));
            }

            var buttons = new[]
            {
                "Jump", "Sprint", "Crouch", "Prone", "Attack", "Aim", "Reload", "SwitchAmmoType", "CycleMagnification", "HoldBreath", "Melee", "Interact", "Next", "Previous", "Cover", "SwapShoulder",
                "Dodge", "Grenade", "SwitchGrenade", "Ability1", "Ability2", "Ping", "SquadCommand", "Scoreboard", "NetStats", "Inventory", "UseStreak", "SwitchStreak",
                "LeanLeft", "LeanRight", "Chat", "TeamChat", "PushToTalk", "Emote", "Countermeasure",
            };

            foreach (var action in buttons)
            {
                slots.Add(new RebindSlot(action, InputDeviceKind.KeyboardMouse));
                if (Array.IndexOf(_keyboardOnlyActions, action) < 0)
                {
                    slots.Add(new RebindSlot(action, InputDeviceKind.Gamepad));
                }
            }

            return slots.ToArray();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _capturing = 0;
            _lastCaptureEndFrame = int.MinValue;
        }
    }
}
