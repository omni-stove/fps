using System.Numerics;

namespace Shooter.Core
{
    public readonly struct BotErrand
    {
        public BotErrand(BotErrandKind kind, Vector3 destination, int targetIndex, bool overridesCombat)
        {
            Kind = kind;
            Destination = destination;
            TargetIndex = targetIndex;
            OverridesCombat = overridesCombat;
        }

        public static BotErrand None => new BotErrand(BotErrandKind.None, Vector3.Zero, -1, false);

        public BotErrandKind Kind { get; }

        public Vector3 Destination { get; }

        public int TargetIndex { get; }

        public bool OverridesCombat { get; }

        public bool HasErrand => Kind != BotErrandKind.None;

        public bool IsOrder => IsOrderKind(Kind);

        public static BotErrand Hold(Vector3 place) => new BotErrand(BotErrandKind.Hold, place, -1, true);

        public static BotErrand Attack(Vector3 target) => new BotErrand(BotErrandKind.Attack, target, -1, true);

        public static BotErrand Follow(Vector3 leader) => new BotErrand(BotErrandKind.Follow, leader, -1, false);

        public static bool IsOrderKind(BotErrandKind kind)
        {
            return kind == BotErrandKind.Hold || kind == BotErrandKind.Attack || kind == BotErrandKind.Follow;
        }

        public BotErrand WithDestination(Vector3 destination) => new BotErrand(Kind, destination, TargetIndex, OverridesCombat);
    }
}
