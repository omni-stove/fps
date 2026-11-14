namespace Fps.Core
{
    /// <summary>バトルロイヤルを始めたときに、プレイヤーをどこから始めさせるか。</summary>
    public enum BattleRoyaleStart
    {
        /// <summary>出現点の地面から始める。</summary>
        Ground,

        /// <summary>出現点の上空に出し、パラシュートを開いた状態で降りさせる。開けなければ地面から始める。</summary>
        Parachute,
    }
}
