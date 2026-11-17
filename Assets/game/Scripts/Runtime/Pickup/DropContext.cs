using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public readonly struct DropContext
    {
        public DropContext(DropOnDeath source, Damageable victim, GameObject killer, Side? victimSide, Vector3 position)
        {
            Source = source;
            Victim = victim;
            Killer = killer;
            VictimSide = victimSide;
            Position = position;
        }

        public DropOnDeath Source { get; }

        public Damageable Victim { get; }

        public GameObject Killer { get; }

        public Side? VictimSide { get; }

        public Vector3 Position { get; }
    }
}
