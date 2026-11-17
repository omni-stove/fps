using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 傾けられるか (カバー中・壁など) は移動の Core (LeanStep) が決めるので、ここでは向きだけを出す。
    // PlayerActionReader (DefaultExecutionOrder 100) がその LateUpdate で読んだ押下を、同じフレームのうちに使う。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class LeanController : MonoBehaviour
    {
        [SerializeField, Tooltip("押している間だけ傾けるか (Hold)、押すたびに切り替えるか (Toggle)")]
        private HoldMode _mode = HoldMode.Hold;

        private LeanInput _buttons;
        private PlayerActionReader _reader;

        public HoldMode Mode
        {
            get => Buttons.Mode;
            set
            {
                Buttons.Mode = value;
                _mode = value;
            }
        }

        public PlayerActionReader Reader
        {
            get => _reader;
            set => _reader = value;
        }

        public int Lean => isActiveAndEnabled ? Buttons.Lean : 0;

        private LeanInput Buttons => _buttons ?? (_buttons = new LeanInput(_mode));

        public CharacterMotorInput ApplyTo(CharacterMotorInput input)
        {
            return input.WithLean(Lean);
        }

        public int Tick(PlayerCommands held, PlayerCommands pressed)
        {
            return Buttons.Update(
                (held & PlayerCommands.LeanLeft) != 0,
                (pressed & PlayerCommands.LeanLeft) != 0,
                (held & PlayerCommands.LeanRight) != 0,
                (pressed & PlayerCommands.LeanRight) != 0);
        }

        public void Release()
        {
            Buttons.Release();
        }

        private void OnDisable()
        {
            // 無効の間は読まないので、Toggle で傾けたまま戻ってこないよう解除しておく。
            Release();
        }

        private void LateUpdate()
        {
            if (_reader == null)
            {
                _reader = GetComponent<PlayerActionReader>();
            }

            if (_reader == null || !_reader.isActiveAndEnabled)
            {
                Release();
                return;
            }

            // ポーズ画面のボタンを押したつもりで傾かないよう、開いている間は押していないことにする。
            if (GameUi.IsPauseOpen)
            {
                Tick(PlayerCommands.None, PlayerCommands.None);
                return;
            }

            Tick(_reader.HeldCommands, _reader.PressedCommands);
        }
    }
}
