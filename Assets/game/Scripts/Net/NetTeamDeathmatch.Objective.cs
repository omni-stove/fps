using Fps.Core;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、<see cref="GameRules.Objective"/> が <see cref="MatchObjective.Domination"/> のときに
    /// 拠点の占領をサーバーで判定し、キル数に代えて拠点のポイントで勝敗を決める処理。
    /// </summary>
    /// <remarks>まだ何もしない。差し込み口はどれもサーバーでだけ、試合が動いている (Session がある) 間に呼ぶ。</remarks>
    public sealed partial class NetTeamDeathmatch
    {
        /// <summary>サーバーで試合を始められた (Session を作った) ときに呼ぶ。<see cref="ServerRules"/> が読める。</summary>
        partial void ObjectiveServerStarted();

        /// <summary>サーバーの毎フレーム、Session を進めた直後に呼ぶ。</summary>
        /// <param name="deltaSeconds">前のフレームからの秒数。</param>
        partial void ObjectiveServerTick(float deltaSeconds);

        /// <summary>プレイヤーを試合に加えた直後に呼ぶ。</summary>
        /// <param name="playerId">加えたプレイヤーの Id (クライアント Id と同じ値)。</param>
        /// <param name="player">加えたプレイヤー。</param>
        partial void ObjectivePlayerJoined(int playerId, NetPlayer player);

        /// <summary>プレイヤーを試合から外した直後に呼ぶ (切断を含む)。</summary>
        /// <param name="playerId">外したプレイヤーの Id。</param>
        partial void ObjectivePlayerLeft(int playerId);

        /// <summary>プレイヤーが倒されたときに呼ぶ。自滅なら killerId と victimId が同じ。</summary>
        partial void ObjectivePlayerKilled(int killerId, int victimId);

        /// <summary>試合が終わって次の試合のために点数などを戻した直後に呼ぶ。</summary>
        partial void ObjectiveMatchRestarted();

        /// <summary>
        /// 試合が終わったとき、勝ったチームを同期・ログに使う前に呼ぶ。拠点で勝敗を決めるなら winner を差し替える。
        /// 何もしなければ Session がキル数で決めた値のまま。
        /// </summary>
        /// <param name="winner">勝ったチーム。引き分けなら null。</param>
        partial void ObjectiveDecideWinner(ref Team? winner);

        /// <summary>試合が終わったときに呼ぶ。</summary>
        /// <param name="winner">勝ったチーム (<c>ObjectiveDecideWinner</c> の後の値)。引き分けなら null。</param>
        partial void ObjectiveMatchEnded(Team? winner);

        /// <summary>サーバーで試合を Despawn するとき、Session を手放す前に呼ぶ。</summary>
        partial void ObjectiveServerStopped();
    }
}
