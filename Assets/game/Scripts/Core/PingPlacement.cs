namespace Shooter.Core
{
    public readonly struct PingPlacement
    {
        public PingPlacement(PingMarker marker, bool refreshed, PingMarker? replaced)
        {
            Marker = marker;
            Refreshed = refreshed;
            Replaced = replaced;
        }

        public PingMarker Marker { get; }

        public bool Refreshed { get; }

        public PingMarker? Replaced { get; }
    }
}
