using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Fps.Runtime
{
    public static class HeldInput
    {
        // macOS では Shift を押すとキーボードの構成変更が届き、Input System がアセット全体の割り当てを解決し直して
        // 押されていたアクションを取り消す。押しっぱなしのボタンは押し直すまで IsPressed が false のままになるので、
        // 割り当て先のボタン自体が押されているかも見る (割り当ての変更には action.controls がそのまま従う)。
        public static bool IsHeld(InputAction action)
        {
            if (action == null || !action.enabled)
            {
                return false;
            }

            if (action.IsPressed())
            {
                return true;
            }

            foreach (var control in action.controls)
            {
                if (control is ButtonControl button && button.isPressed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
