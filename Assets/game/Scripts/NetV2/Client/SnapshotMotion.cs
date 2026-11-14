using System;
using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
{
    /// <summary>
    /// 通信の形 (<see cref="PlayerSnapshot"/>・<see cref="InputFrame"/>) と Core の移動 (<see cref="CharacterMotorState"/>・
    /// <see cref="CharacterMotorInput"/>) の間の変換。.NET のサーバーの移動 (<c>ServerMovement</c>) と同じ読み方にし、予測と答えを揃える。
    /// </summary>
    public static class SnapshotMotion
    {
        /// <summary>行の移動の状態。サーバーはこの値を、その人の最後に処理した入力の後の状態として送る。</summary>
        public static CharacterMotorState ToState(in PlayerSnapshot row)
        {
            var cover = row.Cover;
            var vault = row.Vault;
            return new CharacterMotorState(
                row.Position,
                row.Velocity,
                row.IsGrounded,
                row.IsCrouched,
                new CharacterCoverState(cover.Snapshot, cover.TopY, cover.Edges, cover.AppliedPeek),
                row.Dodge,
                vault.IsVaulting
                    ? CharacterVaultState.FromPoints(vault.Rise, vault.Cross, vault.Land, vault.Elapsed)
                    : CharacterVaultState.None);
        }

        /// <summary>
        /// サーバーが frame を動かすときと同じ入力。ダウン中か・乗り越えてよいかは送らないので、サーバーが配る値 (downed と rules) から入れる。
        /// </summary>
        public static CharacterMotorInput ToInput(in InputFrame frame, bool downed, in MatchRulesSync rules)
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
                false);

            // ActionRules を組み立てずに CharacterMotorInput.WithRules と同じ絞り方をする (同期されるのはこの 3 つの切り替えだけのため)
            if (rules.Cover != Cover.On)
            {
                input = input.WithoutCover();
            }

            if (rules.Dodge != Dodge.On)
            {
                input = input.WithoutDodge();
            }

            return input.WithVaultAllowed(rules.Vault == Vault.On);
        }

        /// <summary>押したボタンを送る形にまとめる。</summary>
        public static InputButtons Buttons(bool sprint, bool jump, bool crouch, bool cover, bool aim, bool dodge)
        {
            var buttons = InputButtons.None;
            buttons |= sprint ? InputButtons.Sprint : InputButtons.None;
            buttons |= jump ? InputButtons.Jump : InputButtons.None;
            buttons |= crouch ? InputButtons.Crouch : InputButtons.None;
            buttons |= cover ? InputButtons.Cover : InputButtons.None;
            buttons |= aim ? InputButtons.Aim : InputButtons.None;
            buttons |= dodge ? InputButtons.Dodge : InputButtons.None;
            return buttons;
        }

        /// <summary>yaw (ラジアン) を -π〜π に収める。送る値を有限で小さく保ち、補間で遠回りしないため。</summary>
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

        /// <summary>a から b へ t (0〜1) だけ、短い向きに回した yaw (ラジアン)。</summary>
        public static float LerpYaw(float a, float b, float t)
        {
            var delta = WrapYaw(b - a);
            return WrapYaw(a + delta * t);
        }
    }
}
