using Fps.Core;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 入力機器を <see cref="InputDeviceKind"/> に分ける。どの <see cref="ControlSettings"/> の値を使うかを決めるのに使う。
    /// </summary>
    public static class InputDeviceKinds
    {
        /// <summary>
        /// action を今動かしている機器 (<see cref="InputAction.activeControl"/> の device) の種類。
        /// 何も動かしていないときは fallback を返すので、呼ぶ側は前回の結果を渡して直前の機器を使い続ける。
        /// </summary>
        public static InputDeviceKind Of(InputAction action, InputDeviceKind fallback)
        {
            return action == null ? fallback : Of(action.activeControl?.device, fallback);
        }

        /// <summary>
        /// device の種類。キーボードとポインター (マウス・ペン・タッチ) はキーボード／マウス、それ以外 (ゲームパッド・ジョイスティックなど) はゲームパッド。
        /// device が null なら fallback。
        /// </summary>
        public static InputDeviceKind Of(InputDevice device, InputDeviceKind fallback)
        {
            if (device == null)
            {
                return fallback;
            }

            return device is Keyboard || device is Pointer ? InputDeviceKind.KeyboardMouse : InputDeviceKind.Gamepad;
        }
    }
}
