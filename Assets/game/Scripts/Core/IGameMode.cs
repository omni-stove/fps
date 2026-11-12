using System;

namespace Fps.Core
{
    /// <summary>
    /// 試合のルール (ゲームモード) に共通する操作。時間は <see cref="Tick"/> で外から進め、
    /// キルと死亡は ID で報告する。勝敗などの結果は各モード固有のプロパティで持つ。
    /// </summary>
    public interface IGameMode
    {
        /// <summary>状態が変わるたびに新しい状態を渡して呼ばれる。呼ばれた時点で <see cref="State"/> は更新済み。</summary>
        event Action<GameModeState> StateChanged;

        GameModeState State { get; }

        /// <summary>試合を始めて Playing にする。Waiting 以外で呼んだときの扱いは実装による。</summary>
        void Start();

        /// <summary>時間を deltaSeconds 進める。負や NaN は ArgumentOutOfRangeException。Playing 以外では何もしない。</summary>
        void Tick(float deltaSeconds);

        /// <summary>killerId が victimId を倒したことを報告する。Playing 以外では何もしない。</summary>
        void ReportKill(int killerId, int victimId);

        /// <summary>playerId が倒した相手なしで死んだこと (落下・環境ダメージなど) を報告する。Playing 以外では何もしない。</summary>
        void ReportDeath(int playerId);
    }
}
