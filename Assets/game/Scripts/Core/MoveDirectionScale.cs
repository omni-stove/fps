using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class MoveDirectionScale
    {
        public const float DefaultForward = 1f;
        public const float DefaultBackward = 1f;
        public const float DefaultSide = 1f;

        public static readonly MoveDirectionScale Uniform = new MoveDirectionScale(DefaultForward, DefaultBackward, DefaultSide);

        public float Forward { get; }

        public float Backward { get; }

        public float Side { get; }

        public MoveDirectionScale(float forward, float backward, float side)
        {
            Forward = Check(forward, nameof(forward));
            Backward = Check(backward, nameof(backward));
            Side = Check(side, nameof(side));
        }

        public Vector2 Apply(Vector2 input)
        {
            return new Vector2(input.X * Side, input.Y * (input.Y >= 0f ? Forward : Backward));
        }

        private static float Check(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }

            return value;
        }
    }
}
