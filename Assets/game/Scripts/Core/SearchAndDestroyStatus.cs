using System;

namespace Shooter.Core
{
    // Game は写しでなく進んでいる試合 (オフライン) か、届くたびに書き換わる写し (対戦) なので、受け取った側は出された時に読む。
    public sealed class SearchAndDestroyStatus
    {
        public SearchAndDestroyStatus(ISearchAndDestroyView game, int? localPlayerId)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            LocalPlayerId = localPlayerId;
        }

        public ISearchAndDestroyView Game { get; }

        public int? LocalPlayerId { get; }
    }
}
