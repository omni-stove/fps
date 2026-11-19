using System;
using System.Numerics;

namespace Shooter.Core
{
    // 船体を底の四隅の点で表す。点ごとの深さから浮力を出せば、傾いた側が深く沈んで押し戻す力が形を描かずに出るため。
    public sealed class BoatSpec
    {
        public const float DefaultMass = 800f;
        public const float DefaultLength = 4f;
        public const float DefaultWidth = 2f;
        public const float DefaultHullBottom = -0.5f;
        public const float DefaultHullHeight = 1f;
        public const float DefaultDraft = 0.3f;
        public const float DefaultHeaveDamping = 4f;
        public const float DefaultForwardDrag = 0.3f;
        public const float DefaultSideDrag = 3f;
        public const float DefaultThrust = 6f;
        public const float DefaultMaxForwardSpeed = 14f;
        public const float DefaultMaxReverseSpeed = 4f;
        public const float DefaultTurnRate = 50f;
        public const float DefaultTurnSpeed = 3f;
        public const float DefaultTurnResponsiveness = 2f;

        public const int HullPointCount = 4;

        public BoatSpec(
            float mass = DefaultMass,
            float length = DefaultLength,
            float width = DefaultWidth,
            float hullBottom = DefaultHullBottom,
            float hullHeight = DefaultHullHeight,
            float draft = DefaultDraft,
            float heaveDamping = DefaultHeaveDamping,
            float forwardDrag = DefaultForwardDrag,
            float sideDrag = DefaultSideDrag,
            float thrust = DefaultThrust,
            float maxForwardSpeed = DefaultMaxForwardSpeed,
            float maxReverseSpeed = DefaultMaxReverseSpeed,
            float turnRate = DefaultTurnRate,
            float turnSpeed = DefaultTurnSpeed,
            float turnResponsiveness = DefaultTurnResponsiveness)
        {
            RequirePositive(mass, nameof(mass));
            RequirePositive(length, nameof(length));
            RequirePositive(width, nameof(width));
            if (float.IsNaN(hullBottom) || float.IsInfinity(hullBottom))
            {
                throw new ArgumentOutOfRangeException(nameof(hullBottom), hullBottom, "hullBottom must be finite");
            }

            RequirePositive(hullHeight, nameof(hullHeight));
            RequirePositive(draft, nameof(draft));
            // 喫水が船体より深いと、沈み切っても重さを支えられず、浮かずに沈み続けるため。
            if (draft > hullHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(draft), draft, $"draft must not exceed hullHeight {hullHeight}");
            }

            RequireNonNegative(heaveDamping, nameof(heaveDamping));
            RequireNonNegative(forwardDrag, nameof(forwardDrag));
            RequireNonNegative(sideDrag, nameof(sideDrag));
            RequireNonNegative(thrust, nameof(thrust));
            RequireNonNegative(maxForwardSpeed, nameof(maxForwardSpeed));
            RequireNonNegative(maxReverseSpeed, nameof(maxReverseSpeed));
            RequireNonNegative(turnRate, nameof(turnRate));
            RequirePositive(turnSpeed, nameof(turnSpeed));
            RequireNonNegative(turnResponsiveness, nameof(turnResponsiveness));

            Mass = mass;
            Length = length;
            Width = width;
            HullBottom = hullBottom;
            HullHeight = hullHeight;
            Draft = draft;
            HeaveDamping = heaveDamping;
            ForwardDrag = forwardDrag;
            SideDrag = sideDrag;
            Thrust = thrust;
            MaxForwardSpeed = maxForwardSpeed;
            MaxReverseSpeed = maxReverseSpeed;
            TurnRate = turnRate;
            TurnSpeed = turnSpeed;
            TurnResponsiveness = turnResponsiveness;
        }

        public static BoatSpec Default => new BoatSpec();

        public float Mass { get; }

        public float Length { get; }

        public float Width { get; }

        public float HullBottom { get; }

        public float HullHeight { get; }

        public float Draft { get; }

        public float HeaveDamping { get; }

        public float ForwardDrag { get; }

        public float SideDrag { get; }

        public float Thrust { get; }

        public float MaxForwardSpeed { get; }

        public float MaxReverseSpeed { get; }

        public float TurnRate { get; }

        public float TurnSpeed { get; }

        public float TurnResponsiveness { get; }

        public Vector3 Propeller => new Vector3(0f, HullBottom, -Length / 2f);

        public Vector3 HullPoint(int index)
        {
            var halfWidth = Width / 2f;
            var halfLength = Length / 2f;
            switch (index)
            {
                case 0: return new Vector3(-halfWidth, HullBottom, halfLength);
                case 1: return new Vector3(halfWidth, HullBottom, halfLength);
                case 2: return new Vector3(-halfWidth, HullBottom, -halfLength);
                case 3: return new Vector3(halfWidth, HullBottom, -halfLength);
                default: throw new ArgumentOutOfRangeException(nameof(index), index, $"index must be between 0 and {HullPointCount - 1}");
            }
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }
    }
}
