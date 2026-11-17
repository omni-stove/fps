using System;
using System.Numerics;

namespace Shooter.Core
{
    // KnockbackFlight を持ち主ごとに持たせず状態に載せるのは、予測の巻き戻しでもサーバーと同じ飛び方から進め直せるようにするため。
    public readonly struct CharacterKnockbackState : IEquatable<CharacterKnockbackState>
    {
        private CharacterKnockbackState(Vector3 velocity, float elapsedSeconds)
        {
            IsFlying = true;
            Velocity = velocity;
            ElapsedSeconds = elapsedSeconds;
        }

        public static CharacterKnockbackState None => default;

        public bool IsFlying { get; }

        public Vector3 Velocity { get; }

        public float ElapsedSeconds { get; }

        public bool IsValid => IsFlying
            ? IsFinite(Velocity.X) && IsFinite(Velocity.Y) && IsFinite(Velocity.Z) && IsFinite(ElapsedSeconds) && ElapsedSeconds >= 0f
            : Velocity == Vector3.Zero && ElapsedSeconds == 0f;

        public static CharacterKnockbackState Flying(Vector3 velocity, float elapsedSeconds)
        {
            if (!IsFinite(velocity.X) || !IsFinite(velocity.Y) || !IsFinite(velocity.Z))
            {
                throw new ArgumentException($"velocity must be finite: {velocity}", nameof(velocity));
            }

            if (!(elapsedSeconds >= 0f) || float.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds, "elapsedSeconds must be a non-negative finite number");
            }

            return new CharacterKnockbackState(velocity, elapsedSeconds);
        }

        // KnockbackFlight.Launch と同じく置き換えずに足す。2 つ目の爆発が弱いと飛ぶ距離が縮んでしまうため。
        public CharacterKnockbackState Launch(Vector3 velocity)
        {
            return IsFlying ? Flying(Velocity + velocity, ElapsedSeconds) : Flying(velocity, 0f);
        }

        public bool Equals(CharacterKnockbackState other)
        {
            return IsFlying == other.IsFlying && Velocity.Equals(other.Velocity) && ElapsedSeconds.Equals(other.ElapsedSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterKnockbackState other && Equals(other);
        }

        public static bool operator ==(CharacterKnockbackState left, CharacterKnockbackState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterKnockbackState left, CharacterKnockbackState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((IsFlying ? 1 : 0) * 397) ^ Velocity.GetHashCode() ^ ElapsedSeconds.GetHashCode();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
