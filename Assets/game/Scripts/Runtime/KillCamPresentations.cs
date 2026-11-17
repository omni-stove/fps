namespace Fps.Runtime
{
    /// <summary>
    /// キルカムの演出 (<see cref="IKillCamPresentation"/>) の登録口。<see cref="Current"/> に入れると差し替えられ、null を入れると <see cref="Plain"/> に戻る。
    /// 枠と文字は演出でなく、UI が <see cref="GameSignals.KillCam"/> を購読して出す。
    /// </summary>
    public static class KillCamPresentations
    {
        private static IKillCamPresentation _current;

        /// <summary>何も重ねず、記録どおりの速さで再生する演出。</summary>
        public static IKillCamPresentation Plain { get; } = new PlainKillCamPresentation();

        /// <summary>使う演出。差し替えていなければ <see cref="Plain"/>。</summary>
        public static IKillCamPresentation Current
        {
            get => GameUi.IsAlive(_current) ? _current : Plain;
            set => _current = value;
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
