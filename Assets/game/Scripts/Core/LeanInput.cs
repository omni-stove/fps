using System;

namespace Shooter.Core
{
    /// <summary>
    /// 左右のリーンのボタンから、移動の入力の傾ける向き (<see cref="CharacterMotorInput.Lean"/>、左 -1・なし 0・右 1) を決める。
    /// 左右それぞれに <see cref="HoldToggleState"/> を持ち、押している間 (<see cref="HoldMode.Hold"/>) か押すたびに切り替え
    /// (<see cref="HoldMode.Toggle"/>) かは <see cref="Mode"/> で左右まとめて選ぶ。左右とも有効なら 0。
    /// Toggle では片方を有効にしたとき、もう片方は (同じ回に押されていなければ) 解除し、反対へ傾け直せるようにする。
    /// 返すのは傾けたい向きで、傾けられるか (カバー中・壁など) は移動の Core (<see cref="LeanStep"/>) が決める。
    /// </summary>
    public sealed class LeanInput
    {
        private readonly HoldToggleState _left;
        private readonly HoldToggleState _right;

        /// <param name="mode">初めのモード。どちらにも傾けていない状態から始める。</param>
        public LeanInput(HoldMode mode = HoldMode.Hold)
        {
            _left = new HoldToggleState(mode);
            _right = new HoldToggleState(mode);
        }

        /// <summary>今のモード。違うモードを入れると、どちらにも傾けていない状態から始め直す。</summary>
        public HoldMode Mode
        {
            get => _left.Mode;
            set
            {
                // HoldToggleState が不正なモードを弾くが、左だけ変わって右で投げるのを避けるため先に確かめる。
                if (value != HoldMode.Hold && value != HoldMode.Toggle)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "unknown hold mode");
                }

                _left.Mode = value;
                _right.Mode = value;
            }
        }

        /// <summary>左のボタンが有効か。</summary>
        public bool IsLeftActive => _left.IsActive;

        /// <summary>右のボタンが有効か。</summary>
        public bool IsRightActive => _right.IsActive;

        /// <summary>傾けたい向き。左 -1・なし 0・右 1。左右とも有効なら 0。</summary>
        public int Lean => Combine(_left.IsActive, _right.IsActive);

        /// <summary>
        /// 1 回分 (オフラインは 1 フレーム、ネット対戦は 1 tick) のボタンの状態を渡し、傾けたい向きを返す。
        /// </summary>
        /// <param name="leftPressed">左のボタンが押されているか。</param>
        /// <param name="leftWasPressed">前回の Update から左のボタンが押されたか。</param>
        /// <param name="rightPressed">右のボタンが押されているか。</param>
        /// <param name="rightWasPressed">前回の Update から右のボタンが押されたか。</param>
        public int Update(bool leftPressed, bool leftWasPressed, bool rightPressed, bool rightWasPressed)
        {
            var wasLeft = _left.IsActive;
            var wasRight = _right.IsActive;
            _left.Update(leftPressed, leftWasPressed);
            _right.Update(rightPressed, rightWasPressed);

            if (_left.Mode == HoldMode.Toggle)
            {
                // Hold は離せば戻るので、両方押していれば 0 のままでよい。Toggle は離しても残るので、反対を押したら乗り換える。
                if (_right.IsActive && !wasRight && wasLeft && !leftWasPressed)
                {
                    _left.Release();
                }
                else if (_left.IsActive && !wasLeft && wasRight && !rightWasPressed)
                {
                    _right.Release();
                }
            }

            return Lean;
        }

        /// <summary>
        /// 外の理由 (死亡・リスポーン・入力を読めなくなったときなど) で左右とも解除する。
        /// Hold でも、ボタンを押し直すまで傾けない。
        /// </summary>
        public void Release()
        {
            _left.Release();
            _right.Release();
        }

        /// <summary>左右のボタンが有効かから傾けたい向き。左だけなら -1、右だけなら 1、どちらも同じなら 0。</summary>
        public static int Combine(bool left, bool right)
        {
            return left == right ? 0 : (right ? 1 : -1);
        }
    }
}
