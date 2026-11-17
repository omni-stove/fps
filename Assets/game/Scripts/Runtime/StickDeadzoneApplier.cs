using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class StickDeadzoneApplier : MonoBehaviour
    {
        public const string LeftStickPath = "<Gamepad>/leftStick";
        public const string RightStickPath = "<Gamepad>/rightStick";
        public const string ProcessorName = "stickDeadzone";

        // stickDeadzone は min=0 を「Input System の既定 (0.125) を使う」と読むので、設定の 0 はこれで表す。
        // 設定の刻み (0.001) より小さく、倒していないスティックの揺れも通す。
        private const float ZeroDeadzone = 0.0001f;

        [SerializeField]
        [Tooltip("デッドゾーンを付ける Input Actions。空ならプロジェクト全体のアクション。")]
        private InputActionAsset _actions;

        private InputActionAsset _applied;
        private bool _listening;
        private bool _needsRefresh;

        public InputActionAsset Actions
        {
            get => _actions;
            set
            {
                if (_actions == value)
                {
                    return;
                }

                _actions = value;
                if (_listening)
                {
                    Detach();
                    Refresh(AppliedControlSettings.Current);
                }
            }
        }

        public InputActionAsset AppliedActions => _applied;

        // 先頭に置くのは、スケールなどのあとに切ると、切る量が設定と変わるため。既にある stickDeadzone を外すのは、2 つ重ねると内側の範囲で 2 回伸ばしてしまうため。
        public static string ProcessorsFor(string baseProcessors, float deadzone)
        {
            if (float.IsNaN(deadzone) || deadzone < 0f || deadzone >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(deadzone), deadzone, "deadzone must be in [0, 1)");
            }

            var min = deadzone > 0f ? deadzone : ZeroDeadzone;
            var builder = new StringBuilder();
            builder.Append(ProcessorName).Append("(min=").Append(min.ToString("0.####", CultureInfo.InvariantCulture)).Append(')');
            foreach (var processor in SplitProcessors(baseProcessors))
            {
                if (IsDeadzoneProcessor(processor))
                {
                    continue;
                }

                builder.Append(',').Append(processor);
            }

            return builder.ToString();
        }

        public static bool IsStickBinding(InputBinding binding)
        {
            if (binding.isComposite)
            {
                return false;
            }

            var path = binding.effectivePath;
            return string.Equals(path, LeftStickPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, RightStickPath, StringComparison.OrdinalIgnoreCase);
        }

        public static int Apply(InputActionAsset actions, float deadzone)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            // 範囲の外を、どの割り当ても触る前に弾く。
            ProcessorsFor(null, deadzone);

            var changed = 0;
            foreach (var map in actions.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    var bindings = action.bindings;
                    for (var i = 0; i < bindings.Count; i++)
                    {
                        var binding = bindings[i];
                        if (!IsStickBinding(binding))
                        {
                            continue;
                        }

                        var processors = ProcessorsFor(binding.processors, deadzone);
                        if (string.Equals(binding.overrideProcessors, processors, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        action.ApplyBindingOverride(i, OverrideOf(binding, processors));
                        changed++;
                    }
                }
            }

            return changed;
        }

        public static int Remove(InputActionAsset actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            var removed = 0;
            foreach (var map in actions.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    var bindings = action.bindings;
                    for (var i = 0; i < bindings.Count; i++)
                    {
                        var binding = bindings[i];
                        if (!IsStickBinding(binding) || binding.overrideProcessors == null)
                        {
                            continue;
                        }

                        if (binding.overridePath == null && binding.overrideInteractions == null)
                        {
                            action.RemoveBindingOverride(i);
                        }
                        else
                        {
                            action.ApplyBindingOverride(i, OverrideOf(binding, null));
                        }

                        removed++;
                    }
                }
            }

            return removed;
        }

        public void Refresh(ControlSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _needsRefresh = false;
            var actions = _actions != null ? _actions : InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning(StickDeadzoneLog.NoActions(), this);
                return;
            }

            if (_applied != null && _applied != actions)
            {
                Detach();
            }

            _applied = actions;
            var changed = Apply(actions, settings.StickDeadzone);
            if (changed > 0)
            {
                Debug.Log(StickDeadzoneLog.Applied(actions.name, settings.StickDeadzone, changed), this);
            }
        }

        private void OnEnable()
        {
            // 専用サーバーにはスティックを倒す人がいない。
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            AppliedControlSettings.Changed += HandleSettingsChanged;
            InputSystem.onActionChange += HandleActionChange;
            _listening = true;
            Refresh(AppliedControlSettings.Current);
        }

        private void OnDisable()
        {
            if (!_listening)
            {
                return;
            }

            AppliedControlSettings.Changed -= HandleSettingsChanged;
            InputSystem.onActionChange -= HandleActionChange;
            _listening = false;
            _needsRefresh = false;
            Detach();
        }

        private void Update()
        {
            if (_needsRefresh)
            {
                Refresh(AppliedControlSettings.Current);
            }
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            Refresh(settings);
        }

        // 上書きの読み直し (InputBindingOverridesStore.Load) や初期値に戻す操作 (PlayerBindings.ResetAll) は上書きをまるごと外すので、
        // 解決し直しを見て付け直す。解決の知らせの中で上書きを変えると解決を入れ子にしてしまうため、次の Update まで待つ。
        private void HandleActionChange(object source, InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged || _applied == null || AssetOf(source) != _applied)
            {
                return;
            }

            _needsRefresh = true;
        }

        private void Detach()
        {
            if (_applied == null)
            {
                return;
            }

            var actions = _applied;
            _applied = null;
            var removed = Remove(actions);
            if (removed > 0)
            {
                Debug.Log(StickDeadzoneLog.Removed(actions.name, removed), this);
            }
        }

        private static InputActionAsset AssetOf(object source)
        {
            switch (source)
            {
                case InputActionAsset asset:
                    return asset;
                case InputActionMap map:
                    return map.asset;
                case InputAction action:
                    return action.actionMap != null ? action.actionMap.asset : null;
                default:
                    return null;
            }
        }

        // ApplyBindingOverride は入力のパス・interaction・processor をまとめて置き換えるので、残す 2 つを今の上書きから写す。
        private static InputBinding OverrideOf(InputBinding binding, string processors)
        {
            return new InputBinding
            {
                overridePath = binding.overridePath,
                overrideInteractions = binding.overrideInteractions,
                overrideProcessors = processors,
            };
        }

        private static bool IsDeadzoneProcessor(string processor)
        {
            var open = processor.IndexOf('(');
            var name = (open >= 0 ? processor.Substring(0, open) : processor).Trim();
            return string.Equals(name, ProcessorName, StringComparison.OrdinalIgnoreCase);
        }

        // processor の引数にもカンマが入る (例: stickDeadzone(min=0.1,max=0.9)) ので、括弧の外のカンマだけで分ける。
        private static List<string> SplitProcessors(string processors)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(processors))
            {
                return result;
            }

            var depth = 0;
            var start = 0;
            for (var i = 0; i <= processors.Length; i++)
            {
                if (i < processors.Length)
                {
                    var c = processors[i];
                    if (c == '(')
                    {
                        depth++;
                        continue;
                    }

                    if (c == ')')
                    {
                        depth = Math.Max(0, depth - 1);
                        continue;
                    }

                    if (c != ',' || depth > 0)
                    {
                        continue;
                    }
                }

                var part = processors.Substring(start, i - start).Trim();
                if (part.Length > 0)
                {
                    result.Add(part);
                }

                start = i + 1;
            }

            return result;
        }
    }
}
