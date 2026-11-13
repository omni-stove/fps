using System;

namespace Fps.Core
{
    /// <summary>
    /// 拾える陣営の条件。既定値 (default) は <see cref="Anyone"/>。
    /// </summary>
    public readonly struct PickupEligibility : IEquatable<PickupEligibility>
    {
        /// <summary>誰でも拾える。</summary>
        public static readonly PickupEligibility Anyone = default;

        private PickupEligibility(PickupSideRule rule, Side side)
        {
            Rule = rule;
            Side = side;
        }

        /// <summary>絞り方。</summary>
        public PickupSideRule Rule { get; }

        /// <summary><see cref="Rule"/> が使う陣営。<see cref="PickupSideRule.Anyone"/> では意味を持たない。</summary>
        public Side Side { get; }

        /// <summary>side の人だけが拾える。</summary>
        public static PickupEligibility Only(Side side) => new PickupEligibility(PickupSideRule.OnlySide, side);

        /// <summary>side 以外の人が拾える。</summary>
        public static PickupEligibility Except(Side side) => new PickupEligibility(PickupSideRule.ExceptSide, side);

        /// <summary>rule と side から作る。定義されていない rule は <see cref="ArgumentOutOfRangeException"/>。</summary>
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

        /// <summary>陣営が collectorSide (陣営を持たなければ null) の人が拾えるなら true。</summary>
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

        /// <example><c>anyone</c>・<c>only:1</c>・<c>except:0</c></example>
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
