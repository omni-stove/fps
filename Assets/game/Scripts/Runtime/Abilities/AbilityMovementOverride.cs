using System;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// 能力が渡した <see cref="IMovementOverride"/> を包み、能力を使っている間だけ有効にして、選ばれている間は
    /// <see cref="PlayerActionConditions.AbilityMoving"/> を必ず立てる。能力が終わると <see cref="AbilityContext"/> が登録を外す。
    /// </summary>
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
