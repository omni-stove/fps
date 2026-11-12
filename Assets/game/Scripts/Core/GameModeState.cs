namespace Fps.Core
{
    /// <summary>ゲームモード (<see cref="IGameMode"/>) の進行状態。どのモードも Waiting → Playing → Ended の順に進む。</summary>
    public enum GameModeState
    {
        /// <summary>開始前。キルも時間も数えない。</summary>
        Waiting,

        /// <summary>試合中。</summary>
        Playing,

        /// <summary>終了。以後、状態は変わらない。結果は各モード固有のプロパティで持つ。</summary>
        Ended,
    }
}
