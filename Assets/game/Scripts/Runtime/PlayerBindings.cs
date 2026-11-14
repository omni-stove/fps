using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// Input Actions の Player マップの割り当てを、キーボード/マウスとゲームパッドで別々に変える。
    /// 変えられる所は <see cref="Slots"/>。新しい入力は <see cref="StartRebind"/> (Input System の PerformInteractiveRebinding) で受け取り、
    /// 割り当ての上書き (binding override) として入れる。元のアセットは書き換えない。
    /// ほかの割り当てとの重なりは <see cref="FindConflicts(RebindSlot)"/> で調べ、取り消すときは <see cref="RestoreOverride"/> で前の上書きに戻す。
    /// 保存と読み込みは <see cref="InputBindingOverridesStore"/> がする。
    /// </summary>
    public sealed class PlayerBindings : IDisposable
    {
        public const string LogTag = "[Input]";

        /// <summary>割り当てを変えるアクションマップ。</summary>
        public const string MapName = "Player";

        /// <summary>Assets/InputSystem_Actions.inputactions の入力グループ (control scheme)。</summary>
        public const string KeyboardMouseGroup = "Keyboard&Mouse";

        public const string GamepadGroup = "Gamepad";

        /// <summary>キーボード/マウスの割り当てを待っている間に押すと取り消す入力。</summary>
        public const string KeyboardCancelPath = "<Keyboard>/escape";

        /// <summary>ゲームパッドの割り当てを待っている間に押すと取り消す入力。</summary>
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

        /// <exception cref="ArgumentNullException">asset が null。</exception>
        /// <exception cref="ArgumentException">asset に <see cref="MapName"/> のマップが無い。</exception>
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

        /// <summary>
        /// 設定画面で変えられる所。並びは画面の行の順で、同じ行のキーボード/マウスとゲームパッドが続く。
        /// ゲームパッドの移動 (左スティック) と視点、メニューを開くための Pause は変えられない。
        /// </summary>
        public static IReadOnlyList<RebindSlot> Slots => _slots;

        /// <summary>
        /// どこかで割り当ての入力を待っているか、待ち終えた直後 (同じかその次のフレーム) か。
        /// 取り消しに押した Esc / Start でポーズ画面や設定画面まで閉じないよう、それらが見る。
        /// </summary>
        public static bool IsCapturingInput =>
            _capturing > 0
            || (Application.isPlaying && _lastCaptureEndFrame != int.MinValue && Time.frameCount - _lastCaptureEndFrame <= 1);

        public InputActionAsset Asset => _asset;

        public InputActionMap Map => _map;

        /// <summary>入力を待っているか。</summary>
        public bool IsRebinding => _operation != null;

        /// <summary>入力を待っている所。待っていなければ null。</summary>
        public RebindSlot? RebindingSlot => _operation != null ? _rebindingSlot : (RebindSlot?)null;

        /// <summary>Player マップのどれかの割り当てに上書きがあるか。</summary>
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

        /// <summary>device の入力グループの名前。</summary>
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

        /// <summary>
        /// 今の言語 (<see cref="Localizer"/>) で、アクション (と合成の部品) の画面に出す名前。
        /// 文字列表のキーは <c>input.action.{アクション名}</c> と、移動の部品の <c>input.move.{up|down|left|right}</c>。
        /// 表に無い名前は、Input Actions に足したばかりのアクションでも行が出るよう、警告を出さずにアクション名をそのまま使う。
        /// </summary>
        public static string LabelOf(string actionName, string part = null)
        {
            if (part != null
                && string.Equals(actionName, "Move", StringComparison.Ordinal)
                && Localizer.TryGet("input.move." + part.ToLowerInvariant(), out var direction))
            {
                return direction;
            }

            var label = actionName != null && Localizer.TryGet("input.action." + actionName, out var text) ? text : actionName;
            return part == null ? label : label + " (" + part + ")";
        }

        /// <summary>
        /// slot が指す割り当ての番号 (アクションの bindings の中の位置)。部品があれば、その入力グループに入った合成の中の最初のその名前の部品。
        /// 無ければ合成でない割り当てのうち、その入力グループに入った最初のもの。見つからなければ -1。
        /// </summary>
        public int BindingIndexOf(RebindSlot slot)
        {
            var action = _map.FindAction(slot.ActionName);
            return action != null ? BindingIndexOf(action, slot) : -1;
        }

        /// <summary>slot が指す割り当てがこのアセットにあるか。</summary>
        public bool Has(RebindSlot slot)
        {
            return BindingIndexOf(slot) >= 0;
        }

        /// <summary>slot に今効いている入力のパス (上書きがあればそれ)。無ければ null。</summary>
        public string PathOf(RebindSlot slot)
        {
            var action = _map.FindAction(slot.ActionName);
            var index = action != null ? BindingIndexOf(action, slot) : -1;
            return index >= 0 ? action.bindings[index].effectivePath : null;
        }

        /// <summary>slot に上書きがあるか。</summary>
        public bool IsOverridden(RebindSlot slot)
        {
            return OverridePathOf(slot) != null;
        }

        /// <summary>slot の上書き。無ければ (か slot が無ければ) null。</summary>
        public string OverridePathOf(RebindSlot slot)
        {
            var action = _map.FindAction(slot.ActionName);
            var index = action != null ? BindingIndexOf(action, slot) : -1;
            return index >= 0 ? action.bindings[index].overridePath : null;
        }

        /// <summary>slot に今効いている入力の、画面に出す名前 (例: "R")。無ければ空文字。</summary>
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

        /// <summary>slot に今効いている入力と同じ入力を使う、Player マップのほかの割り当て。</summary>
        public IReadOnlyList<BindingConflict> FindConflicts(RebindSlot slot)
        {
            return FindConflicts(slot, PathOf(slot));
        }

        /// <summary>
        /// slot を path にしたときに、同じ入力を使う Player マップのほかの割り当て (同じアクションのほかの割り当ても含む)。
        /// 比べるのは効いている入力のパスで、大文字と小文字は区別しない。
        /// </summary>
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
                    conflicts.Add(new BindingConflict(action.name, part, i, LabelOf(action.name, part)));
                }
            }

            return conflicts;
        }

        /// <summary>slot を path に上書きする。</summary>
        /// <exception cref="ArgumentException">path が空か、slot がこのアセットに無い。</exception>
        public void ApplyOverride(RebindSlot slot, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path must not be empty", nameof(path));
            }

            var action = RequireAction(slot, out var index);
            action.ApplyBindingOverride(index, path);
        }

        /// <summary>slot の上書きを overridePath に戻す。null なら上書きを外して既定の入力に戻す。</summary>
        /// <exception cref="ArgumentException">slot がこのアセットに無い。</exception>
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

        /// <summary>入力を待っていればやめ、Player マップの上書きをすべて外して既定の入力に戻す。</summary>
        public void ResetAll()
        {
            CancelRebind();
            _map.RemoveAllBindingOverrides();
        }

        /// <summary>
        /// slot の新しい入力を待ち始める。次に押されたその機器のボタンかキーで上書きし、finished を呼ぶ。
        /// 取り消し (<see cref="KeyboardCancelPath"/> / <see cref="GamepadCancelPath"/> / <see cref="CancelRebind"/>) でも finished を呼ぶ。
        /// アクションが有効なら待つ間だけ無効にし、終わったら有効に戻す (有効なアクションは割り当てを変えられないため)。
        /// 既に待っていれば何もせず false。
        /// </summary>
        /// <exception cref="ArgumentNullException">finished が null。</exception>
        /// <exception cref="ArgumentException">slot がこのアセットに無い。</exception>
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

        /// <summary>入力を待っていればやめる (割り当ては変えない)。finished は取り消しとして呼ぶ。</summary>
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
                "Jump", "Sprint", "Crouch", "Attack", "Aim", "Reload", "Interact", "Next", "Previous", "Cover", "SwapShoulder", "Dodge", "Grenade",
                "Scoreboard",
            };
            foreach (var action in buttons)
            {
                slots.Add(new RebindSlot(action, InputDeviceKind.KeyboardMouse));
                slots.Add(new RebindSlot(action, InputDeviceKind.Gamepad));
            }

            // 回線の表示はゲームパッドに割り当てない (空いているボタンが無い) ので、キーボードの行だけを出す
            slots.Add(new RebindSlot("NetStats", InputDeviceKind.KeyboardMouse));

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
