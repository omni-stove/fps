using System;

namespace Shooter.Core
{
    public readonly struct CharacterLeanState : IEquatable<CharacterLeanState>
    {
        public CharacterLeanState(float amount)
        {
            Amount = amount;
        }

        public static CharacterLeanState None => default;

        public float Amount { get; }

        public bool IsLeaning => Amount != 0f;

        public bool IsValid => !float.IsNaN(Amount) && Amount >= -1f && Amount <= 1f;

        public bool Equals(CharacterLeanState other)
        {
            return Amount.Equals(other.Amount);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterLeanState other && Equals(other);
        }

        public static bool operator ==(CharacterLeanState left, CharacterLeanState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterLeanState left, CharacterLeanState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return Amount.GetHashCode();
        }
    }
}
