using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class StreakAim
    {
        public StreakAim(string rewardId, string displayNameKey, Vector3 origin, float headingDegrees, float range, float lineLength)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                throw new ArgumentException("rewardId must not be empty", nameof(rewardId));
            }

            RewardId = rewardId;
            DisplayNameKey = displayNameKey ?? string.Empty;
            Origin = MinimapContact.RequireFinite(origin, nameof(origin));
            HeadingDegrees = MinimapContact.NormalizeHeading(headingDegrees, nameof(headingDegrees));
            Range = SpecGuard.PositiveFinite(range, nameof(range));
            LineLength = SpecGuard.NonNegativeFinite(lineLength, nameof(lineLength));
            Target = origin;
        }

        public string RewardId { get; }

        public string DisplayNameKey { get; }

        public Vector3 Origin { get; }

        public float Range { get; }

        public float LineLength { get; }

        public Vector3 Target { get; private set; }

        public float HeadingDegrees { get; private set; }

        public StreakTargetingStatus Status =>
            new StreakTargetingStatus(RewardId, DisplayNameKey, Origin, Target, HeadingDegrees, Range, LineLength);

        // 地図は北 (+Z) を上に固定して見せるので、入力の x を東 (+X)、y を北 (+Z) へそのまま動かす。
        public void Move(Vector2 input, float speed, float deltaSeconds)
        {
            if (!IsFinite(input.X) || !IsFinite(input.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(input), input, "input must be finite");
            }

            SpecGuard.NonNegativeFinite(speed, nameof(speed));
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));

            // 斜めに倒したときだけ速くならないよう、長さを 1 までに抑える。
            if (input.LengthSquared() > 1f)
            {
                input = Vector2.Normalize(input);
            }

            var step = input * (speed * deltaSeconds);
            Place(new Vector3(Target.X + step.X, Target.Y, Target.Z + step.Y));
        }

        public void Place(Vector3 target)
        {
            MinimapContact.RequireFinite(target, nameof(target));
            var offset = new Vector2(target.X - Origin.X, target.Z - Origin.Z);
            var distance = offset.Length();
            if (distance > Range)
            {
                offset *= Range / distance;
            }

            Target = new Vector3(Origin.X + offset.X, target.Y, Origin.Z + offset.Y);
        }

        public void Turn(float degrees)
        {
            if (!IsFinite(degrees))
            {
                throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "degrees must be finite");
            }

            HeadingDegrees = MinimapContact.NormalizeHeading(HeadingDegrees + degrees, nameof(degrees));
        }

        public void SetGroundHeight(float y)
        {
            if (!IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "y must be finite");
            }

            Target = new Vector3(Target.X, y, Target.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
