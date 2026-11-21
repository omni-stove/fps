using System;
using Shooter.Core;

namespace Shooter.Server
{
    // 1 つの入力で替わった動きをログに出し、着地と息切れを戦闘のモジュールへ知らせる
    internal sealed partial class ServerMovement
    {
        private void Report(Mover mover, int tick, CharacterMotorState previous, CharacterMotorState current)
        {
            var playerId = mover.Player.PlayerId;
            if (previous.IsCrouched != current.IsCrouched)
            {
                _log.WriteLine(ServerMovementLog.Stance(playerId, tick, current.IsCrouched));
            }

            if (previous.IsProne != current.IsProne)
            {
                _log.WriteLine(ServerMovementLog.Prone(playerId, tick, current.IsProne));
            }

            if (previous.IsDiving != current.IsDiving)
            {
                _log.WriteLine(ServerMovementLog.Dive(playerId, tick, current.IsDiving));
            }

            if (previous.Ladder.IsClimbing != current.Ladder.IsClimbing
                || (current.Ladder.IsClimbing && previous.Ladder.Index != current.Ladder.Index))
            {
                _log.WriteLine(ServerMovementLog.Ladder(
                    playerId, tick, current.Ladder.IsClimbing ? current.Ladder.Index : CharacterLadderState.NoLadder));
            }

            var before = previous.Cover;
            var after = current.Cover;
            if (before.Phase != after.Phase || (after.IsInCover && before.Height != after.Height))
            {
                _log.WriteLine(ServerMovementLog.Cover(playerId, tick, after.Phase, after.IsInCover ? after.Height : (CoverHeight?)null));
            }

            if (previous.IsDodging != current.IsDodging)
            {
                _log.WriteLine(ServerMovementLog.Dodge(playerId, tick, current.IsDodging));
            }

            if (previous.IsVaulting != current.IsVaulting)
            {
                _log.WriteLine(ServerMovementLog.Vault(playerId, tick, current.IsVaulting));
            }

            if (previous.IsKnockedBack && !current.IsKnockedBack)
            {
                _log.WriteLine(ServerMovementLog.KnockbackLanded(playerId, tick, current.Position));
            }

            var simulation = mover.Simulation;
            if (simulation.LastParachuteVerdict.HasValue)
            {
                _log.WriteLine(ServerMovementLog.ParachuteOpen(playerId, tick, simulation.LastParachuteVerdict.Value, simulation.LastParachuteHeight));
            }

            if (simulation.LastParachuteClose.HasValue)
            {
                _log.WriteLine(ServerMovementLog.ParachuteClosed(playerId, tick, simulation.LastParachuteClose.Value));
            }

            ReportSlide(playerId, tick, simulation);

            // 傾きは毎 tick 少しずつ変わるので、向きが替わったときだけ出す
            var leanBefore = Math.Sign(previous.Lean.Amount);
            var leanAfter = Math.Sign(current.Lean.Amount);
            if (leanBefore != leanAfter)
            {
                _log.WriteLine(ServerMovementLog.Lean(playerId, tick, leanAfter));
            }

            if (previous.Swim.IsSwimming != current.Swim.IsSwimming)
            {
                _log.WriteLine(ServerMovementLog.Swim(playerId, tick, current.Swim.IsSwimming));
            }

            if (previous.Swim.IsUnderwater != current.Swim.IsUnderwater)
            {
                _log.WriteLine(ServerMovementLog.Underwater(playerId, tick, current.Swim.IsUnderwater));
            }

            if (previous.WallRun.Side != current.WallRun.Side)
            {
                _log.WriteLine(ServerMovementLog.WallRun(playerId, tick, current.WallRun.Side));
            }

            if (current.AirJumpsUsed > previous.AirJumpsUsed)
            {
                _log.WriteLine(ServerMovementLog.AirJump(playerId, tick, current.AirJumpsUsed));
            }

            var drownings = current.Swim.DrowningsSince(previous.Swim);
            if (drownings > 0)
            {
                _log.WriteLine(ServerMovementLog.OutOfBreath(playerId, tick, drownings));
                Drowned?.Invoke(mover.Player, tick, drownings);
            }

            // 着地した tick の速度は、まだ接地中の吸着の速度に戻る前の落下速度を持っている
            if (!previous.IsGrounded && current.IsGrounded)
            {
                Landed?.Invoke(mover.Player, tick, Math.Max(0f, -current.Velocity.Y));
            }
        }
    }
}
