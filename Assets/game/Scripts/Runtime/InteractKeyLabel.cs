using Fps.Core;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// Interact の案内 (<see cref="IInteractPrompt"/>) に入れるキーの表記。最後に触った入力機器 (キーボードとマウスかゲームパッド) の割り当てを出す。
    /// 機器の見分けに使うアクションは初めて表記を求めたときに探し、それからは持ち続ける。
    /// </summary>
    public sealed class InteractKeyLabel
    {
        private InputAction[] _deviceActions;
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;

        /// <summary>interact を今の機器で押すキーの表記。interact が null なら空文字。</summary>
        public string For(InputAction interact)
        {
            if (interact == null)
            {
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
            return TutorialKeyLabels.For(interact, _device);
        }
    }
}
