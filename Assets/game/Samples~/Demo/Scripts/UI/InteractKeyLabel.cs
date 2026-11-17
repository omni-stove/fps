using System.Text;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed class InteractKeyLabel
    {
        public const string LogTag = "[InteractKey]";

        private InputAction[] _deviceActions;
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;
        private string _lastLabel;
        private InputDeviceKind _lastDevice;

        public string For(InputAction interact)
        {
            if (interact == null)
            {
                Report(string.Empty, null);
                return string.Empty;
            }

            if (_deviceActions == null)
            {
                var actions = InputSystem.actions;
                _deviceActions = new[]
                {
                    actions?.FindAction(InputActionPaths.Move),
                    actions?.FindAction(InputActionPaths.Look),
                    interact,
                };
            }

            _device = TutorialPresenter.DetectDevice(_deviceActions, _device);
            var label = TutorialKeyLabels.For(interact, _device);
            Report(label, interact);
            return label;
        }

        private void Report(string label, InputAction interact)
        {
            if (_lastLabel == label && _lastDevice == _device)
            {
                return;
            }

            _lastLabel = label;
            _lastDevice = _device;
            Debug.Log($"{LogTag} label='{label}' device={_device} {Describe(interact, _deviceActions)}");
        }

        public static string Describe(InputAction interact, InputAction[] deviceActions)
        {
            var text = new StringBuilder();
            if (interact == null)
            {
                text.Append("action=null");
            }
            else
            {
                text.Append("action=").Append(interact.name).Append(" enabled=").Append(interact.enabled).Append(" bindings=[");
                var bindings = interact.bindings;
                for (var i = 0; i < bindings.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(", ");
                    }

                    text.Append(bindings[i].effectivePath).Append(" groups='").Append(bindings[i].groups)
                        .Append("' display='").Append(interact.GetBindingDisplayString(i)).Append('\'');
                }

                text.Append(']');
            }

            if (deviceActions != null)
            {
                foreach (var action in deviceActions)
                {
                    if (action != null)
                    {
                        text.Append(' ').Append(action.name).Append("Device=").Append(action.activeControl?.device?.name ?? "none");
                    }
                }
            }

            return text.ToString();
        }
    }
}
