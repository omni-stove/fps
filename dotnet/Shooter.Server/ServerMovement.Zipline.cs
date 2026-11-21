using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // ジップライン。掴めるか・どの線か・どこまで滑ったかは、クライアントの申告ではなくサーバーの知る体とマップの線 (NetMapZiplines) とルールで決める
    // (オフラインの ZiplineMovement と同じ ZiplineMotion で滑らせる)。
    // 共有の CharacterMotorSimulation に入れずにここで動かすのは、崖のぼりと同じく、状態 (CharacterMotorState) とスナップショットにジップラインの欄が無いため。
    // オフラインは Interact でも掴めるが、入力 (InputButtons) に Interact のボタンが無いので、オフラインでも掴めるジャンプで掴む。
    internal sealed partial class ServerMovement
    {
        public const string ZiplineReasonEnd = "end";
        public const string ZiplineReasonJump = "jump";
        public const string ZiplineReasonDisabled = "disabled";
        public const string ZiplineReasonDowned = "downed";
        public const string ZiplineReasonKnockback = "knockback";
        public const string ZiplineReasonInterrupted = "interrupted";

        public bool IsZiplining(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) && mover.Zipline != null;
        }

        // 掴まっている線のマップでの番号。掴まっていなければ -1
        public int ZiplineIndexOf(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) && mover.Zipline != null ? mover.ZiplineIndex : -1;
        }

        // この入力をジップラインが受け持ったら true。受け持たなければ、崖のぼりとふつうの動きに任せる。
        // 崖のぼりより先に見るのは、オフラインの ZiplineMovement が PlayerController より先にジャンプを読んで掴むため
        private bool TryZipline(Mover mover, int tick, CharacterMotorInput input)
        {
            mover.ZiplineRegrabRemaining = Math.Max(0f, mover.ZiplineRegrabRemaining - _tickSeconds);
            if (mover.Zipline != null)
            {
                var reason = KeepZiplineVerdict(mover);
                if (reason != null)
                {
                    // 速さは滑っていたときのまま残し、続きはふつうの動きに落とさせる (オフラインで横取りが外れたときと同じ)
                    ReleaseZipline(mover, tick, reason);
                    return false;
                }

                if (input.Jump)
                {
                    JumpOffZipline(mover, tick);
                    return true;
                }

                FollowZipline(mover, tick);
                return true;
            }

            if (!input.Jump || !CanGrabZipline(mover) || !TryGrabZipline(mover, tick, input.Yaw))
            {
                return false;
            }

            // 掴んだ tick のジャンプは掴むのに使ったので、跳び降りにしない (オフラインの _ignoreJump と同じ)
            FollowZipline(mover, tick);
            return true;
        }

        // 倒れた・吹き飛ばされた・ルールで切られたときは離す。離さなくてよければ null
        private string KeepZiplineVerdict(Mover mover)
        {
            if (_settings.Rules.Actions.Zipline != Zipline.On)
            {
                return ZiplineReasonDisabled;
            }

            if (mover.Downed)
            {
                return ZiplineReasonDowned;
            }

            return mover.State.IsKnockedBack ? ZiplineReasonKnockback : null;
        }

        // オフラインの CanGrab と同じく、ルールで使えて、倒れておらず、乗り物・カバー・ほかの横取り (はしご・パラシュート・崖のぼり) の最中でないときだけ掴む。
        // 回避・乗り越え・飛び込み・泳ぎ・伏せ・スライディングの最中も断るのは、オフラインはそれらを横取りの始めに打ち切るが、ここで打ち切ると状態の欄を作り直すことになり、
        // 離した後のふつうの動きが打ち切る前の続きから動いてしまうため
        private bool CanGrabZipline(Mover mover)
        {
            var state = mover.State;
            return _settings.Rules.Actions.Zipline == Zipline.On
                && _context.Map.Ziplines.Ziplines.Count > 0
                && mover.ZiplineRegrabRemaining <= 0f
                && !mover.Downed
                && !mover.Riding
                && mover.MantlePath == null
                && !state.Cover.IsInCover
                && !state.IsClimbing
                && !state.IsParachuting
                && !state.IsKnockedBack
                && !state.IsVaulting
                && !state.IsDodging
                && !state.IsDiving
                && !state.IsSwimming
                && !state.IsProne
                && !state.IsSliding;
        }

        private bool TryGrabZipline(Mover mover, int tick, float yaw)
        {
            var tuning = _settings.Zipline;
            var state = mover.State;
            var hand = HandPosition(state.Position);
            var ziplines = _context.Map.Ziplines;
            var index = ziplines.FindNearest(hand, tuning.GrabRadius);
            if (index < 0)
            {
                return false;
            }

            var motion = ziplines.Ziplines[index].CreateMotion();
            if (!motion.TryGrab(hand, state.Velocity, CharacterMotorSimulation.YawForward(yaw)))
            {
                // 線の端の際で、滑る先が残っていない
                return false;
            }

            mover.Zipline = motion;
            mover.ZiplineIndex = index;
            _log.WriteLine(ServerMovementLog.ZiplineGrab(mover.Player.PlayerId, tick, index, motion.Distance, motion.RemainingDistance, motion.Speed));
            return true;
        }

        // 毎 tick 線上の所へ置き直してから動かす。壁に引っかかっても体が線から離れたままにならない (オフラインの Step と同じ)
        private void FollowZipline(Mover mover, int tick)
        {
            var motion = mover.Zipline;
            var simulation = mover.Simulation;
            var from = HangPoint(motion);
            motion.Step(_tickSeconds);
            var to = HangPoint(motion);
            simulation.Place(Ziplined(mover.State, from, Vector3.Zero, false));
            simulation.Body.Move(to - from);
            mover.State = Ziplined(mover.State, simulation.Body.Position, motion.Velocity, simulation.Body.IsGrounded);
            if (motion.HasArrived)
            {
                ReleaseZipline(mover, tick, ZiplineReasonEnd);
            }
        }

        // 跳んで離れるときは上へは跳ばず、滑っていた勢いのまま離れる (オフラインの ReasonJump と同じ)
        private void JumpOffZipline(Mover mover, int tick)
        {
            var velocity = mover.Zipline.Velocity;
            var simulation = mover.Simulation;
            var state = mover.State;
            simulation.Place(state);
            simulation.Body.Move(velocity * _tickSeconds);
            ReleaseZipline(mover, tick, ZiplineReasonJump);
            mover.State = Ziplined(state, simulation.Body.Position, velocity, simulation.Body.IsGrounded);
        }

        // 入力によらずに置き直す口 (Teleport・Drop・Ride・マップの替わり) から呼ぶ。置き直した後に古い線をたどらないため
        private void CancelZipline(Mover mover)
        {
            if (mover.Zipline != null)
            {
                ReleaseZipline(mover, mover.LastProcessedTick, ZiplineReasonInterrupted);
            }
        }

        private void ReleaseZipline(Mover mover, int tick, string reason)
        {
            var speed = mover.Zipline?.Speed ?? 0f;
            mover.Zipline?.Release();
            mover.Zipline = null;
            mover.ZiplineIndex = -1;
            mover.ZiplineRegrabRemaining = _settings.Zipline.RegrabSeconds;
            _log.WriteLine(ServerMovementLog.ZiplineRelease(mover.Player.PlayerId, tick, reason, speed));
        }

        private Vector3 HandPosition(Vector3 bodyPosition)
        {
            return bodyPosition + (Vector3.UnitY * _settings.Zipline.HangOffset);
        }

        private Vector3 HangPoint(ZiplineMotion motion)
        {
            return motion.Position - (Vector3.UnitY * _settings.Zipline.HangOffset);
        }

        // 掴まっている間は立ったまま (オフラインの Step の crouched: false)、ほかの動き (カバー・乗り越え・はしご・壁走り) には入らない。
        // 空中ジャンプの数と猶予は CharacterMotorSimulation.Finish と同じく、床に着いたときだけ戻す
        private CharacterMotorState Ziplined(CharacterMotorState state, Vector3 position, Vector3 velocity, bool grounded)
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
