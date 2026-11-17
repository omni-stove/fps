using System;

namespace Shooter.Core
{
    // 傾けられるか (カバー中・壁など) は移動の Core (LeanStep) が決めるので、ここでは傾けたい向きだけを返す。
    public sealed class LeanInput
    {
        private readonly HoldToggleState _left;
        private readonly HoldToggleState _right;

        public LeanInput(HoldMode mode = HoldMode.Hold)
        {
            _left = new HoldToggleState(mode);
            _right = new HoldToggleState(mode);
        }

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

        public bool IsLeftActive => _left.IsActive;

        public bool IsRightActive => _right.IsActive;

        public int Lean => Combine(_left.IsActive, _right.IsActive);

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

        public void Release()
        {
            _left.Release();
            _right.Release();
        }

        public static int Combine(bool left, bool right)
        {
            return left == right ? 0 : (right ? 1 : -1);
        }
    }
}
