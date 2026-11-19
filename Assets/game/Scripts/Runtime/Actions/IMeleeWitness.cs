using UnityEngine;

namespace Shooter.Runtime
{
    public interface IMeleeWitness
    {
        string Id { get; }

        GameObject gameObject { get; }

        bool IsUnaware { get; }

        string StageName { get; }

        bool Witness(Damageable attacker, float awareness);
    }
}
