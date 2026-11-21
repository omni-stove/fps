using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 崖のぼり。始めてよいかと上る道は、クライアントの申告ではなくサーバーの知る体とルールで決める (オフラインの PlayerController.Mantle と同じ判定)。
    // 共有の CharacterMotorSimulation に入れずにここで動かすのは、状態 (CharacterMotorState) とスナップショットに崖のぼりの欄が無く、
    // 乗り越えの欄 (VaultSync) に載せると、クライアントが乗り越えの速さでたどり直して食い違うため。
    internal sealed partial class ServerMovement
    {
        public bool IsMantling(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) && mover.MantlePath != null;
        }

        // この入力を崖のぼりが受け持ったら true。受け持たなければ、ふつうの動き (乗り越え・ジャンプを含む) に任せる
        private bool TryMantle(Mover mover, int tick, CharacterMotorInput input)
        {
            if (mover.MantlePath != null && !CanKeepMantling(mover))
            {
                EndMantle(mover, tick);
                mover.State = Mantled(mover.State, mover.State.Position, Vector3.Zero, mover.State.IsGrounded);
            }

            if (mover.MantlePath == null && !(input.Jump && CanStartMantle(mover) && TryStartMantle(mover, tick, input.Yaw)))
            {
                return false;
            }

            FollowMantle(mover, tick);
            return true;
        }

        // 倒れた・吹き飛ばされた・ルールで切られたときはやめる (オフラインの MantleRule の setter と移動の横取りと同じ)
        private bool CanKeepMantling(Mover mover)
        {
            return _settings.Rules.Actions.Mantle == Mantle.On && !mover.Downed && !mover.State.IsKnockedBack;
        }

        // オフラインの StartMantleIfAble と同じく、立って床にいて、ほかの動きの最中でないときだけ始める
        private bool CanStartMantle(Mover mover)
        {
            var state = mover.State;
            return _settings.Rules.Actions.Mantle == Mantle.On
                && !mover.Downed
                && state.IsGrounded
                && state.Stance == PlayerStance.Standing
                && !state.IsVaulting
                && !state.IsDodging
                && !state.IsDiving
                && !state.IsClimbing
                && !state.IsKnockedBack
                && !state.IsSwimming
                && !state.IsParachuting
                && !state.IsWallRunning
                && !state.Cover.IsInCover;
        }

        private bool TryStartMantle(Mover mover, int tick, float yaw)
        {
            var simulation = mover.Simulation;
            var state = mover.State;
            simulation.Place(state);

            // オフラインでは乗り越えを先に試すので、乗り越えられる段は崖のぼりにせずふつうの動きへ回す
            if (_settings.Rules.Actions.Vault == Vault.On && simulation.VaultSensor.Probe(state.Position, yaw, simulation.Spec).CanVault)
            {
                return false;
            }

            var result = simulation.VaultSensor.ProbeMantle(state.Position, yaw, simulation.Spec);
            var playerId = mover.Player.PlayerId;
            if (result.Verdict != VaultVerdict.NoObstacle)
            {
                _log.WriteLine(ServerMovementLog.MantleProbe(playerId, tick, result.Verdict, result.Height, result.Depth));
            }

            if (!result.CanMantle)
            {
                return false;
            }

            mover.MantlePath = result.Path;
            mover.MantleElapsed = 0f;
            _log.WriteLine(ServerMovementLog.Mantle(playerId, tick, true));
            return true;
        }

        private void FollowMantle(Mover mover, int tick)
        {
            var path = mover.MantlePath;
            var simulation = mover.Simulation;
            var state = mover.State;
            var before = mover.MantleElapsed;
            var after = Math.Min(path.Duration, before + _tickSeconds);
            var displacement = path.Displacement(before, after);
            simulation.Place(state);
            simulation.Body.Move(displacement);
            mover.MantleElapsed = after;

            // 上がる勢いを持ち越すと縁の上で跳ね上がるので、上り終えたら速度を 0 にする (オフラインの FinishMantle と同じ)
            var done = after >= path.Duration;
            var velocity = done ? Vector3.Zero : displacement / _tickSeconds;
            mover.State = Mantled(state, simulation.Body.Position, velocity, simulation.Body.IsGrounded);
            if (done)
            {
                EndMantle(mover, tick);
            }
        }

        // 入力によらずに置き直す口 (Teleport・Drop・Ride・マップの替わり) から呼ぶ。置き直した後の道を古い位置からたどらないため
        private void CancelMantle(Mover mover)
        {
            if (mover.MantlePath != null)
            {
                EndMantle(mover, mover.LastProcessedTick);
            }
        }

        private void EndMantle(Mover mover, int tick)
        {
            mover.MantlePath = null;
            mover.MantleElapsed = 0f;
            _log.WriteLine(ServerMovementLog.Mantle(mover.Player.PlayerId, tick, false));
        }

        // 上っている間は立ったまま、ほかの動き (カバー・乗り越え・はしご・壁走り) には入らない。
        // 空中ジャンプの数と猶予は CharacterMotorSimulation.Finish と同じく、床に着いたときだけ戻す
        private CharacterMotorState Mantled(CharacterMotorState state, Vector3 position, Vector3 velocity, bool grounded)
        {
            return new CharacterMotorState(
                position,
                velocity,
                grounded,
                PlayerStance.Standing,
                CharacterCoverState.Out,
                state.Dodge,
                CharacterVaultState.None,
                state.Prone,
                CharacterLadderState.None,
                state.Knockback,
                state.Lean,
                state.Swim,
                CharacterWallRunState.None,
                grounded ? 0 : state.AirJumpsUsed,
                grounded ? _settings.Motor.JumpGraceSeconds : 0f,
                state.Parachute);
        }
    }
}
