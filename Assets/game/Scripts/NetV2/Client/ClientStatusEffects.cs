using System.Diagnostics.CodeAnalysis;
using Fps.Protocol;

namespace Fps.NetV2
{
    [SuppressMessage("Performance", "CA1822", Justification = "Each feature keeps what it shows on the instance")]
    public sealed class ClientStatusEffects
    {
        public void Receive(StatusEffectMessage message)
        {
        }
    }
}
