using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // グラップルの縄を掛ける先と引き寄せは、オフライン (GrappleAbility・GrappleMovement) と同じ Core (GrappleTargeting・GrapplePull) で
    // サーバーが決め、動かした位置をスナップショットで皆に送る。持ち主の申告は向き (yaw・pitch) とボタンだけを使う。
    // 崖のぼりはサーバーがまだ扱わない (ServerUnsupportedActions の mantle) ので、壁に着いたら縁で止まり、跳ぶか時間で離す。
    internal sealed partial class ServerMovement
    {
        public const string GrappleFileName = "grapple.json";

        private GrappleType _grapple;
        private Func<int, bool> _hasGrapple;

        public GrappleType Grapple => _grapple;

        // grapple.json が無ければ縄を掛けさせない。読めないファイルは既定値にせず止める (置ける物の一覧と同じ)
        public static bool TryLoadGrapple(IReadOnlyList<string> searchRoots, out GrappleType grapple, out string path, out string error)
        {
            ArgumentNullException.ThrowIfNull(searchRoots);
            grapple = null;
            path = null;
            foreach (var root in searchRoots)
            {
                var candidate = Path.GetFullPath(
                    Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, GrappleFileName));
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    grapple = GrappleJson.Parse(File.ReadAllText(candidate), candidate);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    grapple = null;
                    error = "cannot read grapple tuning: " + e.Message;
                    return false;
                }

                path = candidate;
                error = null;
                return true;
            }

            error = null;
            return true;
        }

        // 持っている能力は能力の選択 (ServerAbilityChoices) が知るので、その問い合わせを受け取る。掛けていた縄は外し、回数は満たし直す
        public void UseGrapple(GrappleType grapple, Func<int, bool> hasGrapple)
        {
            _grapple = grapple ?? throw new ArgumentNullException(nameof(grapple));
            _hasGrapple = hasGrapple ?? throw new ArgumentNullException(nameof(hasGrapple));
            foreach (var mover in _byPlayer.Values)
            {
                ReleaseGrapple(mover, GrappleEnd.Interrupted);
                mover.GrappleSlot = null;
            }
        }

        public bool IsGrappling(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) && mover.Hook != null;
        }

        public bool IsHanging(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) && mover.Hook != null && mover.Hook.Hanging;
        }

        public bool TryGetGrappleCharges(int playerId, out int charges)
        {
            charges = 0;
            if (_grapple == null || !_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            charges = mover.GrappleSlot != null ? mover.GrappleSlot.Charges : _grapple.Ability.MaxCharges;
            return true;
        }

        // オフラインの AbilityHost が出現のたびに枠を満たすのと同じく、出現し直した人は回数を満たして始める。
        // 体の置き直し (Teleported) で満たさないのは、乗り物を降りるときも置き直しになり、降りるたびに満ちてしまうため
        public bool RefillGrapple(int playerId)
        {
            if (!_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            ReleaseGrapple(mover, GrappleEnd.Interrupted);
            mover.GrappleSlot = null;
            return true;
        }

        private void TickGrappleSlots(float deltaSeconds)
        {
            if (_grapple == null || !(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            foreach (var mover in _byPlayer.Values)
            {
                mover.GrappleSlot?.Tick(deltaSeconds);
            }
        }

        // 縄がこの入力で体を動かしたら true を返し、ふつうの移動 (CharacterMotorSimulation.Step) を飛ばさせる
        private bool TryStepGrapple(Mover mover, InputFrame frame, CharacterMotorState previous, out CharacterMotorState current)
        {
            var grapplePressed = frame.Has(InputButtons.Grapple) && !mover.GrappleHeld;
            var jumpPressed = frame.Has(InputButtons.Jump) && !mover.JumpHeld;
            mover.GrappleHeld = frame.Has(InputButtons.Grapple);
            mover.JumpHeld = frame.Has(InputButtons.Jump);
            current = previous;

            if (mover.Hook != null)
            {
                var refusal = Refusal(mover, false);
                if (refusal != ServerGrappleRefusal.None)
                {
                    ReleaseGrapple(mover, GrappleEnd.Interrupted, frame.Tick);
                    return false;
                }

                current = ContinueGrapple(mover, frame.Tick, previous, jumpPressed);
                return true;
            }

            if (!grapplePressed || _grapple == null)
            {
                return false;
            }

            if (!TryAttach(mover, frame, previous))
            {
                return false;
            }

            current = ContinueGrapple(mover, frame.Tick, previous, false);
            return true;
        }

        private bool TryAttach(Mover mover, InputFrame frame, CharacterMotorState previous)
        {
            var playerId = mover.Player.PlayerId;
            var refusal = Refusal(mover, true);
            if (refusal != ServerGrappleRefusal.None)
            {
                Warn(mover, ServerGrappleLog.Refused(playerId, frame.Tick, refusal));
                return false;
            }

            var simulation = mover.Simulation;
            simulation.Place(previous);
            var body = simulation.Body;
            var origin = body.EyePosition(previous.Position);
            var direction = GrappleTargeting.Direction(frame.Yaw, frame.Pitch);
            var miss = GrappleTargeting.Aim(body.World, _grapple, ServerHitboxes.RaycastLayers, origin, direction, out var target);
            if (miss != GrappleMiss.None)
            {
                // オフラインと同じく、外れた縄は回数を使わない
                Warn(mover, ServerGrappleLog.Miss(playerId, frame.Tick, miss, target));
                return false;
            }

            var slot = SlotOf(mover);
            slot.TryStart();
            slot.End();

            var hook = new GrappleHook(target, HangPosition(target), new GrapplePull(_grapple.Spec));
            hook.Pull.Start(previous.Position, previous.Velocity, hook.HangPosition);
            mover.Hook = hook;
            _log.WriteLine(ServerGrappleLog.Attached(playerId, frame.Tick, target));
            return true;
        }

        private ServerGrappleRefusal Refusal(Mover mover, bool starting)
        {
            if (_grapple == null || _settings.Rules.Actions.Abilities != Abilities.On)
            {
                return ServerGrappleRefusal.RuleOff;
            }

            if (mover.Downed || mover.Riding)
            {
                return ServerGrappleRefusal.CannotAct;
            }

            if (_hasGrapple == null || !_hasGrapple(mover.Player.PlayerId))
            {
                return ServerGrappleRefusal.NotEquipped;
            }

            if (!starting)
            {
                return ServerGrappleRefusal.None;
            }

            var slot = SlotOf(mover);
            if (slot.CanStart)
            {
                return ServerGrappleRefusal.None;
            }

            return slot.Block == AbilityUseBlock.Cooldown && slot.Charges > 0 ? ServerGrappleRefusal.CoolingDown : ServerGrappleRefusal.NoCharges;
        }

        private AbilitySlot SlotOf(Mover mover)
        {
            if (mover.GrappleSlot == null || mover.GrappleSlot.Spec != _grapple.Ability)
            {
                mover.GrappleSlot = new AbilitySlot(_grapple.Ability);
            }

            return mover.GrappleSlot;
        }

        // 体の位置 (CharacterMotorState.Position) は当たりの形の中心から CenterY だけ下にあるので、止まる中心 (GrappleSpec.HangPoint) から引く
        private Vector3 HangPosition(GrappleTarget target)
        {
            var standing = _settings.Stance.Standing;
            var center = _grapple.Spec.HangPoint(target.Point, target.Normal, _settings.Stance.Radius, standing.Height * 0.5f);
            return center - new Vector3(0f, standing.CenterY, 0f);
        }

        private CharacterMotorState ContinueGrapple(Mover mover, int tick, CharacterMotorState previous, bool jumpPressed)
        {
            var hook = mover.Hook;
            var simulation = mover.Simulation;

            // 縄に引かれている間は立った形で動かす (オフラインの GrappleMovement も伏せ・しゃがみを解く)
            var standing = new CharacterMotorState(previous.Position, previous.Velocity, false, false);
            simulation.Place(standing);
            var body = simulation.Body;

            if (hook.Hanging)
            {
                if (jumpPressed)
                {
                    var hop = Vector3.UnitY * MathF.Sqrt(2f * _settings.Motor.Gravity * _settings.Motor.JumpHeight);
                    ReleaseGrapple(mover, GrappleEnd.Jumped, tick, hop.Length());
                    return new CharacterMotorState(previous.Position, hop, false, false);
                }

                hook.HangRemaining -= _tickSeconds;
                if (hook.HangRemaining <= 0f)
                {
                    ReleaseGrapple(mover, GrappleEnd.Dropped, tick);
                }

                return new CharacterMotorState(previous.Position, Vector3.Zero, false, false);
            }

            var pull = hook.Pull;
            if (jumpPressed)
            {
                var momentum = pull.Release(GrappleEnd.Jumped);
                ReleaseGrapple(mover, GrappleEnd.Jumped, tick, momentum.Length());
                return new CharacterMotorState(previous.Position, momentum, false, false);
            }

            var displacement = pull.Step(previous.Position, hook.HangPosition, _tickSeconds);
            body.Move(displacement);
            var position = body.Position;
            if (pull.IsPulling)
            {
                return new CharacterMotorState(position, pull.Velocity, false, false);
            }

            if (pull.Ended != GrappleEnd.Arrived)
            {
                ReleaseGrapple(mover, pull.Ended, tick);
                return new CharacterMotorState(position, Vector3.Zero, false, false);
            }

            var wall = hook.Target.IsWall;
            _log.WriteLine(ServerGrappleLog.Arrived(mover.Player.PlayerId, tick, pull.ElapsedSeconds, wall));

            // 床や段の上に掛けたなら、着いた所にそのまま立つ。止まって待つのは壁と天井だけ (オフラインと同じ)
            if (hook.Target.IsFloor || !(_grapple.Spec.HangSeconds > 0f))
            {
                mover.Hook = null;
                return new CharacterMotorState(position, Vector3.Zero, false, false);
            }

            hook.Hanging = true;
            hook.HangRemaining = _grapple.Spec.HangSeconds;
            return new CharacterMotorState(position, Vector3.Zero, false, false);
        }

        private void ReleaseGrapple(Mover mover, GrappleEnd reason)
        {
            ReleaseGrapple(mover, reason, mover.LastProcessedTick);
        }

        private void ReleaseGrapple(Mover mover, GrappleEnd reason, int tick, float speed = 0f)
        {
            if (mover.Hook == null)
            {
                return;
            }

            mover.Hook = null;
            _log.WriteLine(ServerGrappleLog.Released(mover.Player.PlayerId, tick, reason, speed));
        }

        private sealed class GrappleHook
        {
            public GrappleHook(GrappleTarget target, Vector3 hangPosition, GrapplePull pull)
            {
                Target = target;
                HangPosition = hangPosition;
                Pull = pull;
            }

            public GrappleTarget Target { get; }

            public Vector3 HangPosition { get; }

            public GrapplePull Pull { get; }

            public bool Hanging { get; set; }

            public float HangRemaining { get; set; }
        }
    }
}
