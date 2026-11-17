using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct ImpactHit
    {
        private ImpactHit(bool isCharacter, HitZone zone, ZoneDamageMultipliers multipliers, Vector3 point, float distance)
        {
            if (!(distance >= 0f) || float.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must be a non-negative finite number");
            }

            IsCharacter = isCharacter;
            Zone = zone;
            Multipliers = multipliers;
            Point = point;
            Distance = distance;
        }

        public bool IsCharacter { get; }

        public HitZone Zone { get; }

        public ZoneDamageMultipliers Multipliers { get; }

        public Vector3 Point { get; }

        public float Distance { get; }

        public static ImpactHit Character(HitZone zone, Vector3 point, float distance, ZoneDamageMultipliers multipliers)
        {
            return new ImpactHit(true, zone, multipliers ?? throw new ArgumentNullException(nameof(multipliers)), point, distance);
        }

        public static ImpactHit Structure(Vector3 point, float distance)
        {
            return new ImpactHit(false, HitZone.Body, ZoneDamageMultipliers.Default, point, distance);
        }
    }
}
