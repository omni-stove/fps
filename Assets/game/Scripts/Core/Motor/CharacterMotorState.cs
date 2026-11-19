using System;
using System.Numerics;

namespace Shooter.Core
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
            : this(
                position,
                velocity,
                isGrounded,
                isCrouched ? PlayerStance.Crouching : PlayerStance.Standing,
                cover,
                dodge,
                vault,
                CharacterProneState.None,
                CharacterLadderState.None)
        {
        }

        public CharacterMotorState(
            Vector3 position,
            Vector3 velocity,
            bool isGrounded,
            PlayerStance stance,
            CharacterCoverState cover,
            DodgeSnapshot dodge,
            CharacterVaultState vault,
            CharacterProneState prone,
            CharacterLadderState ladder)
            : this(position, velocity, isGrounded, stance, cover, dodge, vault, prone, ladder, CharacterKnockbackState.None)
        {
        }

        public CharacterMotorState(
            Vector3 position,
            Vector3 velocity,
            bool isGrounded,
            PlayerStance stance,
            CharacterCoverState cover,
            DodgeSnapshot dodge,
            CharacterVaultState vault,
            CharacterProneState prone,
            CharacterLadderState ladder,
            CharacterKnockbackState knockback)
            : this(
                position,
                velocity,
                isGrounded,
                stance,
                cover,
                dodge,
                vault,
                prone,
                ladder,
                knockback,
                CharacterLeanState.None,
                CharacterSwimState.None,
                CharacterWallRunState.None,
                0)
        {
        }

        public CharacterMotorState(
            Vector3 position,
            Vector3 velocity,
            bool isGrounded,
            PlayerStance stance,
            CharacterCoverState cover,
            DodgeSnapshot dodge,
            CharacterVaultState vault,
            CharacterProneState prone,
            CharacterLadderState ladder,
            CharacterKnockbackState knockback,
            CharacterLeanState lean,
            CharacterSwimState swim,
            CharacterWallRunState wallRun,
            int airJumpsUsed)
            : this(position, velocity, isGrounded, stance, cover, dodge, vault, prone, ladder, knockback, lean, swim, wallRun, airJumpsUsed, 0f)
        {
        }

        public CharacterMotorState(
            Vector3 position,
            Vector3 velocity,
            bool isGrounded,
            PlayerStance stance,
            CharacterCoverState cover,
            DodgeSnapshot dodge,
            CharacterVaultState vault,
            CharacterProneState prone,
            CharacterLadderState ladder,
            CharacterKnockbackState knockback,
            CharacterLeanState lean,
            CharacterSwimState swim,
            CharacterWallRunState wallRun,
            int airJumpsUsed,
            float jumpGraceLeft)
            : this(
                position,
                velocity,
                isGrounded,
                stance,
                cover,
                dodge,
                vault,
                prone,
                ladder,
                knockback,
                lean,
                swim,
                wallRun,
                airJumpsUsed,
                jumpGraceLeft,
                CharacterParachuteState.None)
        {
        }

        public CharacterMotorState(
            Vector3 position,
            Vector3 velocity,
            bool isGrounded,
            PlayerStance stance,
            CharacterCoverState cover,
            DodgeSnapshot dodge,
            CharacterVaultState vault,
            CharacterProneState prone,
            CharacterLadderState ladder,
            CharacterKnockbackState knockback,
            CharacterLeanState lean,
            CharacterSwimState swim,
            CharacterWallRunState wallRun,
            int airJumpsUsed,
            float jumpGraceLeft,
            CharacterParachuteState parachute)
        {
            Position = position;
            Velocity = velocity;
            IsGrounded = isGrounded;
            Stance = stance;
            Cover = cover;
            Dodge = dodge;
            Vault = vault;
            Prone = prone;
            Ladder = ladder;
            Knockback = knockback;
            Lean = lean;
            Swim = swim;
            WallRun = wallRun;
            AirJumpsUsed = airJumpsUsed;
            JumpGraceLeft = jumpGraceLeft;
            Parachute = parachute;
        }

        public Vector3 Position { get; }

        public Vector3 Velocity { get; }

        public bool IsGrounded { get; }

        public PlayerStance Stance { get; }

        public bool IsCrouched => Stance == PlayerStance.Crouching;

        public bool IsProne => Stance == PlayerStance.Prone;

        public CharacterCoverState Cover { get; }

        public DodgeSnapshot Dodge { get; }

        public CharacterVaultState Vault { get; }

        public CharacterProneState Prone { get; }

        public CharacterLadderState Ladder { get; }

        public CharacterKnockbackState Knockback { get; }

        public CharacterLeanState Lean { get; }

        public CharacterSwimState Swim { get; }

        public CharacterWallRunState WallRun { get; }

        public int AirJumpsUsed { get; }

        public float JumpGraceLeft { get; }

        public CharacterParachuteState Parachute { get; }

        public bool IsParachuting => Parachute.IsOpen;

        public bool IsInJumpGrace => JumpGraceStep.IsOpen(IsGrounded, JumpGraceLeft);

        public bool IsSwimming => Swim.IsSwimming;

        // シミュレーションは構えを持たないので、構えを止めるのはこれを読む構えを動かす側。
        public bool CanAimDownSights => !IsSwimming;

        public bool IsWallRunning => WallRun.IsRunning;

        public bool IsKnockedBack => Knockback.IsFlying;

        public bool IsDiving => Prone.IsDiving;

        public bool IsClimbing => Ladder.IsClimbing;

        public bool IsDodging => Dodge.IsRolling;

        public bool IsVaulting => Vault.IsVaulting;

        public bool IsFinite => IsFiniteVector(Position) && IsFiniteVector(Velocity) && Cover.IsValid && Dodge.IsValid && Vault.IsValid
            && Prone.IsValid && Ladder.IsValid && Knockback.IsValid && Stance >= PlayerStance.Standing && Stance <= PlayerStance.Prone
            && Lean.IsValid && Swim.IsValid && WallRun.IsValid && AirJumpsUsed >= 0
            && JumpGraceLeft >= 0f && !float.IsInfinity(JumpGraceLeft) && Parachute.IsValid;

        // 位置が一致していても姿勢が違えばカプセルの高さが違い、以降の移動も目の高さも変わるので、位置の許容値に埋もれさせず必ず巻き戻す。
        // 待ち時間の食い違いだけでは巻き戻さない (次に始められるかが変われば、そのとき転がっているかが食い違う)。
        // ジャンプの猶予の残り (JumpGraceLeft) も同じく、跳べたかが変われば位置が食い違うので比べない。
        public static float PositionError(CharacterMotorState predicted, CharacterMotorState authoritative)
        {
            if (predicted.Stance != authoritative.Stance)
            {
                return StanceMismatchError;
            }

            var mine = predicted.Cover;
            var theirs = authoritative.Cover;
            if (mine.Phase != theirs.Phase || (mine.IsInCover && mine.Height != theirs.Height))
            {
                return StanceMismatchError;
            }

            if (predicted.IsDodging != authoritative.IsDodging || predicted.IsVaulting != authoritative.IsVaulting
                || predicted.IsDiving != authoritative.IsDiving
                || predicted.IsKnockedBack != authoritative.IsKnockedBack
                || predicted.IsParachuting != authoritative.IsParachuting
                || predicted.Ladder.IsClimbing != authoritative.Ladder.IsClimbing
                || (predicted.Ladder.IsClimbing && predicted.Ladder.Index != authoritative.Ladder.Index))
            {
                return StanceMismatchError;
            }

            // 傾きの量の差は同じ入力が続けば揃うので、向きが食い違うときだけ巻き戻す (量の差まで見ると傾けている途中で毎回巻き戻す)。
            if (predicted.Swim.IsSwimming != authoritative.Swim.IsSwimming
                || predicted.WallRun.Side != authoritative.WallRun.Side
                || predicted.AirJumpsUsed != authoritative.AirJumpsUsed
                || LeanSide(predicted.Lean.Amount) != LeanSide(authoritative.Lean.Amount))
            {
                return StanceMismatchError;
            }

            return Vector3.Distance(predicted.Position, authoritative.Position);
        }

        public CharacterMotorState WithKnockback(CharacterKnockbackState knockback)
        {
            return new CharacterMotorState(
                Position, Velocity, IsGrounded, Stance, Cover, Dodge, Vault, Prone, Ladder, knockback, Lean, Swim, WallRun, AirJumpsUsed, JumpGraceLeft,
                Parachute);
        }

        public CharacterMotorState WithParachute(CharacterParachuteState parachute)
        {
            return new CharacterMotorState(
                Position, Velocity, IsGrounded, Stance, Cover, Dodge, Vault, Prone, Ladder, Knockback, Lean, Swim, WallRun, AirJumpsUsed, JumpGraceLeft,
                parachute);
        }

        public bool Equals(CharacterMotorState other)
        {
            return Position.Equals(other.Position)
                && Velocity.Equals(other.Velocity)
                && IsGrounded == other.IsGrounded
                && Stance == other.Stance
                && Cover.Equals(other.Cover)
                && Dodge.Equals(other.Dodge)
                && Vault.Equals(other.Vault)
                && Prone.Equals(other.Prone)
                && Ladder.Equals(other.Ladder)
                && Knockback.Equals(other.Knockback)
                && Lean.Equals(other.Lean)
                && Swim.Equals(other.Swim)
                && WallRun.Equals(other.WallRun)
                && AirJumpsUsed == other.AirJumpsUsed
                && JumpGraceLeft.Equals(other.JumpGraceLeft)
                && Parachute.Equals(other.Parachute);
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
                return (Position.GetHashCode() * 397) ^ Velocity.GetHashCode() ^ (IsGrounded ? 1 : 0) ^ ((int)Stance << 1)
                    ^ Cover.GetHashCode() ^ Dodge.GetHashCode() ^ Vault.GetHashCode() ^ Prone.GetHashCode() ^ Ladder.GetHashCode()
                    ^ Knockback.GetHashCode() ^ Lean.GetHashCode() ^ Swim.GetHashCode() ^ WallRun.GetHashCode() ^ (AirJumpsUsed << 4)
                    ^ JumpGraceLeft.GetHashCode() ^ (Parachute.GetHashCode() << 2);
            }
        }

        // Math.Sign は NaN で投げるので、壊れた状態を受けても比べられるよう比較で決める。
        private static int LeanSide(float amount)
        {
            return amount > 0f ? 1 : (amount < 0f ? -1 : 0);
        }

        private static bool IsFiniteVector(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
