using System;

namespace Shooter.Core
{
    public sealed class AircraftSpec
    {
        public const float DefaultMass = 8000f;
        public const float DefaultMaxThrust = 15f;

        public AircraftSpec(float mass = DefaultMass, float maxThrust = DefaultMaxThrust, AircraftAeroSpec aero = null)
        {
            RequirePositive(mass, nameof(mass));
            RequirePositive(maxThrust, nameof(maxThrust));

            Mass = mass;
            MaxThrust = maxThrust;
            Aero = aero ?? AircraftAeroSpec.Default;
        }

        public static AircraftSpec Default => new AircraftSpec();

        public float Mass { get; }

        // 力でなく加速 (m/s²) で持つのは、質量を変えても飛び方が変わらず、質量はぶつかったときの重さだけを決めるようにするため。
        public float MaxThrust { get; }

        public AircraftAeroSpec Aero { get; }

        // 抗力の係数を最高速度から逆算するのは、推力を開き切って水平に飛んだときに最高速度で釣り合うようにするため。
        public float DragCoefficient => MaxThrust / (Aero.MaxSpeed * Aero.MaxSpeed);

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
