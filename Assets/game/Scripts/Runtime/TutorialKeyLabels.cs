using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// チュートリアルの案内に出す、アクションのキー (ボタン) の表記を決める。入力機器の種類の制御スキーム
    /// (Assets/InputSystem_Actions.inputactions の Keyboard&amp;Mouse / Gamepad) に入っている最初の割り当てを、
    /// Input System の表示名 (<c>InputAction.GetBindingDisplayString</c>) で出すので、割り当てを変えていれば変えた後のキーになる。
    /// </summary>
    public static class TutorialKeyLabels
    {
        public const string KeyboardMouseGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        // 2D の合成 (WASD) を「上・左・下・右」の順に並べると、キーボードの並びどおり WASD と読める
        private static readonly string[] _directionOrder = { "up", "left", "down", "right" };

        /// <summary>device の種類の割り当てを入れている制御スキームの名前。</summary>
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
        /// action を device で操作するキーの表記。device の制御スキームの割り当てを並び順に見て、最初に見つかったものを出す。
        /// 合成 (WASD など) は部品ごとの表記を <see cref="TutorialSteps.JoinDirectionalKeys"/> でまとめる。
        /// </summary>
        /// <returns>表記。device の割り当てが無ければ空文字。</returns>
        public static string For(InputAction action, InputDeviceKind device)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var group = GroupOf(device);
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isPartOfComposite)
                {
                    continue;
                }

                var label = binding.isComposite
                    ? CompositeLabel(action, i, group)
                    : InGroup(binding, group) ? action.GetBindingDisplayString(i) : string.Empty;
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label.Trim();
                }
            }

            return string.Empty;
        }

        /// <summary>binding の groups (; 区切り) に group があるか。</summary>
        public static bool InGroup(InputBinding binding, string group)
        {
            var groups = binding.groups;
            if (string.IsNullOrEmpty(groups) || string.IsNullOrEmpty(group))
            {
                return false;
            }

            foreach (var entry in groups.Split(InputBinding.Separator))
            {
                if (string.Equals(entry.Trim(), group, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string CompositeLabel(InputAction action, int compositeIndex, string group)
        {
            var bindings = action.bindings;

            // 部品の名前ごとに、group の最初の割り当て (WASD と矢印キーの両方があれば先に書いた方) を取る
            var firstByPart = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var partOrder = new List<string>();
            for (var i = compositeIndex + 1; i < bindings.Count && bindings[i].isPartOfComposite; i++)
            {
                var part = bindings[i];
                var name = part.name ?? string.Empty;
                if (!InGroup(part, group) || firstByPart.ContainsKey(name))
                {
                    continue;
                }

                firstByPart.Add(name, i);
                partOrder.Add(name);
            }

            if (firstByPart.Count == 0)
            {
                return string.Empty;
            }

            var labels = new List<string>(firstByPart.Count);
            foreach (var direction in _directionOrder)
            {
                if (firstByPart.TryGetValue(direction, out var index))
                {
                    labels.Add(action.GetBindingDisplayString(index));
                    firstByPart.Remove(direction);
                }
            }

            // 方向でない部品 (1D の負・正など) は書いた順に後ろへ足す
            foreach (var name in partOrder)
            {
                if (firstByPart.TryGetValue(name, out var index))
                {
                    labels.Add(action.GetBindingDisplayString(index));
                }
            }

            return TutorialSteps.JoinDirectionalKeys(labels);
        }
    }
}
