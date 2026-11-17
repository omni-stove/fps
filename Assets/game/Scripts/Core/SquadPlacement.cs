namespace Shooter.Core
{
    public readonly struct SquadPlacement
    {
        public SquadPlacement(Side squad, int place)
        {
            Squad = squad;
            Place = place;
        }

        public Side Squad { get; }

        public int Place { get; }
    }
}
