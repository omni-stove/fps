using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // .NET のサーバーの移動 (ServerMovement) と同じ読み方にし、予測と答えを揃える。
    public static class SnapshotMotion
    {
        public static CharacterMotorState ToState(in PlayerSnapshot row)
        {
            return ToState(row, CharacterKnockbackState.None);
        }

        // 吹き飛ばしは持ち主にだけ送るので (OwnerSnapshot.Knockback)、行とは別に受け取る。
        // 泳ぎと壁走りは有無と向きしか届かないので、ここでは水面・息・壁の向き・走った秒数を推し量らず空にし、持ち主の予測だけが Complete で埋める。
        public static CharacterMotorState ToState(in PlayerSnapshot row, CharacterKnockbackState knockback)
        {
            var cover = row.Cover;
            var vault = row.Vault;
            var wallRunSide = (Shooter.Core.WallRunSide)(byte)row.WallRunSide;
            return new CharacterMotorState(
                row.Position,
                row.Velocity,
                row.IsGrounded,
                row.IsProne ? PlayerStance.Prone : row.IsCrouched ? PlayerStance.Crouching : PlayerStance.Standing,
                new CharacterCoverState(cover.Snapshot, cover.TopY, cover.Edges, cover.AppliedPeek),
                row.Dodge,
                vault.IsVaulting
                    ? CharacterVaultState.FromPoints(vault.Rise, vault.Cross, vault.Land, vault.Elapsed)
                    : CharacterVaultState.None,
                row.Prone,
                row.Ladder,
                knockback,
                new CharacterLeanState(row.Lean),
                row.Swimming ? new CharacterSwimState(true, 0f, false) : CharacterSwimState.None,
                wallRunSide == Shooter.Core.WallRunSide.None
                    ? CharacterWallRunState.None
                    : new CharacterWallRunState(wallRunSide, 0f, Vector3.Zero),
                row.AirJumpsUsed,
                row.JumpGraceLeft,
                ToParachute(row));
        }

        // 開いている間の速度の横はパラシュートの横の速さそのもの (ServerMovement が CharacterMotorSimulation の速度をそのまま載せる) なので、
        // 横の速さを別に送らずに速度から戻す。
        public static CharacterParachuteState ToParachute(in PlayerSnapshot row)
        {
            return row.IsParachuting
                ? CharacterParachuteState.Open(new Vector2(row.Velocity.X, row.Velocity.Z))
                : CharacterParachuteState.None;
        }

        // 巻き戻すかを決める値 (泳いでいるか・壁走りの向き・リーンの向き) を authoritative のまま変えないのは、変えると食い違いを見逃すため。
        // 走り始めを予測し損ねた壁走りの法線を空のまま返すのは、壁を調べるのに体を世界に置く必要があり、それを持つのが呼び手のため。
        public static CharacterMotorState Complete(
            CharacterMotorState authoritative, CharacterMotorState predicted, IWaterVolumes water, StanceGeometry geometry)
        {
            if (water == null)
            {
                throw new ArgumentNullException(nameof(water));
            }

            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            // 届く傾きは 1/127 刻みに丸めてあるので、向きが同じなら丸める前の予測の量を使い、やり直しで量が段になって動かないようにする
            var lean = Side(authoritative.Lean.Amount) == Side(predicted.Lean.Amount) ? predicted.Lean : authoritative.Lean;
            return new CharacterMotorState(
                authoritative.Position,
                authoritative.Velocity,
                authoritative.IsGrounded,
                authoritative.Stance,
                authoritative.Cover,
                authoritative.Dodge,
                authoritative.Vault,
                authoritative.Prone,
                authoritative.Ladder,
                authoritative.Knockback,
                lean,
                CompleteSwim(authoritative, predicted.Swim, water, geometry),
                CompleteWallRun(authoritative, predicted.WallRun),
                authoritative.AirJumpsUsed,
                authoritative.JumpGraceLeft,
                authoritative.Parachute,
                authoritative.Slide);
        }

        public static CharacterMotorState WithWallRun(CharacterMotorState state, CharacterWallRunState wallRun)
        {
            return new CharacterMotorState(
                state.Position,
                state.Velocity,
                state.IsGrounded,
                state.Stance,
                state.Cover,
                state.Dodge,
                state.Vault,
                state.Prone,
                state.Ladder,
                state.Knockback,
                state.Lean,
                state.Swim,
                wallRun,
                state.AirJumpsUsed,
                state.JumpGraceLeft,
                state.Parachute,
                state.Slide);
        }

        private static CharacterSwimState CompleteSwim(
            CharacterMotorState authoritative, CharacterSwimState predicted, IWaterVolumes water, StanceGeometry geometry)
        {
            if (!authoritative.Swim.IsSwimming)
            {
                return CharacterSwimState.None;
            }

            if (predicted.IsSwimming)
            {
                return predicted;
            }

            // SwimStep と同じく足元の水面で測る。予測が水に無かったので息はまだ使っていないものとする
            var standing = geometry.Standing;
            var position = authoritative.Position;
            var surface = water.SurfaceAt(new Vector3(position.X, position.Y + standing.BottomY, position.Z));
            var surfaceY = surface.HasValue && !float.IsNaN(surface.Value) && !float.IsInfinity(surface.Value)
                ? surface.Value
                : position.Y + SwimStep.ChestY(geometry);
            return new CharacterSwimState(true, surfaceY, position.Y + standing.EyeY < surfaceY);
        }

        private static CharacterWallRunState CompleteWallRun(CharacterMotorState authoritative, CharacterWallRunState predicted)
        {
            var side = authoritative.WallRun.Side;
            if (side != Shooter.Core.WallRunSide.None)
            {
                return predicted.Side == side ? predicted : new CharacterWallRunState(side, 0f, Vector3.Zero);
            }

            // 着地すれば前の壁を忘れる (WallRunStep.Stop) ので、空中のときだけ、同じ壁に付き直さないための前の壁を予測から引き継ぐ
            if (authoritative.IsGrounded)
            {
                return CharacterWallRunState.None;
            }

            return predicted.IsRunning ? CharacterWallRunState.LeftWall(predicted.Normal) : predicted;
        }

        private static int Side(float amount)
        {
            return amount > 0f ? 1 : (amount < 0f ? -1 : 0);
        }

        // ダウン中か・乗り越え・伏せ・はしご・ジャンプ・パラシュートを使えるか・持ち物の倍率は入力で送らないので、
        // サーバーが配る値 (downed と rules と OwnerSnapshot.CarrySpeedMultiplier) から入れ、サーバーと同じ入力にする。
        public static CharacterMotorInput ToInput(in InputFrame frame, bool downed, in MatchRulesSync rules, float carrySpeedMultiplier = 1f)
        {
            var input = new CharacterMotorInput(
                frame.Move,
                frame.Yaw,
                frame.Has(InputButtons.Sprint),
                frame.Has(InputButtons.Jump),
                frame.Has(InputButtons.Crouch),
                downed,
                frame.Has(InputButtons.Cover),
                frame.Has(InputButtons.Aim),
                frame.Has(InputButtons.Dodge),
                false,
                frame.Has(InputButtons.Prone),
                false,
                false);

            // ActionRules を組み立てずに CharacterMotorInput.WithRules と同じ絞り方をする (同期されるのは移動に効く切り替えだけのため)
            if (rules.Cover != Cover.On)
            {
                input = input.WithoutCover();
            }

            if (rules.Dodge != Dodge.On)
            {
                input = input.WithoutDodge();
            }

            // slideAllowed は入れない。actions.slide と滑りの状態はまだクライアントへ届かないので、予測で滑らせるとサーバーの答えと毎回食い違う
            return input.With(
                vaultAllowed: rules.Vault == Vault.On,
                proneAllowed: rules.Prone == Prone.On,
                ladderAllowed: rules.Ladder == Ladder.On,
                carrySpeedMultiplier: carrySpeedMultiplier,
                jumpAllowed: rules.Jump == Jump.On,
                lean: LeanInput.Combine(frame.Has(InputButtons.LeanLeft), frame.Has(InputButtons.LeanRight)),
                openParachute: frame.Has(InputButtons.OpenParachute),
                closeParachute: frame.Has(InputButtons.CloseParachute),
                parachuteAllowed: rules.Parachute == Parachute.On,
                slide: frame.Has(InputButtons.Slide));
        }

        // 近接は押している間を送る。サーバー (ServerMelee) とクライアント (ClientMelee) はどちらも押し始めた tick だけ振るので、押したままでは振り直さない
        public static InputButtons Buttons(
            bool sprint,
            bool jump,
            bool crouch,
            bool cover,
            bool aim,
            bool dodge,
            bool prone = false,
            bool leanLeft = false,
            bool leanRight = false,
            bool melee = false,
            bool openParachute = false,
            bool closeParachute = false,
            bool slide = false)
        {
            var buttons = InputButtons.None;
            buttons |= sprint ? InputButtons.Sprint : InputButtons.None;
            buttons |= jump ? InputButtons.Jump : InputButtons.None;
            buttons |= crouch ? InputButtons.Crouch : InputButtons.None;
            buttons |= cover ? InputButtons.Cover : InputButtons.None;
            buttons |= aim ? InputButtons.Aim : InputButtons.None;
            buttons |= dodge ? InputButtons.Dodge : InputButtons.None;
            buttons |= prone ? InputButtons.Prone : InputButtons.None;
            buttons |= leanLeft ? InputButtons.LeanLeft : InputButtons.None;
            buttons |= leanRight ? InputButtons.LeanRight : InputButtons.None;
            buttons |= melee ? InputButtons.Melee : InputButtons.None;
            buttons |= openParachute ? InputButtons.OpenParachute : InputButtons.None;
            buttons |= closeParachute ? InputButtons.CloseParachute : InputButtons.None;
            buttons |= slide ? InputButtons.Slide : InputButtons.None;
            return buttons;
        }

        // -π〜π に収めるのは、送る値を有限で小さく保ち、補間で遠回りしないため。
        public static float WrapYaw(float yaw)
        {
            if (float.IsNaN(yaw) || float.IsInfinity(yaw))
            {
                throw new ArgumentOutOfRangeException(nameof(yaw), yaw, "yaw must be finite");
            }

            var twoPi = 2f * MathF.PI;
            var wrapped = yaw % twoPi;
            if (wrapped > MathF.PI)
            {
                wrapped -= twoPi;
            }
            else if (wrapped < -MathF.PI)
            {
                wrapped += twoPi;
            }

            return wrapped;
        }

        public static float LerpYaw(float a, float b, float t)
        {
            var delta = WrapYaw(b - a);
            return WrapYaw(a + delta * t);
        }
    }
}
