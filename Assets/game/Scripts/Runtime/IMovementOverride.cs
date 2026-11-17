using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IMovementOverride
    {
        int Priority { get; }

        bool IsActive { get; }

        PlayerActionConditions Conditions { get; }

        void Begin(PlayerController mover);

        MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds);

        void End(PlayerController mover);
    }
}
