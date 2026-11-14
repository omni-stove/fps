using System;
using Fps.Core;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetHud"/> のうち、サーバーから届いた放置の警告 (<see cref="NetPlayer"/> の .Idle.cs) を案内の欄に
    /// <see cref="HudFormat.IdleWarning"/> で出す処理。
    /// </summary>
    /// <remarks>
    /// サーバーは警告を出すときと消すときにだけ送るので、残り秒数は届いた値から <see cref="Tick"/> で自分で数える。
    /// 0 まで数えても消さない (切断が届くまで出しておく)。
    /// </remarks>
    public sealed partial class NetHud
    {
        // 切断されるまでの残り秒数。警告していなければ null
        private float? _idleRemaining;
        private int _idleDisplayedSeconds;

        /// <summary>放置の警告を出しているか。</summary>
        public bool IsShowingIdleWarning => _idleRemaining.HasValue;

        /// <summary>「あと remainingSeconds 秒で切断」の警告を出し、ここから数える。負・NaN は 0 として扱う。</summary>
        public void ShowIdleWarning(float remainingSeconds)
        {
            _idleRemaining = remainingSeconds > 0f && !float.IsInfinity(remainingSeconds) ? remainingSeconds : 0f;
            _idleDisplayedSeconds = HudFormat.DisplayedSeconds(_idleRemaining.Value);
            ShowNotice();
        }

        /// <summary>放置の警告を消す。出していなければ何もしない。</summary>
        public void ClearIdleWarning()
        {
            if (!_idleRemaining.HasValue)
            {
                return;
            }

            _idleRemaining = null;
            _idleDisplayedSeconds = 0;
            ShowNotice();
        }

        // 秒の桁が変わったときだけ案内を書き直す
        private void IdleTick(float deltaSeconds)
        {
            if (!_idleRemaining.HasValue)
            {
                return;
            }

            _idleRemaining = Math.Max(0f, _idleRemaining.Value - Math.Max(0f, deltaSeconds));
            var displayed = HudFormat.DisplayedSeconds(_idleRemaining.Value);
            if (displayed == _idleDisplayedSeconds)
            {
                return;
            }

            _idleDisplayedSeconds = displayed;
            ShowNotice();
        }

        // ShowNotice が、試合の終わりの案内の次に優先して出す。警告は生きて試合をしている間しか届かない
        private bool TryShowIdleNotice()
        {
            if (!_idleRemaining.HasValue)
            {
                return false;
            }

            Model.SetNotice(HudFormat.IdleWarning(_idleDisplayedSeconds));
            return true;
        }
    }
}
