using System;

namespace Shooter.Core
{
    /// <summary>
    /// 爆破の Runner が <see cref="ModeStatus.Detail"/> に入れる、爆破固有の状態。試合 (<see cref="Game"/>) と、この画面で操作しているプレイヤーの ID。
    /// </summary>
    /// <remarks><see cref="Game"/> は進んでいる試合そのもので、写しではない。受け取った側は出された時に読む。</remarks>
    public sealed class SearchAndDestroyStatus
    {
        /// <param name="game">進めている試合。</param>
        /// <param name="localPlayerId">この画面で操作しているプレイヤー。いなければ null。</param>
        /// <exception cref="ArgumentNullException">game が null。</exception>
        public SearchAndDestroyStatus(SearchAndDestroy game, int? localPlayerId)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            LocalPlayerId = localPlayerId;
        }

        public SearchAndDestroy Game { get; }

        /// <summary>この画面で操作しているプレイヤー。いなければ null。</summary>
        public int? LocalPlayerId { get; }
    }
}
