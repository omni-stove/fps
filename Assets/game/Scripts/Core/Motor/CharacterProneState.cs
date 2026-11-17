using System;
using System.Numerics;

namespace Shooter.Core
{
    // 押した瞬間・ダッシュの始まり・しゃがみの押下で伏せを解くので、巻き戻しても同じ答えにするため前の tick のダッシュとしゃがみの入力も持つ。
    public readonly struct CharacterProneState : IEquatable<CharacterProneState>
    {
        public CharacterProneState(
            bool isActive, bool wasSprinting, bool wasCrouching, bool isDiving, Vector2 diveDirection, float diveElapsed)
        {
            IsActive = isActive;
            WasSprinting = wasSprinting;
            WasCrouching = wasCrouching;
            IsDiving = isDiving;
            DiveDirection = isDiving ? diveDirection : Vector2.Zero;
            DiveElapsed = isDiving ? diveElapsed : 0f;
        }

        public static CharacterProneState None => default;

        public bool IsActive { get; }

        public bool WasSprinting { get; }

        public bool WasCrouching { get; }

        public bool IsDiving { get; }

        public Vector2 DiveDirection { get; }

        public float DiveElapsed { get; }

        public bool IsValid => IsFinite(DiveDirection.X) && IsFinite(DiveDirection.Y) && IsFinite(DiveElapsed) && DiveElapsed >= 0f
            && (!IsDiving || DiveDirection.LengthSquared() > 0f);

        public bool Equals(CharacterProneState other)
        {
            return IsActive == other.IsActive
                && WasSprinting == other.WasSprinting
                && WasCrouching == other.WasCrouching
                && IsDiving == other.IsDiving
                && DiveDirection.Equals(other.DiveDirection)
                && DiveElapsed.Equals(other.DiveElapsed);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterProneState other && Equals(other);
        }

        public static bool operator ==(CharacterProneState left, CharacterProneState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterProneState left, CharacterProneState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var flags = (IsActive ? 1 : 0) | (WasSprinting ? 2 : 0) | (WasCrouching ? 4 : 0) | (IsDiving ? 8 : 0);
                return (flags * 397) ^ DiveDirection.GetHashCode() ^ DiveElapsed.GetHashCode();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
