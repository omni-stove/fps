using System;

namespace Shooter.Core
{
    public readonly struct PickupEligibility : IEquatable<PickupEligibility>
    {
        public static readonly PickupEligibility Anyone = new PickupEligibility(PickupSideRule.Anyone, default(Side));

        private PickupEligibility(PickupSideRule rule, Side side)
        {
            Rule = rule;
            Side = side;
        }

        public PickupSideRule Rule { get; }

        public Side Side { get; }

        public static PickupEligibility Only(Side side) => new PickupEligibility(PickupSideRule.OnlySide, side);

        public static PickupEligibility Except(Side side) => new PickupEligibility(PickupSideRule.ExceptSide, side);

        public static PickupEligibility Of(PickupSideRule rule, Side side)
        {
            switch (rule)
            {
                case PickupSideRule.Anyone:
                    return Anyone;
                case PickupSideRule.OnlySide:
                    return Only(side);
                case PickupSideRule.ExceptSide:
                    return Except(side);
                default:
                    throw new ArgumentOutOfRangeException(nameof(rule), rule, "rule is not a valid PickupSideRule");
            }
        }

        public bool Allows(Side? collectorSide)
        {
            switch (Rule)
            {
                case PickupSideRule.OnlySide:
                    return collectorSide.HasValue && collectorSide.Value == Side;
                case PickupSideRule.ExceptSide:
                    return !collectorSide.HasValue || collectorSide.Value != Side;
                default:
                    return true;
            }
        }

        public bool Equals(PickupEligibility other) => Rule == other.Rule && (Rule == PickupSideRule.Anyone || Side == other.Side);

        public override bool Equals(object obj) => obj is PickupEligibility other && Equals(other);

        public override int GetHashCode() => Rule == PickupSideRule.Anyone ? 0 : ((int)Rule * 397) ^ Side.Id;

        public override string ToString()
        {
            switch (Rule)
            {
                case PickupSideRule.OnlySide:
                    return "only:" + Side.Id;
                case PickupSideRule.ExceptSide:
                    return "except:" + Side.Id;
                default:
                    return "anyone";
            }
        }

        public static bool operator ==(PickupEligibility left, PickupEligibility right) => left.Equals(right);

        public static bool operator !=(PickupEligibility left, PickupEligibility right) => !left.Equals(right);
    }
}
