using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 体の今の状態を、他のモジュール (問い合わせ・ダメージの量) とスナップショット・共有の体へ渡す
    internal sealed partial class ServerMovement
    {
        public bool TryGetState(int playerId, out CharacterMotorState state)
        {
            if (_byPlayer.TryGetValue(playerId, out var mover))
            {
                state = mover.State;
                return true;
            }

            state = default;
            return false;
        }

        public bool IsAiming(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) && mover.Aiming;
        }

        // 走るかはクライアントの SprintInput が決めて押したボタンで届くので、最後に当てた入力のボタンをそのまま返す
        public bool IsSprinting(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) && mover.Sprinting;
        }

        public int LastProcessedInputTick(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) ? mover.LastProcessedTick : -1;
        }

        public int FallDamage(float fallSpeed, int maxHealth)
        {
            return _settings.Rules.FallDamage == Shooter.Core.FallDamage.On
                ? FallDamageCalculator.Calculate(fallSpeed, _settings.Motor, maxHealth)
                : 0;
        }

        public int DrownDamage(int drownings)
        {
            if (drownings < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(drownings), drownings, "drownings must not be negative");
            }

            return (int)Math.Min(int.MaxValue, (long)drownings * _settings.Swim.DrownDamage);
        }

        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(receiver);
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (!_byPlayer.TryGetValue(row.PlayerId, out var mover))
                {
                    continue;
                }

                var state = mover.State;
                row.Position = state.Position;
                row.Velocity = state.Velocity;
                row.Yaw = mover.Yaw;
                row.IsGrounded = state.IsGrounded;
                row.IsCrouched = state.IsCrouched;
                row.IsProne = state.IsProne;
                row.IsAiming = mover.Aiming;
                row.Cover = new CoverSync(state.Cover.Snapshot, state.Cover.TopY, state.Cover.Edges, state.Cover.AppliedPeek);
                row.Dodge = state.Dodge;
                row.Vault = state.Vault.IsVaulting
                    ? new VaultSync(true, state.Vault.Rise, state.Vault.Cross, state.Vault.Land, state.Vault.Elapsed)
                    : VaultSync.None;
                row.Prone = state.Prone;
                row.Ladder = state.Ladder;
                row.Lean = state.Lean.Amount;
                row.Swimming = state.Swim.IsSwimming;
                row.WallRunSide = (Shooter.Protocol.WallRunSide)(byte)state.WallRun.Side;
                row.AirJumpsUsed = (byte)Math.Min(byte.MaxValue, state.AirJumpsUsed);
                row.JumpGraceLeft = state.JumpGraceLeft;
                row.IsParachuting = state.IsParachuting;
            }

            if (_byPlayer.TryGetValue(receiver.PlayerId, out var own))
            {
                snapshot.Owner.LastProcessedInputTick = own.LastProcessedTick;
                snapshot.Owner.CarrySpeedMultiplier = CarrySpeedMultiplier(own);
                snapshot.Owner.Knockback = own.State.Knockback;
            }
        }

        // 位置の持ち主はここなので、ルール・戦闘・Bot が同じ位置で判定できるよう、動かすたびに共有の体へ写す
        private void Publish(Mover mover)
        {
            if (_context.Bodies.TryGet(mover.Player.PlayerId, out var body))
            {
                var state = mover.State;
                body.SetPose(
                    state.Position, mover.Yaw, state.Stance, state.Ladder.IsClimbing ? state.Ladder.Index : CharacterLadderState.NoLadder, state.Lean.Amount);
            }
        }
    }
}
