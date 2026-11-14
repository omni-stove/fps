using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 伏せの入力の解釈 (<see cref="ProneInput"/>) と飛び込み (<see cref="ProneDive"/>) の中身。実際の姿勢は <see cref="CharacterMotorState.Stance"/> が持つ。
    /// 押した瞬間・ダッシュの始まり・しゃがみの押下で伏せを解くので、前の tick のダッシュとしゃがみの入力も持つ (巻き戻しても同じ答えにするため)。
    /// </summary>
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

        /// <summary>伏せたいか (<see cref="ProneInput.IsActive"/>)。頭上が塞がっていると、false でも伏せたままのことがある。</summary>
        public bool IsActive { get; }

        public bool WasSprinting { get; }

        public bool WasCrouching { get; }

        public bool IsDiving { get; }

        /// <summary>飛び込んでいる向き (ワールドの水平の単位ベクトル、X がワールドの X、Y がワールドの Z)。</summary>
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
