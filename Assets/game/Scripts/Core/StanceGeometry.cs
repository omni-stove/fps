using System;

namespace Shooter.Core
{
    public sealed class StanceGeometry
    {
        public StanceShape Standing { get; }

        public StanceShape Crouched { get; }

        public StanceShape Prone { get; }

        public float Radius { get; }

        public StanceGeometry(float standingHeight, float standingCenterY, float standingEyeY, float crouchHeight, float radius)
            : this(standingHeight, standingCenterY, standingEyeY, crouchHeight, crouchHeight, radius)
        {
        }

        public StanceGeometry(
            float standingHeight, float standingCenterY, float standingEyeY, float crouchHeight, float proneHeight, float radius)
        {
            if (!(standingHeight > 0f) || float.IsInfinity(standingHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(standingHeight), standingHeight, "standingHeight must be finite and positive");
            }

            if (!IsFinite(standingCenterY))
            {
                throw new ArgumentOutOfRangeException(nameof(standingCenterY), standingCenterY, "standingCenterY must be finite");
            }

            if (!IsFinite(standingEyeY))
            {
                throw new ArgumentOutOfRangeException(nameof(standingEyeY), standingEyeY, "standingEyeY must be finite");
            }

            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be finite and not negative");
            }

            // 半径の 2 倍より低いと CharacterController は球として扱い、足元を保てなくなる。
            if (!(crouchHeight > 0f && crouchHeight <= standingHeight && crouchHeight >= radius * 2f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(crouchHeight),
                    crouchHeight,
                    $"crouchHeight must be positive, at least twice the radius ({radius * 2f}) and not above standingHeight ({standingHeight})");
            }

            // 伏せから立つときにしゃがみを経て確かめるので、しゃがみより高い伏せは認めない。
            if (!(proneHeight > 0f && proneHeight <= crouchHeight && proneHeight >= radius * 2f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(proneHeight),
                    proneHeight,
                    $"proneHeight must be positive, at least twice the radius ({radius * 2f}) and not above crouchHeight ({crouchHeight})");
            }

            Radius = radius;
            Standing = new StanceShape(standingHeight, standingCenterY, standingEyeY);
            Crouched = Lowered(Standing, crouchHeight);
            Prone = Lowered(Standing, proneHeight);
        }

        public StanceShape Shape(bool crouched)
        {
            return crouched ? Crouched : Standing;
        }

        public StanceShape Shape(PlayerStance stance)
        {
            switch (stance)
            {
                case PlayerStance.Standing:
                    return Standing;
                case PlayerStance.Crouching:
                    return Crouched;
                case PlayerStance.Prone:
                    return Prone;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stance), stance, "stance is not a defined PlayerStance");
            }
        }

        public void SphereCenters(StanceShape shape, out float bottomY, out float topY)
        {
            var half = Math.Max((shape.Height * 0.5f) - Radius, 0f);
            bottomY = shape.CenterY - half;
            topY = shape.CenterY + half;
        }

        private static StanceShape Lowered(StanceShape standing, float height)
        {
            var drop = standing.Height - height;
            var feetY = standing.BottomY;

            // 目が足元より下へ行かないようにする。ただし元から足元より下に置かれた目はそのまま。
            var eyeY = Math.Max(standing.EyeY - drop, Math.Min(standing.EyeY, feetY));
            return new StanceShape(height, feetY + (height * 0.5f), eyeY);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
