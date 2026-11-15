using System.Diagnostics.CodeAnalysis;
using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
{
    [SuppressMessage("Performance", "CA1822", Justification = "Each feature keeps what it shows on the instance")]
    public sealed class ClientBeams
    {
        public void Receive(BeamStateMessage message)
        {
        }

        public void LocalTrigger(FireRequestMessage request, WeaponSpec spec)
        {
        }
    }
}
