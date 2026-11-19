using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IAircraftConditionSource
    {
        AircraftCondition Condition { get; }
    }
}
