using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct LadderSpan
    {
        public const float FaceGap = 0.05f;

        public const float HeightTolerance = 0.3f;

        public static readonly Vector3 Up = Vector3.UnitY;

        public LadderSpan(Vector3 bottom, Vector3 facing, float height, float width)
        {
            if (!IsFinite(bottom))
            {
                throw new ArgumentOutOfRangeException(nameof(bottom), bottom, "bottom must be finite");
            }

            var flat = new Vector3(facing.X, 0f, facing.Z);
            var length = flat.Length();
            if (!IsFinite(facing) || !(length > 1e-4f))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "facing must have a finite, non-zero horizontal part");
            }

            if (!(height > 0f) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be positive and finite");
            }

            if (!(width > 0f) || float.IsInfinity(width))
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "width must be positive and finite");
            }

            Bottom = bottom;
            Facing = flat / length;
            Height = height;
            Width = width;
        }

        public Vector3 Bottom { get; }

        public Vector3 Facing { get; }

        public float Height { get; }

        public float Width { get; }

        public Vector3 Right => Vector3.Cross(Up, Facing);

        public Vector3 Top => Bottom + (Up * Height);

        public Vector3 FeetAt(float height, float radius)
        {
            return Bottom - (Facing * (radius + FaceGap)) + (Up * height);
        }

        public Vector3 TopExit(float radius)
        {
            return Top + (Facing * (radius + FaceGap));
        }

        public Vector3 LinkBottom(float clearance)
        {
            return Bottom - (Facing * clearance);
        }

        public Vector3 LinkTop(float clearance)
        {
            return Top + (Facing * clearance);
        }

        public Vector3[] ClimbPath(bool ascending, float radius)
        {
            var foot = FeetAt(0f, radius);
            var head = FeetAt(Height, radius);
            var exit = TopExit(radius);
            return ascending ? new[] { foot, head, exit } : new[] { exit, head, foot };
        }

        public Vector3 JumpOffVelocity(float speed)
        {
            return -Facing * speed;
        }

        public LadderEntry Probe(Vector3 feet, Vector3 velocity, float radius, float grabDistance, MotorSpec motor)
        {
            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            if (!IsFinite(feet) || !IsFinite(velocity) || !(radius >= 0f) || float.IsInfinity(radius)
                || !(grabDistance >= 0f) || float.IsInfinity(grabDistance))
            {
                return LadderEntry.None;
            }

            var relative = feet - Bottom;
            var along = Vector3.Dot(relative, Facing);
            var lateral = Vector3.Dot(relative, Right);
            if (Math.Abs(lateral) > Width / 2f)
            {
                return LadderEntry.None;
            }

            var horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            var speed = horizontal.Length();
            if (speed < motor.LadderMinApproachSpeed)
            {
                return LadderEntry.None;
            }

            var approach = Vector3.Dot(horizontal / speed, Facing);
            var reach = radius + grabDistance;
            var height = relative.Y;
            if (along <= 0f && along >= -reach
                && height >= -HeightTolerance && height <= Height - motor.LadderTopEntryDepth
                && approach >= motor.LadderMinApproachDot)
            {
                return LadderEntry.Front;
            }

            if (along >= 0f && along <= reach
                && Math.Abs(height - Height) <= HeightTolerance
                && -approach >= motor.LadderMinApproachDot)
            {
                return LadderEntry.Top;
            }

            return LadderEntry.None;
        }

        public float HeightOf(Vector3 feet)
        {
            return feet.Y - Bottom.Y;
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
