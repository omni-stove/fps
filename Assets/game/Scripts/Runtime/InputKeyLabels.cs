using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public static class InputKeyLabels
    {
        public const string KeyboardMouseGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        // 2D の合成 (WASD) を「上・左・下・右」の順に並べると、キーボードの並びどおり WASD と読める
        private static readonly string[] _directionOrder = { "up", "left", "down", "right" };

        public static InputDeviceKind DetectDevice(InputAction[] actions, InputDeviceKind fallback)
        {
            if (actions == null)
            {
                return fallback;
            }

            foreach (var action in actions)
            {
                // スティックの小さな揺れで表記が行き来しないよう、押したと言える大きさのときだけ機器を見る
                if (action != null && action.enabled && action.IsPressed())
                {
                    return InputDeviceKinds.Of(action, fallback);
                }
            }

            return fallback;
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
