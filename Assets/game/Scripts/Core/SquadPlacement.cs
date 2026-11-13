namespace Fps.Core
{
    /// <summary>バトルロイヤル (<see cref="BattleRoyaleSession"/>) の分隊 1 つの順位。同時に脱落した分隊は同じ順位になる。</summary>
    public readonly struct SquadPlacement
    {
        public SquadPlacement(Side squad, int place)
        {
            Squad = squad;
            Place = place;
        }

        public Side Squad { get; }

        /// <summary>順位。1 が優勝。</summary>
        public int Place { get; }
    }
}
