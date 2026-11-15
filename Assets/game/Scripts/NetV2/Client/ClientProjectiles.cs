using System.Diagnostics.CodeAnalysis;
using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
{
    [SuppressMessage("Performance", "CA1822", Justification = "Each feature keeps what it shows on the instance")]
    public sealed class ClientProjectiles
    {
        public void Spawned(ProjectileSpawnedMessage message)
        {
        }

        public void Ended(ProjectileEndedMessage message)
        {
        }

        public void Apply(SnapshotMessage snapshot)
        {
        }

        public void LocalFired(FireRequestMessage request, WeaponSpec spec)
        {
        }
    }
}
