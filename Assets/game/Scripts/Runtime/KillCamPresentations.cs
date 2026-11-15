using System;

namespace Fps.Runtime
{
    /// <summary>
    /// キルカムの演出 (<see cref="IKillCamPresentation"/>) の登録口。<see cref="Current"/> に入れると差し替えられ、null を入れると既定に戻る。
    /// 既定は UI のアセンブリ (Fps.Runtime.UI) が起動時に <see cref="UseDefault"/> で登録する (HUD に枠と見出しを出す)。
    /// それも無ければ <see cref="Plain"/> (記録どおりの速さで再生し、枠と文字は出さない) を使う。
    /// </summary>
    public static class KillCamPresentations
    {
        private static IKillCamPresentation _current;
        private static IKillCamPresentation _default;

        /// <summary>何も重ねず、記録どおりの速さで再生する演出。</summary>
        public static IKillCamPresentation Plain { get; } = new PlainKillCamPresentation();

        /// <summary>使う演出。差し替えていなければ既定、既定も無ければ <see cref="Plain"/>。</summary>
        public static IKillCamPresentation Current
        {
            get
            {
                if (GameUi.IsAlive(_current))
                {
                    return _current;
                }

                return GameUi.IsAlive(_default) ? _default : Plain;
            }
            set => _current = value;
        }

        /// <summary>
        /// 既定の演出を presentation にする。<see cref="Current"/> を差し替えていればそちらを使い続ける
        /// (起動時の登録の順番に依らず、差し替えが既定に上書きされないよう、既定は別に持つ)。
        /// </summary>
        /// <exception cref="ArgumentNullException">presentation が null。</exception>
        public static void UseDefault(IKillCamPresentation presentation)
        {
            _default = presentation ?? throw new ArgumentNullException(nameof(presentation));
        }

        private sealed class PlainKillCamPresentation : IKillCamPresentation
        {
            public float PlaybackSpeed => 1f;

            public void Begin(KillCamShot shot)
            {
            }

            public void Progress(float progress)
            {
            }

            public void End(bool completed)
            {
            }
        }
    }
}
