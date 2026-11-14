using System;

namespace Fps.Core
{
    public sealed class ZoneDamageMultipliers
    {
        public static readonly ZoneDamageMultipliers Default = new ZoneDamageMultipliers(
            DamageCalculator.HeadMultiplier,
            DamageCalculator.BodyMultiplier,
            DamageCalculator.LimbMultiplier,
            DamageCalculator.WeakPointMultiplier);

        public double Head { get; }

        public double Body { get; }

        public double Limb { get; }

        public double WeakPoint { get; }

        public ZoneDamageMultipliers(double head, double body, double limb, double weakPoint)
        {
            Require(head, nameof(head));
            Require(body, nameof(body));
            Require(limb, nameof(limb));
            Require(weakPoint, nameof(weakPoint));
            Head = head;
            Body = body;
            Limb = limb;
            WeakPoint = weakPoint;
        }

        public double For(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head:
                    return Head;
                case HitZone.Body:
                    return Body;
                case HitZone.Limb:
                    return Limb;
                case HitZone.WeakPoint:
                    return WeakPoint;
                default:
                    throw new ArgumentOutOfRangeException(nameof(zone), zone, "unknown hit zone");
            }
        }

        private static void Require(double value, string name)
        {
            if (!(value >= 0d) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be non-negative and finite");
            }
        }
    }
}
