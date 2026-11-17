using System.Numerics;

namespace Shooter.Core
{
    public readonly struct EnemyDecision
    {
        public static EnemyDecision None => default;

        public Vector3? MoveTo { get; }

        public Vector3? LookAt { get; }

        public bool Fire { get; }

        public bool Crouch { get; }

        public EnemyDecision(Vector3? moveTo, Vector3? lookAt, bool fire)
            : this(moveTo, lookAt, fire, false)
        {
        }

        public EnemyDecision(Vector3? moveTo, Vector3? lookAt, bool fire, bool crouch)
        {
            MoveTo = moveTo;
            LookAt = lookAt;
            Fire = fire;
            Crouch = crouch;
        }
    }
}
