namespace Fps.Core
{
    public readonly struct SharedPing
    {
        public SharedPing(int pingerId, Team team, PingMarker marker)
        {
            PingerId = pingerId;
            Team = team;
            Marker = marker;
        }

        public int PingerId { get; }

        public Team Team { get; }

        public PingMarker Marker { get; }

        public int Id => Marker.Id;
    }
}
