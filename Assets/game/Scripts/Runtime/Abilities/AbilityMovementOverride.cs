using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 能力が渡した IMovementOverride を包むのは、選ばれている間の PlayerActionConditions.AbilityMoving を能力任せにせず必ず立てるため。
    internal sealed class AbilityMovementOverride : IMovementOverride
    {
        private readonly IMovementOverride _inner;

        public AbilityMovementOverride(IMovementOverride inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IMovementOverride Inner => _inner;

        public int Priority => _inner.Priority;

        public bool IsActive => _inner.IsActive;

        public PlayerActionConditions Conditions => _inner.Conditions | PlayerActionConditions.AbilityMoving;

        public void Begin(PlayerController mover)
        {
            _inner.Begin(mover);
        }

        public MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            return _inner.Step(mover, input, deltaSeconds);
        }

        public void End(PlayerController mover)
        {
            _inner.End(mover);
        }
    }
}
