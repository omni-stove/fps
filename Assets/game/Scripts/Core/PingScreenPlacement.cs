namespace Shooter.Core
{
    public readonly struct PingScreenPlacement
    {
        public PingScreenPlacement(bool isOnScreen, float x, float y, float angleDegrees, PingScreenEdge edge)
        {
            IsOnScreen = isOnScreen;
            X = x;
            Y = y;
            AngleDegrees = angleDegrees;
            Edge = edge;
        }

        public bool IsOnScreen { get; }

        public float X { get; }

        public float Y { get; }

        public float AngleDegrees { get; }

        public PingScreenEdge Edge { get; }
    }
}
