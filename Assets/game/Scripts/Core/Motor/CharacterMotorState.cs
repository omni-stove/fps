using System;
using System.Numerics;

namespace Fps.Core
{
    public readonly struct CharacterMotorState : IEquatable<CharacterMotorState>
    {
        public const float StanceMismatchError = float.PositiveInfinity;

        public CharacterMotorState(Vector3 position, Vector3 velocity, bool isGrounded, bool isCrouched)
            : this(position, velocity, isGrounded, isCrouched, CharacterCoverState.Out, DodgeSnapshot.Ready, CharacterVaultState.None)
        {
        }

        public CharacterMotorState(
            Vector3 position,
            Vector3 velocity,
            bool isGrounded,
            bool isCrouched,
            CharacterCoverState cover,
            DodgeSnapshot dodge,
            CharacterVaultState vault)
        {
            Position = position;
            Velocity = velocity;
            IsGrounded = isGrounded;
            IsCrouched = isCrouched;
            Cover = cover;
            Dodge = dodge;
            Vault = vault;
        }

        public Vector3 Position { get; }

        public Vector3 Velocity { get; }

        public bool IsGrounded { get; }

        public bool IsCrouched { get; }

        public CharacterCoverState Cover { get; }

        public DodgeSnapshot Dodge { get; }

        public CharacterVaultState Vault { get; }

        public bool IsDodging => Dodge.IsRolling;

        public bool IsVaulting => Vault.IsVaulting;

        public bool IsFinite => IsFiniteVector(Position) && IsFiniteVector(Velocity) && Cover.IsValid && Dodge.IsValid && Vault.IsValid;

        /// <summary>
        /// 位置が一致していても姿勢が違えばカプセルの高さが違い、以降の移動も目の高さも変わるので、位置の許容値に埋もれさせず必ず巻き戻す。
        /// 待ち時間の食い違いだけでは巻き戻さない (次に始められるかが変われば、そのとき転がっているかが食い違う)。
        /// </summary>
        public static float PositionError(CharacterMotorState predicted, CharacterMotorState authoritative)
        {
            if (predicted.IsCrouched != authoritative.IsCrouched)
            {
                return StanceMismatchError;
            }

            var mine = predicted.Cover;
            var theirs = authoritative.Cover;
            if (mine.Phase != theirs.Phase || (mine.IsInCover && mine.Height != theirs.Height))
            {
                return StanceMismatchError;
            }

            if (predicted.IsDodging != authoritative.IsDodging || predicted.IsVaulting != authoritative.IsVaulting)
            {
                return StanceMismatchError;
            }

            return Vector3.Distance(predicted.Position, authoritative.Position);
        }

        public bool Equals(CharacterMotorState other)
        {
            return Position.Equals(other.Position)
                && Velocity.Equals(other.Velocity)
                && IsGrounded == other.IsGrounded
                && IsCrouched == other.IsCrouched
                && Cover.Equals(other.Cover)
                && Dodge.Equals(other.Dodge)
                && Vault.Equals(other.Vault);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterMotorState other && Equals(other);
        }

        public static bool operator ==(CharacterMotorState left, CharacterMotorState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterMotorState left, CharacterMotorState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Position.GetHashCode() * 397) ^ Velocity.GetHashCode() ^ (IsGrounded ? 1 : 0) ^ (IsCrouched ? 2 : 0)
                    ^ Cover.GetHashCode() ^ Dodge.GetHashCode() ^ Vault.GetHashCode();
            }
        }

        private static bool IsFiniteVector(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
