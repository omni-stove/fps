namespace Fps.Core
{
    /// <summary>ポーズ画面を開いている間に、ゲームの時間をどうするか。</summary>
    public enum PauseTimeMode
    {
        /// <summary>時間を止めない (オンライン)。画面を重ねて入力を止めるだけで、試合は進み撃たれる。</summary>
        KeepRunning = 0,

        /// <summary>時間を止める (オフラインの射撃場・キャンペーン)。閉じたら開く前の速さに戻す。</summary>
        Freeze = 1,
    }
}
