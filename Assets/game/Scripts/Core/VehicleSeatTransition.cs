using System;

namespace Shooter.Core
{
    // 席は乗り込み・降車の間も持ったままにする。途中で他の人に席を取られたり、降りる途中で乗り物から外れたりしないようにするため。
    public sealed class VehicleSeatTransition
    {
        public VehicleSeatTransitionKind Kind { get; private set; }

        public float Seconds { get; private set; }

        public float Elapsed { get; private set; }

        public bool IsActive => Kind != VehicleSeatTransitionKind.None;

        public float Progress => !IsActive ? 1f : Seconds > 0f ? Math.Min(Elapsed / Seconds, 1f) : 1f;

        public bool IsComplete => IsActive && Elapsed >= Seconds;

        public void Begin(VehicleSeatTransitionKind kind, float seconds)
        {
            if (kind == VehicleSeatTransitionKind.None || !Enum.IsDefined(typeof(VehicleSeatTransitionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be Entering or Exiting");
            }

            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative and must be finite");
            }

            Kind = kind;
            Seconds = seconds;
            Elapsed = 0f;
        }

        public VehicleSeatTransitionKind Advance(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            if (!IsActive)
            {
                return VehicleSeatTransitionKind.None;
            }

            Elapsed = Math.Min(Elapsed + deltaSeconds, Seconds);
            if (Elapsed < Seconds)
            {
                return VehicleSeatTransitionKind.None;
            }

            var finished = Kind;
            Clear();
            return finished;
        }

        public void Clear()
        {
            Kind = VehicleSeatTransitionKind.None;
            Seconds = 0f;
            Elapsed = 0f;
        }
    }
}
