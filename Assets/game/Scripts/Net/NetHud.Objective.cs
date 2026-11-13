namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetHud"/> のうち、試合の勝敗を拠点で決めるとき (<see cref="Fps.Core.GameRules.Objective"/> が
    /// <see cref="Fps.Core.MatchObjective.Domination"/>) に、拠点の持ち主・占領の進み・ポイントを出す処理。
    /// </summary>
    /// <remarks>まだ何もしない。</remarks>
    public sealed partial class NetHud
    {
        /// <summary>試合を購読し終えたときに呼ぶ。<see cref="Match"/> は新しい試合で、試合が無くなったときは null。</summary>
        partial void ObjectiveMatchBound();

        /// <summary>試合の購読を外す直前に呼ぶ。<see cref="Match"/> はまだ外す前の試合 (無ければ null)。</summary>
        partial void ObjectiveMatchUnbinding();

        /// <summary>毎フレーム呼ぶ。</summary>
        /// <param name="deltaSeconds">前のフレームからの秒数。</param>
        partial void ObjectiveTick(float deltaSeconds);

        /// <summary>プレイヤー・試合の購読をすべて外した後に呼ぶ (破棄するとき)。</summary>
        partial void ObjectiveReleased();
    }
}
