using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// LeanLeft / LeanRight の入力 (<see cref="PlayerActionReader.HeldCommands"/> と <see cref="PlayerActionReader.PressedCommands"/>) を毎フレーム読み、
    /// 移動の入力に入れる傾ける向き (<see cref="Lean"/>、左 -1・なし 0・右 1) を決める。左右とも押していれば 0。
    /// 押している間か押すたびに切り替えるかは <see cref="Mode"/> (<see cref="LeanInput"/>) で選ぶ。
    /// 傾けられるか (カバー中・壁など) は移動の Core (<see cref="LeanStep"/>) が決めるので、ここでは向きだけを出す。
    /// 移動の入力へは <see cref="ApplyTo"/> で入れる。プレイヤーに付けるときは <see cref="LeanActionInstaller"/> を使う。
    /// </summary>
    // PlayerActionReader (DefaultExecutionOrder 100) がその LateUpdate で読んだ押下を、同じフレームのうちに使う。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class LeanController : MonoBehaviour
    {
        [SerializeField, Tooltip("押している間だけ傾けるか (Hold)、押すたびに切り替えるか (Toggle)")]
        private HoldMode _mode = HoldMode.Hold;

        private LeanInput _buttons;
        private PlayerActionReader _reader;

        /// <summary>押している間だけ傾けるか、押すたびに切り替えるか。変えると、どちらにも傾けていない状態から始め直す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">定義されていないモード。</exception>
        public HoldMode Mode
        {
            get => Buttons.Mode;
            set
            {
                Buttons.Mode = value;
                _mode = value;
            }
        }

        /// <summary>
        /// ボタンを読む元。null (か破棄済み) なら、LateUpdate のたびに同じ GameObject の <see cref="PlayerActionReader"/> を探す。
        /// </summary>
        public PlayerActionReader Reader
        {
            get => _reader;
            set => _reader = value;
        }

        /// <summary>傾けたい向き。左 -1・なし 0・右 1。無効の間は 0。</summary>
        public int Lean => isActiveAndEnabled ? Buttons.Lean : 0;

        private LeanInput Buttons => _buttons ?? (_buttons = new LeanInput(_mode));

        /// <summary>移動の入力 input に、今の傾けたい向き (<see cref="Lean"/>) を入れたもの。</summary>
        public CharacterMotorInput ApplyTo(CharacterMotorInput input)
        {
            return input.WithLean(Lean);
        }

        /// <summary>
        /// 1 フレーム分のボタンを渡して傾けたい向きを決め、返す。held は押されているボタン、pressed はそのフレームに押されたボタン。
        /// LeanLeft / LeanRight 以外のボタンは見ない。
        /// </summary>
        public int Tick(PlayerCommands held, PlayerCommands pressed)
        {
            return Buttons.Update(
                (held & PlayerCommands.LeanLeft) != 0,
                (pressed & PlayerCommands.LeanLeft) != 0,
                (held & PlayerCommands.LeanRight) != 0,
                (pressed & PlayerCommands.LeanRight) != 0);
        }

        /// <summary>左右とも解除する (死亡・リスポーンなど)。Hold でも、ボタンを押し直すまで傾けない。</summary>
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
