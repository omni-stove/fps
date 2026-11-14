namespace Fps.Core
{
    public enum SharedPingChangeKind
    {
        Placed,
        Moved,
        Lost,
        Removed,
    }

    public readonly struct SharedPingChange
    {
        private SharedPingChange(SharedPingChangeKind kind, SharedPing ping, bool refreshed, PingRemoval removal)
        {
            Kind = kind;
            Ping = ping;
            Refreshed = refreshed;
            Removal = removal;
        }

        public SharedPingChangeKind Kind { get; }

        public SharedPing Ping { get; }

        public bool Refreshed { get; }

        public PingRemoval Removal { get; }

        public static SharedPingChange Placed(SharedPing ping, bool refreshed) =>
            new SharedPingChange(SharedPingChangeKind.Placed, ping, refreshed, default);

        public static SharedPingChange Moved(SharedPing ping) => new SharedPingChange(SharedPingChangeKind.Moved, ping, false, default);

        public static SharedPingChange Lost(SharedPing ping) => new SharedPingChange(SharedPingChangeKind.Lost, ping, false, default);

        public static SharedPingChange Removed(SharedPing ping, PingRemoval removal) =>
            new SharedPingChange(SharedPingChangeKind.Removed, ping, false, removal);
    }
}
