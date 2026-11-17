using System.Numerics;

namespace Shooter.Core
{
    public readonly struct BotInputPlan
    {
        public Vector2 Move { get; }

        public float Yaw { get; }

        public float Pitch { get; }

        public bool Sprint { get; }

        public bool Crouch { get; }

        public bool Fire { get; }

        public Vector3 FireOrigin { get; }

        public Vector3 FireDirection { get; }

        public BotInputPlan(
            Vector2 move, float yaw, float pitch, bool sprint, bool crouch, bool fire, Vector3 fireOrigin, Vector3 fireDirection)
        {
            Move = move;
            Yaw = yaw;
            Pitch = pitch;
            Sprint = sprint;
            Crouch = crouch;
            Fire = fire;
            FireOrigin = fireOrigin;
            FireDirection = fireDirection;
        }
    }
}
