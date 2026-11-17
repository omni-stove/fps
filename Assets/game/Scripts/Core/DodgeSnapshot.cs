using System;
using System.Numerics;

namespace Shooter.Core
{
    // ネット対戦の予測で過去の tick へ巻き戻して同じ入力を再シミュレーションするために、転がりの進みと待ち時間を含める。
    public readonly struct DodgeSnapshot : IEquatable<DodgeSnapshot>
    {
        public DodgeSnapshot(bool isRolling, Vector2 direction, float elapsed, float cooldownRemaining)
        {
            IsRolling = isRolling;
            Direction = direction;
            Elapsed = elapsed;
            CooldownRemaining = cooldownRemaining;
        }

        public static DodgeSnapshot Ready => default;

        public bool IsRolling { get; }

        public Vector2 Direction { get; }

        public float Elapsed { get; }

        public float CooldownRemaining { get; }

        public bool IsValid
        {
            get
            {
                if (!IsFinite(Direction.X) || !IsFinite(Direction.Y) || !IsFinite(Elapsed) || !IsFinite(CooldownRemaining))
                {
                    return false;
                }

                if (Elapsed < 0f || CooldownRemaining < 0f)
                {
                    return false;
                }

                return !IsRolling || Direction.LengthSquared() > 0f;
            }
        }

        public bool Equals(DodgeSnapshot other)
        {
            return IsRolling == other.IsRolling
                && Direction.Equals(other.Direction)
                && Elapsed.Equals(other.Elapsed)
                && CooldownRemaining.Equals(other.CooldownRemaining);
        }

        public override bool Equals(object obj)
        {
            return obj is DodgeSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsRolling, Direction, Elapsed, CooldownRemaining);
        }

        public static bool operator ==(DodgeSnapshot left, DodgeSnapshot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DodgeSnapshot left, DodgeSnapshot right)
        {
            return !left.Equals(right);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
