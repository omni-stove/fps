using System;

namespace Shooter.Core
{
    // 倍率でなく 1 から欠けた分で持つのは、default(AircraftCondition) が「どこも壊れていない」になり、渡し忘れた呼び出しで翼も舵も効かなくならないようにするため。
    public readonly struct AircraftCondition : IEquatable<AircraftCondition>
    {
        private readonly float _leftLiftLoss;
        private readonly float _rightLiftLoss;
        private readonly float _controlLoss;
        private readonly float _thrustLoss;

        public AircraftCondition(float leftLift = 1f, float rightLift = 1f, float control = 1f, float thrust = 1f)
        {
            RequireFraction(leftLift, nameof(leftLift));
            RequireFraction(rightLift, nameof(rightLift));
            RequireFraction(control, nameof(control));
            RequireFraction(thrust, nameof(thrust));
            _leftLiftLoss = 1f - leftLift;
            _rightLiftLoss = 1f - rightLift;
            _controlLoss = 1f - control;
            _thrustLoss = 1f - thrust;
        }

        public static AircraftCondition Intact => default;

        public float LeftLift => 1f - _leftLiftLoss;

        public float RightLift => 1f - _rightLiftLoss;

        public float Lift => (LeftLift + RightLift) * 0.5f;

        public float Control => 1f - _controlLoss;

        public float Thrust => 1f - _thrustLoss;

        public bool Equals(AircraftCondition other)
        {
            return _leftLiftLoss.Equals(other._leftLiftLoss)
                && _rightLiftLoss.Equals(other._rightLiftLoss)
                && _controlLoss.Equals(other._controlLoss)
                && _thrustLoss.Equals(other._thrustLoss);
        }

        public override bool Equals(object obj)
        {
            return obj is AircraftCondition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_leftLiftLoss, _rightLiftLoss, _controlLoss, _thrustLoss);
        }

        public static bool operator ==(AircraftCondition left, AircraftCondition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AircraftCondition left, AircraftCondition right)
        {
            return !left.Equals(right);
        }

        private static void RequireFraction(float value, string name)
        {
            if (!(value >= 0f && value <= 1f))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between 0 and 1");
            }
        }
    }
}
