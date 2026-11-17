using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IPlayerCarryLoad
    {
        float CarrySpeedMultiplier(CarryLoadSpec carry);
    }
}
