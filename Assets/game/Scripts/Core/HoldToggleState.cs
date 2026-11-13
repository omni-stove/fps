using System;

namespace Fps.Core
{
    /// <summary>
    /// 1 つの操作 (ダッシュ・しゃがみ・ADS など) が有効かどうか。フレームごとに <see cref="Update"/> でボタンの状態を渡す。
    /// <see cref="HoldMode.Hold"/> は押している間だけ、<see cref="HoldMode.Toggle"/> は押すたびに有効と無効を切り替える。
    /// </summary>
    public sealed class HoldToggleState
    {
        private HoldMode _mode;

        /// <param name="mode">初めのモード。解除した状態から始める。</param>
        public HoldToggleState(HoldMode mode)
        {
            _mode = Validate(mode);
        }

        /// <summary>今のモード。違うモードを入れると解除した状態から始める。同じモードを入れても何も変わらない。</summary>
        public HoldMode Mode
        {
            get => _mode;
            set
            {
                if (Validate(value) == _mode)
                {
                    return;
                }

                _mode = value;
                IsActive = false;
            }
        }

        /// <summary>操作が有効か。</summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 1 フレーム分のボタンの状態を渡し、有効かどうかを返す。
        /// Hold では、押し続けていても <see cref="Release"/> やモードの切り替えの後はボタンを押し直すまで有効にならない。
        /// </summary>
        /// <param name="isPressed">今ボタンが押されているか。</param>
        /// <param name="wasPressedThisFrame">今フレームにボタンが押されたか。同じフレームで離されていてもよい。</param>
        public bool Update(bool isPressed, bool wasPressedThisFrame)
        {
            switch (_mode)
            {
                case HoldMode.Hold:
                    // isPressed だけで決めると、自動解除した直後に押しっぱなしのボタンでまた有効になってしまう。
                    IsActive = isPressed && (IsActive || wasPressedThisFrame);
                    break;
                case HoldMode.Toggle:
                    if (wasPressedThisFrame)
                    {
                        IsActive = !IsActive;
                    }

                    break;
            }

            return IsActive;
        }

        /// <summary>外から解除する (例: ADS を始めたらダッシュをやめる)。既に解除されていれば何もしない。</summary>
        public void Release()
        {
            IsActive = false;
        }

        private static HoldMode Validate(HoldMode mode)
        {
            if (mode != HoldMode.Hold && mode != HoldMode.Toggle)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown hold mode");
            }

            return mode;
        }
    }
}
