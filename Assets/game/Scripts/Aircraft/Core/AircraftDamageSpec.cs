using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public enum AircraftDamageEffect
    {
        LeftLift,
        RightLift,
        Control,
        Thrust,
    }

    public sealed class AircraftDamagePartSpec
    {
        public AircraftDamagePartSpec(string id, AircraftDamageEffect effect, int durability)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (!Enum.IsDefined(typeof(AircraftDamageEffect), effect))
            {
                throw new ArgumentOutOfRangeException(nameof(effect), effect, "effect is not a defined AircraftDamageEffect");
            }

            if (durability <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durability), durability, "durability must be positive");
            }

            Id = id;
            Effect = effect;
            Durability = durability;
        }

        public string Id { get; }

        public AircraftDamageEffect Effect { get; }

        public int Durability { get; }
    }

    public sealed class AircraftDamageSpec
    {
        public const string DefaultLeftWingId = "wing_left";
        public const string DefaultRightWingId = "wing_right";
        public const string DefaultTailId = "tail";
        public const string DefaultEngineId = "engine";
        public const int DefaultWingDurability = 300;
        public const int DefaultTailDurability = 200;
        public const int DefaultEngineDurability = 250;
        public const float DefaultMinLift = 0.2f;
        public const float DefaultMinControl = 0.3f;
        public const float DefaultMinThrust = 0f;

        private readonly AircraftDamagePartSpec[] _parts;

        // parts を null にすると既定の 4 部位を使う。空の配列なら、どこを撃たれても飛び方は変わらない。
        public AircraftDamageSpec(
            IEnumerable<AircraftDamagePartSpec> parts = null,
            float minLift = DefaultMinLift,
            float minControl = DefaultMinControl,
            float minThrust = DefaultMinThrust)
        {
            RequireFraction(minLift, nameof(minLift));
            RequireFraction(minControl, nameof(minControl));
            RequireFraction(minThrust, nameof(minThrust));

            var list = new List<AircraftDamagePartSpec>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in parts ?? DefaultParts())
            {
                if (part == null)
                {
                    throw new ArgumentException($"parts[{list.Count}] is empty", nameof(parts));
                }

                if (!ids.Add(part.Id))
                {
                    throw new ArgumentException($"part id '{part.Id}' appears more than once", nameof(parts));
                }

                list.Add(part);
            }

            _parts = list.ToArray();
            MinLift = minLift;
            MinControl = minControl;
            MinThrust = minThrust;
        }

        public static AircraftDamageSpec Default => new AircraftDamageSpec();

        public IReadOnlyList<AircraftDamagePartSpec> Parts => _parts;

        public float MinLift { get; }

        public float MinControl { get; }

        public float MinThrust { get; }

        public static AircraftDamagePartSpec[] DefaultParts()
        {
            return new[]
            {
                new AircraftDamagePartSpec(DefaultLeftWingId, AircraftDamageEffect.LeftLift, DefaultWingDurability),
                new AircraftDamagePartSpec(DefaultRightWingId, AircraftDamageEffect.RightLift, DefaultWingDurability),
                new AircraftDamagePartSpec(DefaultTailId, AircraftDamageEffect.Control, DefaultTailDurability),
                new AircraftDamagePartSpec(DefaultEngineId, AircraftDamageEffect.Thrust, DefaultEngineDurability),
            };
        }

        public AircraftDamagePartSpec FindPart(string id)
        {
            foreach (var part in _parts)
            {
                if (string.Equals(part.Id, id, StringComparison.Ordinal))
                {
                    return part;
                }
            }

            return null;
        }

        public float MinOf(AircraftDamageEffect effect)
        {
            switch (effect)
            {
                case AircraftDamageEffect.LeftLift:
                case AircraftDamageEffect.RightLift:
                    return MinLift;
                case AircraftDamageEffect.Control:
                    return MinControl;
                case AircraftDamageEffect.Thrust:
                    return MinThrust;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect, "effect is not a defined AircraftDamageEffect");
            }
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
